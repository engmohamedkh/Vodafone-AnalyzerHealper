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
	/// Need-interaction rule: validates workflow file naming only for files under Subprocess and Logic folders.
	/// Subprocess: {ShortName}_{Stage}_{WorkflowName}.xaml (path must contain a directory named "Subprocess").
	/// Logic: {ShortName}_{WorkflowName}.xaml (path must contain a directory named "Logic"); IAP files are skipped.
	/// On violation reported as Error so run shows errors and user must fix via interaction (Fix tab → confirm → dialog for ShortName/ProcessStage, then rename and update x:Class/DisplayName).
	/// Logic from OldAnalyzerHelper.RenameWorkflowFilesAction.
	/// </summary>
	public sealed class WorkflowFileNamingRule : IAnalyzerRuleWithFix
	{
		public string RuleId => "VF-015";
		public string RuleName => "WorkflowFileNaming";
		public string DefaultRecommendation =>
			"File naming convention not followed. Subprocess: {ShortName}_{Stage}_{WorkflowName}.xaml, Logic: {ShortName}_{WorkflowName}.xaml. Use Fix to rename and update internals.";
		public bool RequiresUserInteraction => true;

		private static readonly XNamespace XNs = "http://schemas.microsoft.com/winfx/2006/xaml";

		public IReadOnlyList<RuleCheckResult> Check(string filePath, string content)
		{
			var results = new List<RuleCheckResult>();
			if (string.IsNullOrWhiteSpace(filePath) || !filePath.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
				return results;

			// Determine folder type
			bool isSubprocess = IsInSubprocessFolder(filePath);
			bool isLogic = IsInLogicFolder(filePath);

			if (!isSubprocess && !isLogic)
				return results; // Only check Subprocess and Logic folders

			string fileName = Path.GetFileNameWithoutExtension(filePath);

			// Logic: skip IAP files
			if (isLogic && fileName.StartsWith("IAP", StringComparison.OrdinalIgnoreCase))
				return results;

			// Check naming convention
			bool needsRename = false;
			string message = "";

			if (isSubprocess)
			{
				// Subprocess: should have at least 2 underscores (ShortName_Stage_WorkflowName)
				int underscoreCount = fileName.Count(c => c == '_');
				if (underscoreCount < 2)
				{
					needsRename = true;
					message = $"Subprocess file should follow pattern: {{ShortName}}_{{Stage}}_{{WorkflowName}}.xaml (found {underscoreCount} underscore(s)).";
				}
			}
			else if (isLogic)
			{
				// Logic: should have at least 1 underscore (ShortName_WorkflowName)
				int underscoreCount = fileName.Count(c => c == '_');
				if (underscoreCount < 1)
				{
					needsRename = true;
					message = $"Logic file should follow pattern: {{ShortName}}_{{WorkflowName}}.xaml (found {underscoreCount} underscore(s)).";
				}
			}

			// Also check if x:Class and DisplayName match the filename
			if (!needsRename && !string.IsNullOrWhiteSpace(content))
			{
				try
				{
					var doc = XDocument.Parse(content, LoadOptions.PreserveWhitespace);
					var classAttr = doc.Root?.Attribute(XNs + "Class");
					string? xClass = classAttr?.Value;

					var body = doc.Root?.Elements()
						.FirstOrDefault(e => e.Name.LocalName == "Sequence" ||
											 e.Name.LocalName == "Flowchart");
					string? displayName = body?.Attribute("DisplayName")?.Value;

					// Check if x:Class doesn't match filename
					if (!string.IsNullOrEmpty(xClass) && !xClass.Equals(fileName, StringComparison.OrdinalIgnoreCase))
					{
						needsRename = true;
						message = $"x:Class attribute ('{xClass}') does not match filename ('{fileName}').";
					}
					// Check if DisplayName doesn't match filename
					else if (!string.IsNullOrEmpty(displayName) && !displayName.Equals(fileName, StringComparison.OrdinalIgnoreCase))
					{
						needsRename = true;
						message = $"DisplayName attribute ('{displayName}') does not match filename ('{fileName}').";
					}
				}
				catch
				{
					// If parsing fails, ignore - we already checked underscore count
				}
			}

			if (needsRename)
			{
				results.Add(new RuleCheckResult
				{
					RuleId = RuleId,
					RuleName = RuleName,
					Level = RuleLevel.Error,
					Message = message,
					FilePath = filePath,
					Recommendation = DefaultRecommendation,
					RequiresUserInteraction = RequiresUserInteraction
				});
			}

			return results;
		}

		public bool DefineAndFix(string filePath, string content, out string newContent)
		{
			newContent = content;
			if (string.IsNullOrWhiteSpace(filePath) || !filePath.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
				return false;

			// Determine folder type
			bool isSubprocess = IsInSubprocessFolder(filePath);
			bool isLogic = IsInLogicFolder(filePath);

			if (!isSubprocess && !isLogic)
				return false;

			string fileName = Path.GetFileNameWithoutExtension(filePath);

			// Logic: skip IAP files
			if (isLogic && fileName.StartsWith("IAP", StringComparison.OrdinalIgnoreCase))
				return false;

			// Subprocess: request ShortName and Stage; Logic: request ShortName only
			var dialog = new RenameConfigWindow(requireStage: isSubprocess);
			if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.ShortName))
				return false;

			string shortName = dialog.ShortName.Trim();
			string processStage = isSubprocess ? (dialog.ProcessStage?.Trim() ?? "") : "";

			// Extract workflow name from current file name
			string workflowName = ExtractWorkflowName(fileName, isSubprocess);

			// Compute target file name
			string newStem = isSubprocess
				? $"{shortName}_{processStage}_{workflowName}"
				: $"{shortName}_{workflowName}";

			// Skip if already correct
			if (fileName.Equals(newStem, StringComparison.OrdinalIgnoreCase))
			{
				// Still check if x:Class and DisplayName need updating
				return UpdateInternals(filePath, content, newStem, out newContent);
			}

			// Update internals
			bool internalChanged = UpdateInternals(filePath, content, newStem, out string updatedContent);

			if (!internalChanged)
			{
				newContent = content;
				return false;
			}

			// Target path for the renamed file
			string dir = Path.GetDirectoryName(filePath) ?? "";
			string newPath = Path.Combine(dir, newStem + ".xaml");

			if (File.Exists(newPath) && !string.Equals(filePath, newPath, StringComparison.OrdinalIgnoreCase))
			{
				newContent = content;
				return false; // Destination exists — skip to avoid overwrite
			}

			// Write updated content (x:Class + DisplayName) then rename file so the real file is fixed
			try
			{
				// Write to current path first so the file has updated internals
				File.WriteAllText(filePath, updatedContent);
				// Rename to convention: ShortName_Stage_WorkflowName.xaml (or ShortName_WorkflowName for Logic)
				if (!string.Equals(filePath, newPath, StringComparison.OrdinalIgnoreCase))
					File.Move(filePath, newPath);
			}
			catch
			{
				newContent = updatedContent;
				return true; // Let FixRunner try writing to path if still there
			}

			newContent = updatedContent;
			return true;
		}

		/// <summary>True if filePath lies under a directory named exactly "Subprocess" (path segment).</summary>
		private static bool IsInSubprocessFolder(string filePath)
		{
			if (string.IsNullOrWhiteSpace(filePath)) return false;
			string normalized = filePath.Replace('\\', '/').TrimEnd('/');
			var segments = normalized.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
			return segments.Any(s => s.Equals("Subprocess", StringComparison.OrdinalIgnoreCase));
		}

		/// <summary>True if filePath lies under a directory named exactly "Logic" (path segment).</summary>
		private static bool IsInLogicFolder(string filePath)
		{
			if (string.IsNullOrWhiteSpace(filePath)) return false;
			string normalized = filePath.Replace('\\', '/').TrimEnd('/');
			var segments = normalized.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
			return segments.Any(s => s.Equals("Logic", StringComparison.OrdinalIgnoreCase));
		}

		private static string ExtractWorkflowName(string stem, bool isSubprocess)
		{
			int underscoresNeeded = isSubprocess ? 2 : 1;
			int pos = 0;
			int found = 0;

			while (found < underscoresNeeded && pos < stem.Length)
			{
				int next = stem.IndexOf('_', pos);
				if (next < 0) break;
				pos = next + 1;
				found++;
			}

			// If we found enough underscores, return everything from that point
			if (found == underscoresNeeded && pos <= stem.Length)
				return stem.Substring(pos);

			// Not enough underscores — keep the whole stem as workflow name
			return stem;
		}

		private static bool UpdateInternals(string filePath, string originalContent, string newStem, out string newContent)
		{
			newContent = originalContent;

			string original;
			try { original = string.IsNullOrWhiteSpace(originalContent) ? File.ReadAllText(filePath) : originalContent; }
			catch { return false; }

			XDocument doc;
			try { doc = XDocument.Parse(original, LoadOptions.PreserveWhitespace); }
			catch { return false; }

			bool changed = false;
			var classAttrName = XNs + "Class";

			// ── x:Class on root Activity element (add if missing) ─────────────
			if (doc.Root != null)
			{
				var classAttr = doc.Root.Attribute(classAttrName);
				if (classAttr != null)
				{
					if (classAttr.Value != newStem)
					{
						classAttr.Value = newStem;
						changed = true;
					}
				}
				else
				{
					doc.Root.Add(new XAttribute(classAttrName, newStem));
					changed = true;
				}
			}

			// ── DisplayName on root Sequence or Flowchart (add if missing) ────
			var body = doc.Root?.Elements()
				.FirstOrDefault(e => e.Name.LocalName == "Sequence" ||
									 e.Name.LocalName == "Flowchart");

			if (body != null)
			{
				var dnAttr = body.Attribute("DisplayName");
				if (dnAttr != null)
				{
					if (dnAttr.Value != newStem)
					{
						dnAttr.Value = newStem;
						changed = true;
					}
				}
				else
				{
					body.Add(new XAttribute("DisplayName", newStem));
					changed = true;
				}
			}

			// ── strComponentName variable: set Default to new workflow name ──
			foreach (var varEl in doc.Descendants().Where(e => e.Name.LocalName == "Variable"))
			{
				string? varName = varEl.Attributes().FirstOrDefault(a => a.Name.LocalName == "Name")?.Value;
				if (string.IsNullOrEmpty(varName) || !varName.Equals("strComponentName", StringComparison.OrdinalIgnoreCase))
					continue;
				var defaultAttr = varEl.Attributes().FirstOrDefault(a => a.Name.LocalName == "Default");
				if (defaultAttr != null)
				{
					if (defaultAttr.Value != newStem)
					{
						defaultAttr.Value = newStem;
						changed = true;
					}
				}
				else
				{
					var defaultChild = varEl.Elements().FirstOrDefault(e => e.Name.LocalName == "Variable.Default");
					if (defaultChild != null)
					{
						string currentVal = defaultChild.Value?.Trim() ?? "";
						if (currentVal != newStem)
						{
							defaultChild.SetValue(newStem);
							changed = true;
						}
					}
					else
					{
						varEl.Add(new XAttribute("Default", newStem));
						changed = true;
					}
				}
			}

			if (!changed)
			{
				newContent = original;
				return false;
			}

			// ── Serialise back ────────────────────────────────────────────────
			try
			{
				bool crlf = original.Contains("\r\n");
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

				string body2 = sb.ToString();
				if (original.TrimStart().StartsWith("<?xml"))
				{
					int end = original.IndexOf("?>", StringComparison.Ordinal) + 2;
					if (end >= 2)
						body2 = original.Substring(0, end) +
								(crlf ? "\r\n" : "\n") + body2;
				}

				newContent = body2;
				return true;
			}
			catch
			{
				newContent = original;
				return false;
			}
		}
	}
}
