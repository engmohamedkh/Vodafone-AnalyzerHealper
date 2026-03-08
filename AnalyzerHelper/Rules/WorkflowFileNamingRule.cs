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
    /// Need-interaction rule: checks workflow file naming convention.
    /// Subprocess: {ShortName}_{Stage}_{WorkflowName}.xaml
    /// Logic: {ShortName}_{WorkflowName}.xaml (skips IAP files)
    /// Fix shows dialog to collect ShortName and ProcessStage, then renames file and updates x:Class and DisplayName.
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
                    Level = RuleLevel.Warning,
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

            // Show dialog to get ShortName and ProcessStage
            var dialog = new RenameConfigWindow();
            if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.ShortName))
                return false;

            string shortName = dialog.ShortName.Trim();
            string processStage = dialog.ProcessStage?.Trim() ?? "";

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

            // Check if target file already exists
            string dir = Path.GetDirectoryName(filePath) ?? "";
            string newPath = Path.Combine(dir, newStem + ".xaml");
            
            if (File.Exists(newPath) && !string.Equals(filePath, newPath, StringComparison.OrdinalIgnoreCase))
            {
                // Destination exists — skip to avoid overwrite
                newContent = content;
                return false;
            }

            // Write updated content to file first (before rename)
            // FixRunner will also write, but that's okay - it will write the same content
            try
            {
                File.WriteAllText(filePath, updatedContent);
            }
            catch
            {
                // If write fails, still return updated content - FixRunner will try to write it
                newContent = updatedContent;
                return true;
            }

            // Rename file on disk
            try
            {
                if (!string.Equals(filePath, newPath, StringComparison.OrdinalIgnoreCase))
                {
                    File.Move(filePath, newPath);
                }
            }
            catch
            {
                // If rename fails, content is still updated at old path
                // This is acceptable - user can manually rename or run fix again
            }
            
            newContent = updatedContent;
            return true;
        }

        private static bool IsInSubprocessFolder(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return false;
            string normalized = filePath.Replace('\\', '/');
            return normalized.Contains("/Subprocess/", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Contains("\\Subprocess\\", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsInLogicFolder(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return false;
            string normalized = filePath.Replace('\\', '/');
            return normalized.Contains("/Logic/", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Contains("\\Logic\\", StringComparison.OrdinalIgnoreCase);
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

            // ── x:Class on root Activity element ─────────────────────────────
            var classAttr = doc.Root?.Attribute(XNs + "Class");
            if (classAttr != null && classAttr.Value != newStem)
            {
                classAttr.Value = newStem;
                changed = true;
            }

            // ── DisplayName on root Sequence or Flowchart ─────────────────────
            var body = doc.Root?.Elements()
                .FirstOrDefault(e => e.Name.LocalName == "Sequence" ||
                                     e.Name.LocalName == "Flowchart");

            if (body != null)
            {
                var dnAttr = body.Attribute("DisplayName");
                if (dnAttr != null && dnAttr.Value != newStem)
                {
                    dnAttr.Value = newStem;
                    changed = true;
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
