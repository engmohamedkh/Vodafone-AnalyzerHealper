using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
	/// Fix requires user interaction: shows a dialog with one input per section, then updates the workflow.
	/// Logic from OldAnalyzerHelper/Fixannotationaction.cs and VodafoneWorkFlowsCustomRules/Annotations.cs.
	/// </summary>
	public sealed class AnnotationRule : IAnalyzerRuleWithFix
	{
		public string RuleId => "VF-016";
		public string RuleName => "Annotation";
		public string DefaultRecommendation =>
			"Workflow annotation must contain all sections: Component Name, Description, Pre Condition, Post Condition, PDD Section. Use Fix to edit via the annotation dialog.";
		public bool RequiresUserInteraction => true;

		private static readonly string[] SectionKeys = { "Component Name", "Description", "Pre Condition", "Post Condition", "PDD Section" };
		private static readonly XNamespace Sap2010 = "http://schemas.microsoft.com/netfx/2010/xaml/activities/presentation";

		public IReadOnlyList<RuleCheckResult> Check(string filePath, string content)
		{
			var results = new List<RuleCheckResult>();
			if (string.IsNullOrWhiteSpace(content)) return results;

			XDocument doc;
			try { doc = XDocument.Parse(content, LoadOptions.PreserveWhitespace); }
			catch { return results; }

			XElement root = FindRootSequenceOrFlowchart(doc);
			if (root == null) return results;

			// Skip framework workflows if desired (optional)
			string displayName = (string)root.Attribute("DisplayName") ?? "";
			if (displayName.EndsWith("_loader", StringComparison.OrdinalIgnoreCase) ||
			    displayName.EndsWith("_worker", StringComparison.OrdinalIgnoreCase) ||
			    displayName.EndsWith("_loaderworker", StringComparison.OrdinalIgnoreCase))
				return results;

			// Find annotation attribute (LocalName is unique; namespace may differ after XDocument load)
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

		public bool DefineAndFix(string filePath, string content, out string newContent)
		{
			newContent = content;
			if (string.IsNullOrWhiteSpace(content)) return false;

			XDocument doc;
			try { doc = XDocument.Parse(content, LoadOptions.PreserveWhitespace); }
			catch { return false; }

			XElement root = FindRootSequenceOrFlowchart(doc);
			if (root == null) return false;

			var annotAttr = root.Attributes().FirstOrDefault(a =>
				string.Equals(a.Name.LocalName, "Annotation.AnnotationText", StringComparison.Ordinal));

			string componentName = Path.GetFileNameWithoutExtension(filePath);
			string description = "Narrative of what tasks the component will perform";
			string preCondition = "#NA";
			string postCondition = "#NA";
			string pddSection = "#NA";

			if (annotAttr != null && !string.IsNullOrWhiteSpace(annotAttr.Value))
			{
				var parsed = ParseAnnotation(annotAttr.Value);
				componentName = GetSectionValue(parsed, "Component Name").Trim();
				if (string.IsNullOrEmpty(componentName)) componentName = Path.GetFileNameWithoutExtension(filePath);
				description = GetSectionValue(parsed, "Description").Trim();
				if (string.IsNullOrEmpty(description)) description = "Narrative of what tasks the component will perform";
				preCondition = GetSectionValue(parsed, "Pre Condition").Trim();
				if (string.IsNullOrEmpty(preCondition)) preCondition = "#NA";
				postCondition = GetSectionValue(parsed, "Post Condition").Trim();
				if (string.IsNullOrEmpty(postCondition)) postCondition = "#NA";
				pddSection = GetSectionValue(parsed, "PDD Section").Trim();
				if (string.IsNullOrEmpty(pddSection)) pddSection = "#NA";
			}

			var dialog = new AnnotationConfigWindow(
				componentName, description, preCondition, postCondition, pddSection);
			if (dialog.ShowDialog() != true) return false;

			string newAnnotationText = BuildAnnotationText(
				dialog.ComponentName?.Trim() ?? componentName,
				dialog.Description?.Trim() ?? description,
				dialog.PreCondition?.Trim() ?? preCondition,
				dialog.PostCondition?.Trim() ?? postCondition,
				dialog.PddSection?.Trim() ?? pddSection);

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

		private static XElement FindRootSequenceOrFlowchart(XDocument doc)
		{
			return doc.Root?.Elements()
				.FirstOrDefault(e =>
					e.Name.LocalName == "Sequence" ||
					e.Name.LocalName == "Flowchart");
		}

		/// <summary>Parse annotation attribute value (newlines may be &#xA; or literal).</summary>
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
			// Try exact key then case-insensitive
			if (parsed.TryGetValue(key, out var v)) return v ?? "";
			var k = parsed.Keys.FirstOrDefault(x => string.Equals(x, key, StringComparison.OrdinalIgnoreCase));
			return k != null ? (parsed[k] ?? "") : "";
		}

		private static string BuildAnnotationText(string componentName, string description,
			string preCondition, string postCondition, string pddSection)
		{
			return $"Component Name: {componentName}\nDescription: {description}\nPre Condition: {preCondition}\nPost Condition: {postCondition}\nPDD Section: {pddSection}";
		}
	}
}
