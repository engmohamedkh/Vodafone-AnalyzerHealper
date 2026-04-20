using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Xml;
using System.Xml.Linq;
using AnalyzerHelper.Interfaces;
using AnalyzerHelper.Models;
using AnalyzerHelper.View;
using System.Text.RegularExpressions;

namespace AnalyzerHelper.Rules
{
	/// <summary>
	/// Need-interaction rule: validates workflow file naming only for files under Subprocess and Logic folders.
	/// Subprocess: {ShortName}_{Stage}_{WorkflowName}.xaml (path must contain a directory named "Subprocess").
	/// Logic: {ShortName}_{WorkflowName}.xaml (path must contain a directory named "Logic"); IAP files are skipped.
	/// On violation reported as Error so run shows errors and user must fix via interaction (Fix tab → confirm → dialog for ShortName/ProcessStage, then rename and update x:Class/DisplayName).
	/// Logic from OldAnalyzerHelper.RenameWorkflowFilesAction.
	///
	/// Batch mode: shows ONE config dialog for ShortName/ProcessStage, then renames all files at once
	/// and updates WorkflowFileName references across all XAML files in the project.
	/// </summary>
	public sealed class WorkflowFileNamingRule : IBatchAnalyzerRuleWithFix
	{
		public string RuleId => "VF-016";
		public string RuleName => "WorkflowFileNaming";
		public string DefaultRecommendation =>
			"Use Fix to rename the file and update internals.";
		public bool RequiresUserInteraction => true;

		private static readonly XNamespace XNs = "http://schemas.microsoft.com/winfx/2006/xaml";

		// ── Batch state ───────────────────────────────────────────────────
		private string? _batchShortName;
		private string? _batchProcessStage;
		private bool _batchPrepared;
		private bool _batchCancelled;
		private string _batchRootFolder = "";
		private Dictionary<string, string> _renameMap = new(); // oldPath → newStem

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
				string recommendation = isSubprocess
					? "Subprocess files must follow: {ShortName}_{Stage}_{WorkflowName}.xaml. Use Fix to rename and update internals."
					: "Logic files must follow: {ShortName}_{WorkflowName}.xaml. Use Fix to rename and update internals.";
				results.Add(new RuleCheckResult
				{
					RuleId = RuleId,
					RuleName = RuleName,
					Level = RuleLevel.Error,
					Message = message,
					FilePath = filePath,
					Recommendation = recommendation,
					RequiresUserInteraction = RequiresUserInteraction
				});
			}

