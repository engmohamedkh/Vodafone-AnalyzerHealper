using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using AnalyzerHelper.Interfaces;
using AnalyzerHelper.Models;
using AnalyzerHelper.View;

namespace AnalyzerHelper.Rules
{
	/// <summary>
	/// Validates that the root Sequence/Flowchart has annotation with all required sections.
	/// Sections: Component Name, Description, Pre Condition, Post Condition, PDD Section.
	/// Fix shows a batch table for ALL files, auto-populating from workflow structure.
	/// </summary>
	public sealed class AnnotationRule : IBatchAnalyzerRuleWithFix
	{
		public string RuleId => "VF-016";
		public string RuleName => "Annotation";
		public string DefaultRecommendation =>
			"Workflow annotation must contain all sections: Component Name, Description, Pre Condition, Post Condition, PDD Section. Use Fix to edit via the annotation dialog.";
		public bool RequiresUserInteraction => true;

		private static readonly string[] SectionKeys = { "Component Name", "Description", "Pre Condition", "Post Condition", "PDD Section" };
		private static readonly XNamespace Sap2010 = "http://schemas.microsoft.com/netfx/2010/xaml/activities/presentation";

		// ── Batch state ──
		private bool _batchPrepared;
		private bool _batchCancelled;
		private Dictionary<string, AnnotationRow>? _batchRows;

		public IReadOnlyList<RuleCheckResult> Check(string filePath, string content)
		{
			var results = new List<RuleCheckResult>();
			if (string.IsNullOrWhiteSpace(content)) return results;

			XDocument doc;
			try { doc = XDocument.Parse(content, LoadOptions.PreserveWhitespace); }
			catch { return results; }

			XElement root = FindRootSequenceOrFlowchart(doc);
			if (root == null) return results;

			string displayName = (string)root.Attribute("DisplayName") ?? "";
			if (displayName.EndsWith("_loader", StringComparison.OrdinalIgnoreCase) ||
			    displayName.EndsWith("_worker", StringComparison.OrdinalIgnoreCase) ||
			    displayName.EndsWith("_loaderworker", StringComparison.OrdinalIgnoreCase))
				return results;

			var annotAttr = root.Attributes().FirstOrDefault(a =>
				string.Equals(a.Name.LocalName, "Annotation.AnnotationText", StringComparison.Ordinal));

			var missing = new List<string>();
			if (annotAttr == null || string.IsNullOrWhiteSpace(annotAttr.Value))
			{
				missing.AddRange(SectionKeys);
			}
			else
			{
				var parsed = ParseAnnotation(annotAttr.Value);
				foreach (var key in SectionKeys)
				{
					string value = GetSectionValue(parsed, key);
					if (string.IsNullOrWhiteSpace(value))
						missing.Add(key);
				}
			}

			if (missing.Count == 0) return results;

			results.Add(new RuleCheckResult
			{
				RuleId = RuleId,
				RuleName = RuleName,
				Level = RuleLevel.Error,
				Message = "Annotation missing or incomplete. Missing section(s): " + string.Join(", ", missing) + ".",
				FilePath = filePath,
				Recommendation = DefaultRecommendation,
				RequiresUserInteraction = RequiresUserInteraction
			});
			return results;
		}

		// =====================================================================
		//  PrepareBatch — scan all files, show single batch dialog
		// =====================================================================
		public void PrepareBatch(IReadOnlyList<string> filePaths)
		{
			_batchPrepared = false;
			_batchCancelled = false;
			_batchRows = new Dictionary<string, AnnotationRow>(StringComparer.OrdinalIgnoreCase);

			string commonRoot = FindCommonRoot(filePaths);
			var rows = new List<AnnotationRow>();

			foreach (var path in filePaths)
			{
				if (string.IsNullOrWhiteSpace(path) || !path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
					continue;

				string content;
				try { content = File.ReadAllText(path); }
				catch { continue; }

				XDocument doc;
				try { doc = XDocument.Parse(content, LoadOptions.PreserveWhitespace); }
				catch { continue; }

				XElement root = FindRootSequenceOrFlowchart(doc);
				if (root == null) continue;

				// Skip framework workflows
				string displayName = (string)root.Attribute("DisplayName") ?? "";
				if (displayName.EndsWith("_loader", StringComparison.OrdinalIgnoreCase) ||
				    displayName.EndsWith("_worker", StringComparison.OrdinalIgnoreCase) ||
				    displayName.EndsWith("_loaderworker", StringComparison.OrdinalIgnoreCase))
					continue;

				string rel = !string.IsNullOrEmpty(commonRoot) && path.StartsWith(commonRoot, StringComparison.OrdinalIgnoreCase)
					? path.Substring(commonRoot.Length).TrimStart(Path.DirectorySeparatorChar, '/', '\\')
					: Path.GetFileName(path);

				// Auto-populate from existing annotation
				string componentName = Path.GetFileNameWithoutExtension(path);
				string description = "Narrative of what tasks the component will perform";
				string preCondition = "#NA";
				string postCondition = "#NA";
				string pddSection = "#NA";

				var annotAttr = root.Attributes().FirstOrDefault(a =>
					string.Equals(a.Name.LocalName, "Annotation.AnnotationText", StringComparison.Ordinal));

				if (annotAttr != null && !string.IsNullOrWhiteSpace(annotAttr.Value))
				{
					var parsed = ParseAnnotation(annotAttr.Value);
					string v;
					v = GetSectionValue(parsed, "Component Name").Trim();
					if (!string.IsNullOrEmpty(v)) componentName = v;
					v = GetSectionValue(parsed, "Description").Trim();
					if (!string.IsNullOrEmpty(v)) description = v;
					v = GetSectionValue(parsed, "Pre Condition").Trim();
					if (!string.IsNullOrEmpty(v)) preCondition = v;
					v = GetSectionValue(parsed, "Post Condition").Trim();
					if (!string.IsNullOrEmpty(v)) postCondition = v;
					v = GetSectionValue(parsed, "PDD Section").Trim();
					if (!string.IsNullOrEmpty(v)) pddSection = v;
				}

				// Auto-populate Pre/Post Condition from workflow structure
				if (IsDefaultValue(preCondition))
				{
					string extracted = ExtractConditionFromSequence(doc, "Pre Condition");
					preCondition = !string.IsNullOrWhiteSpace(extracted) ? extracted : "#NA";
				}
				if (IsDefaultValue(postCondition))
				{
					string extracted = ExtractConditionFromSequence(doc, "Post Condition");
					postCondition = !string.IsNullOrWhiteSpace(extracted) ? extracted : "#NA";
				}

				var row = new AnnotationRow
				{
					FilePath = path,
					File = rel,
					ComponentName = componentName,
					Description = description,
					PreCondition = preCondition,
					PostCondition = postCondition,
					PddSection = pddSection
				};
				rows.Add(row);
				_batchRows[path] = row;
			}

			if (rows.Count == 0)
			{
				_batchCancelled = true;
				return;
			}

			var dialog = new AnnotationBatchWindow(rows);
			if (dialog.ShowDialog() != true || !dialog.Applied)
			{
				_batchCancelled = true;
				return;
			}

			_batchPrepared = true;
		}

		// =====================================================================
		//  DefineAndFix — apply annotation using batch values
		// =====================================================================
		public bool DefineAndFix(string filePath, string content, out string newContent)
		{
			newContent = content;
			if (_batchCancelled) return false;
			if (string.IsNullOrWhiteSpace(content)) return false;

			XDocument doc;
			try { doc = XDocument.Parse(content, LoadOptions.PreserveWhitespace); }
			catch { return false; }

			XElement root = FindRootSequenceOrFlowchart(doc);
			if (root == null) return false;

			AnnotationRow? row = null;
			if (_batchPrepared && _batchRows != null)
			{
				if (!_batchRows.TryGetValue(filePath, out row))
					return false;
				
				if (!row.IsSelectedForFix) return false;
			}
			else
			{
				// Fallback single-file mode (shouldn't happen in batch)
				return false;
			}

			var annotAttr = root.Attributes().FirstOrDefault(a =>
				string.Equals(a.Name.LocalName, "Annotation.AnnotationText", StringComparison.Ordinal));

			string newAnnotationText = BuildAnnotationText(
				row.ComponentName?.Trim() ?? Path.GetFileNameWithoutExtension(filePath),
				row.Description?.Trim() ?? "",
				row.PreCondition?.Trim() ?? "#NA",
				row.PostCondition?.Trim() ?? "#NA",
				row.PddSection?.Trim() ?? "#NA");

			XName annotName = Sap2010 + "Annotation.AnnotationText";
			if (annotAttr != null)
				annotAttr.Value = newAnnotationText;
			else
				root.Add(new XAttribute(annotName, newAnnotationText));

			try
			{
				bool crlf = content.Contains("\r\n");
				var sb = new StringBuilder();
				var settings = new XmlWriterSettings
				{
					OmitXmlDeclaration = true,
					Indent = true,
					IndentChars = "  ",
					NewLineChars = crlf ? "\r\n" : "\n",
					NewLineHandling = NewLineHandling.Replace,
				};
				using (var sw = new StringWriter(sb))
				using (var xw = XmlWriter.Create(sw, settings))
					doc.Save(xw);

				newContent = sb.ToString();
				if (content.TrimStart().StartsWith("<?xml"))
				{
					int end = content.IndexOf("?>", StringComparison.Ordinal) + 2;
					if (end >= 2)
						newContent = content.Substring(0, end) + (crlf ? "\r\n" : "\n") + newContent;
				}
				return true;
			}
			catch
			{
				newContent = content;
				return false;
			}
		}

		// ── Helpers ───────────────────────────────────────────────────────

		/// <summary>Extract Info_Log StrMessage from If.Then inside a Sequence with given DisplayName.</summary>
		private static string ExtractConditionFromSequence(XDocument doc, string sequenceDisplayName)
		{
			// Find Sequence with matching DisplayName
			var seq = doc.Descendants()
				.FirstOrDefault(e => e.Name.LocalName == "Sequence" &&
					string.Equals((string?)e.Attribute("DisplayName") ?? "", sequenceDisplayName, StringComparison.OrdinalIgnoreCase));
			if (seq == null) return "";

			// Find If.Then inside it
			var ifThen = seq.Descendants()
				.Where(e => e.Name.LocalName == "If")
				.SelectMany(ifEl => ifEl.Elements().Where(c => c.Name.LocalName == "If.Then"))
				.FirstOrDefault();
			if (ifThen == null) return "";

			// Find Info_Log StrMessage in the Then branch
			var infoLog = ifThen.Descendants()
				.FirstOrDefault(e => e.Name.LocalName == "Info_Log");
			if (infoLog == null) return "";

			return (string?)infoLog.Attribute("StrMessage") ?? "";
		}

		private static XElement FindRootSequenceOrFlowchart(XDocument doc)
		{
			return doc.Root?.Elements()
				.FirstOrDefault(e =>
					e.Name.LocalName == "Sequence" ||
					e.Name.LocalName == "Flowchart");
		}

		private static Dictionary<string, string> ParseAnnotation(string attrValue)
		{
			var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			if (string.IsNullOrWhiteSpace(attrValue)) return result;

			string normalized = attrValue.Replace("&#xD;&#xA;", "\n").Replace("&#xA;", "\n").Replace("\r\n", "\n");
			string currentKey = null;

			foreach (var line in normalized.Split('\n'))
			{
				string trimmed = line.Trim();
				if (string.IsNullOrEmpty(trimmed)) continue;

				string matchedKey = SectionKeys.FirstOrDefault(k =>
					trimmed.StartsWith(k.Split(' ')[0], StringComparison.OrdinalIgnoreCase) && trimmed.Contains(":"));
				if (matchedKey != null)
				{
					int idx = trimmed.IndexOf(':');
					result[matchedKey] = trimmed.Substring(idx + 1).Trim();
					currentKey = matchedKey;
				}
				else if (currentKey != null && result.ContainsKey(currentKey))
				{
					result[currentKey] += "\n" + trimmed;
				}
			}

			return result;
		}

		private static string GetSectionValue(Dictionary<string, string> parsed, string key)
		{
			if (parsed.TryGetValue(key, out var v)) return v ?? "";
			var k = parsed.Keys.FirstOrDefault(x => string.Equals(x, key, StringComparison.OrdinalIgnoreCase));
			return k != null ? (parsed[k] ?? "") : "";
		}

		private static string BuildAnnotationText(string componentName, string description,
			string preCondition, string postCondition, string pddSection)
		{
			return $"Component Name: {componentName}\nDescription: {description}\nPre Condition: {preCondition}\nPost Condition: {postCondition}\nPDD Section: {pddSection}";
		}

		private static bool IsDefaultValue(string value)
		{
			if (string.IsNullOrWhiteSpace(value)) return true;
			string v = value.Trim().ToUpperInvariant();
			return v == "#NA" || v == "NA" || v == "N/A";
		}

		private static string FindCommonRoot(IReadOnlyList<string> paths)
		{
			if (paths == null || paths.Count == 0) return "";
			var dirs = paths.Where(p => !string.IsNullOrWhiteSpace(p))
				.Select(p => Path.GetDirectoryName(p) ?? "").Distinct(StringComparer.OrdinalIgnoreCase).ToList();
			if (dirs.Count == 0) return "";
			string common = dirs[0];
			foreach (var d in dirs.Skip(1))
			{
				while (!string.IsNullOrEmpty(common) && !d.StartsWith(common, StringComparison.OrdinalIgnoreCase))
					common = Path.GetDirectoryName(common) ?? "";
			}
			return common;
		}
	}
}