			return results;
		}

		// =====================================================================
		//  PrepareBatch — single dialog, pre-compute all renames
		// =====================================================================
		public void PrepareBatch(IReadOnlyList<string> filePaths)
		{
			_batchPrepared = false;
			_batchCancelled = false;
			_batchShortName = null;
			_batchProcessStage = null;
			_renameMap.Clear();

			// Check if any files actually need renaming (must be in Subprocess or Logic)
			bool hasSubprocess = filePaths.Any(IsInSubprocessFolder);
			bool hasLogic = filePaths.Any(IsInLogicFolder);

			if (!hasSubprocess && !hasLogic)
			{
				_batchCancelled = true;
				return;
			}

			var dialog = new RenameConfigWindow(requireStage: hasSubprocess);
			if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.ShortName))
			{
				_batchCancelled = true;
				throw new OperationCanceledException($"User cancelled the {RuleName} batch fix dialog.");
			}

			_batchShortName = dialog.ShortName.Trim();
			_batchProcessStage = dialog.ProcessStage?.Trim() ?? "";
			_batchPrepared = true;

			// Find root folder for reference updates (common parent of all files)
			_batchRootFolder = FindCommonRoot(filePaths);

			// Pre-compute all rename mappings — include ALL files under Subprocess/Logic
			foreach (var path in filePaths)
			{
				if (string.IsNullOrWhiteSpace(path) || !path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
					continue;

				bool isSubprocess = IsInSubprocessFolder(path);
				bool isLogic = IsInLogicFolder(path);
				if (!isSubprocess && !isLogic) continue;

				string fileName = Path.GetFileNameWithoutExtension(path);

				// IAP files are not renamed (per convention)
				if (isLogic && fileName.StartsWith("IAP", StringComparison.OrdinalIgnoreCase)) continue;

				string workflowName = ExtractWorkflowName(fileName, isSubprocess);
				string newStem = isSubprocess
					? $"{_batchShortName}_{_batchProcessStage}_{workflowName}"
					: $"{_batchShortName}_{workflowName}";

				if (!fileName.Equals(newStem, StringComparison.OrdinalIgnoreCase))
					_renameMap[path] = newStem;
			}
		}

		// =====================================================================
		//  DefineAndFix — uses batch config, renames + updates references
		// =====================================================================
		public bool DefineAndFix(string filePath, string content, out string newContent)
		{
			newContent = content;
			if (_batchCancelled) return false;
			if (string.IsNullOrWhiteSpace(filePath) || !filePath.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
				return false;

			// Determine folder type
			bool isSubprocess = IsInSubprocessFolder(filePath);
			bool isLogic = IsInLogicFolder(filePath);

			if (!isSubprocess && !isLogic)
				return false;

			string fileName = Path.GetFileNameWithoutExtension(filePath);

			// IAP files are not renamed (per convention)
			if (isLogic && fileName.StartsWith("IAP", StringComparison.OrdinalIgnoreCase))
				return false;

			string shortName;
			string processStage;

			if (_batchPrepared)
			{
				// Use batch config — no per-file dialog
				shortName = _batchShortName ?? "";
				processStage = _batchProcessStage ?? "";
			}
			else
			{
				// Fallback: single-file mode
				var dialog = new RenameConfigWindow(requireStage: isSubprocess);
				if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.ShortName))
					throw new OperationCanceledException($"User cancelled the {RuleName} fix dialog.");

				shortName = dialog.ShortName.Trim();
				processStage = isSubprocess ? (dialog.ProcessStage?.Trim() ?? "") : "";
			}

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

				// ── Update WorkflowFileName references across all XAML files in project ──
				UpdateWorkflowFileReferences(filePath, newPath);
			}
			catch
			{
				newContent = updatedContent;
				return true; // Let FixRunner try writing to path if still there
			}

			newContent = updatedContent;
			return true;
		}

		// =====================================================================
		//  Update WorkflowFileName references across all XAML files
		// =====================================================================
		private void UpdateWorkflowFileReferences(string oldFilePath, string newFilePath)
		{
			string oldFileName = Path.GetFileName(oldFilePath);
			string newFileName = Path.GetFileName(newFilePath);

			// Patterns to scan
			string rootToScan = _batchRootFolder;
			if (string.IsNullOrEmpty(rootToScan))
				rootToScan = Path.GetDirectoryName(Path.GetDirectoryName(oldFilePath) ?? "") ?? "";

			if (string.IsNullOrEmpty(rootToScan) || !Directory.Exists(rootToScan))
				return;

			// Regex to find WorkflowFileName attribute that ends with the old filename (allowing any folder prefix and any slash)
			// Matches: WorkflowFileName="any\path\old.xaml" or WorkflowFileName="old.xaml"
			// Group 1: any folder prefix
			string patternStr = $@"WorkflowFileName=[""']([^""']*(?:[\\/])?){Regex.Escape(oldFileName)}[""']";
			var regex = new Regex(patternStr, RegexOptions.IgnoreCase);

			try
			{
				var allXaml = Directory.GetFiles(rootToScan, "*.xaml", SearchOption.AllDirectories);
				foreach (var xamlPath in allXaml)
				{
					if (string.Equals(xamlPath, newFilePath, StringComparison.OrdinalIgnoreCase))
						continue;

					try
					{
						string xamlContent = File.ReadAllText(xamlPath);
						if (regex.IsMatch(xamlContent))
						{
							string updatedContent = regex.Replace(xamlContent, $"WorkflowFileName=\"$1{newFileName}\"");
							if (updatedContent != xamlContent)
								File.WriteAllText(xamlPath, updatedContent);
						}
					}
					catch { }
				}
			}
			catch { }
		}

		// =====================================================================
		//  Helpers
		// =====================================================================

		/// <summary>Finds a root folder common to all paths.</summary>
		private static string FindCommonRoot(IReadOnlyList<string> paths)
		{
			if (paths == null || paths.Count == 0) return "";
			var first = Path.GetDirectoryName(paths[0]) ?? "";
			foreach (var p in paths)
			{
				var dir = Path.GetDirectoryName(p) ?? "";
				while (!string.IsNullOrEmpty(first) && !dir.StartsWith(first, StringComparison.OrdinalIgnoreCase))
					first = Path.GetDirectoryName(first) ?? "";
			}
			return first;
		}

		/// <summary>True if filePath lies under a directory named like "Subprocess" (path segment).</summary>
		private static bool IsInSubprocessFolder(string filePath)
		{
			if (string.IsNullOrWhiteSpace(filePath)) return false;
			string normalized = filePath.Replace('\\', '/').TrimEnd('/');
			var segments = normalized.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
			
			string[] variants = { "Subprocess", "Subprocesses", "Sub-process", "Sub-processes", "Sub Process", "Sub Processes" };
			return segments.Any(s => variants.Any(v => s.Equals(v, StringComparison.OrdinalIgnoreCase)));
		}

		/// <summary>True if filePath lies under a directory named like "Logic" (path segment).</summary>
		private static bool IsInLogicFolder(string filePath)
		{
			if (string.IsNullOrWhiteSpace(filePath)) return false;
			string normalized = filePath.Replace('\\', '/').TrimEnd('/');
			var segments = normalized.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
			
			string[] variants = { "Logic", "Logics", "Workflow Logic", "Workflows" };
			return segments.Any(s => variants.Any(v => s.Equals(v, StringComparison.OrdinalIgnoreCase)));
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
