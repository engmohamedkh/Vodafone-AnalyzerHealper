using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using AnalyzerHelper.Interfaces;
using AnalyzerHelper.Models;

namespace AnalyzerHelper.Rules
{
    /// <summary>
    /// VF-049 — Validates that entry workflows (Loader / Worker) properly invoke and configure
    /// the input file validation workflow (Logic\IAP_ValidateInputFiles.xaml).
    /// </summary>
    public sealed class ValidateInput : IAnalyzerRule
    {
        private static readonly HashSet<string> ExemptArgs = new(StringComparer.OrdinalIgnoreCase)
        {
            "WorkflowFileName",
            "ContinueOnError",
            "Timeout",
            "Log Entry",
            "Log Exit",
            "ArgumentsVariable",
            "LogLevel"
        };

        public string RuleId => "VF-049";
        public string RuleName => "InputValidation";
        public string DefaultRecommendation => "Please Use Input Validation file to validate inputs";
        public bool RequiresUserInteraction => false;

        public IReadOnlyList<RuleCheckResult> Check(string filePath, string content)
        {
            var results = new List<RuleCheckResult>();

            if (string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(content))
                return results;

            if (!filePath.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
                return results;

            // Applies strictly to Loader or Worker workflows (e.g. *_loader.xaml, *_worker.xaml, *_loaderworker.xaml)
            string fileName = Path.GetFileName(filePath);
            string[] parts = fileName.Split('_');
            string lastPart = parts.Length > 0 ? parts[parts.Length - 1] : fileName;

            bool isWorkerOrLoader = lastPart.IndexOf("worker.xaml", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    lastPart.IndexOf("loader.xaml", StringComparison.OrdinalIgnoreCase) >= 0;

            if (!isWorkerOrLoader)
                return results;

            if (!XamlActivityHelper.TryParse(content, out var doc) || doc?.Root == null)
                return results;

            // 1. Check if IAP_ValidateInputFiles.xaml exists in the project
            string? projectDir = FindProjectDirectory(filePath);
            bool fileExists = false;
            if (!string.IsNullOrEmpty(projectDir))
            {
                string logicPath = Path.Combine(projectDir, "Logic", "IAP_ValidateInputFiles.xaml");
                if (File.Exists(logicPath))
                {
                    fileExists = true;
                }
                else
                {
                    try
                    {
                        fileExists = Directory.EnumerateFiles(projectDir, "*IAP_ValidateInputFiles.xaml", SearchOption.AllDirectories).Any();
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }

            // 2. Search for InvokeWorkflowFile calling IAP_ValidateInputFiles.xaml
            bool validateFound = false;
            var argIssues = new List<string>();

            foreach (var el in doc.Descendants())
            {
                if (XamlActivityHelper.IsPropertyElement(el))
                    continue;

                if (!el.Name.LocalName.Equals("InvokeWorkflowFile", StringComparison.OrdinalIgnoreCase))
                    continue;

                string? wfTarget = el.Attribute("WorkflowFileName")?.Value;
                if (string.IsNullOrEmpty(wfTarget))
                {
                    wfTarget = el.Elements()
                        .FirstOrDefault(c => c.Name.LocalName.Equals("WorkflowFileName", StringComparison.OrdinalIgnoreCase))
                        ?.Value;
                }

                if (wfTarget != null && wfTarget.IndexOf("IAP_ValidateInputFiles.xaml", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    validateFound = true;
                    string invokeDisplayName = XamlActivityHelper.GetDisplayName(el);

                    // Check arguments container
                    var argsContainer = el.Elements().FirstOrDefault(c =>
                        c.Name.LocalName.Equals("InvokeWorkflowFile.Arguments", StringComparison.OrdinalIgnoreCase) ||
                        c.Name.LocalName.Equals("Arguments", StringComparison.OrdinalIgnoreCase));

                    if (argsContainer != null)
                    {
                        foreach (var argEl in argsContainer.Elements())
                        {
                            string argLocalName = argEl.Name.LocalName;
                            string direction = "In";
                            if (argLocalName.StartsWith("Out", StringComparison.OrdinalIgnoreCase))
                                direction = "Out";
                            else if (argLocalName.StartsWith("InOut", StringComparison.OrdinalIgnoreCase))
                                direction = "InOut";

                            string? argName = argEl.Attributes().FirstOrDefault(a =>
                                string.Equals(a.Name.LocalName, "Key", StringComparison.OrdinalIgnoreCase))?.Value;

                            if (string.IsNullOrWhiteSpace(argName))
                            {
                                argName = argEl.Attributes().FirstOrDefault(a =>
                                    string.Equals(a.Name.LocalName, "Name", StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(a.Name.LocalName, "DisplayName", StringComparison.OrdinalIgnoreCase))?.Value;
                            }

                            if (string.IsNullOrWhiteSpace(argName) || ExemptArgs.Contains(argName))
                                continue;

                            string expr = GetArgumentExpression(argEl);
                            if (IsEmptyArgumentExpression(expr))
                            {
                                argIssues.Add($"The Input file validation Workflow '{invokeDisplayName}' has empty {direction} argument: {argName}.");
                            }
                        }
                    }

                    break;
                }
            }

            // 3. Collect errors matching legacy rule reporting
            if (!validateFound)
            {
                results.Add(new RuleCheckResult
                {
                    RuleId = RuleId,
                    RuleName = RuleName,
                    Level = RuleLevel.Warning,
                    FilePath = filePath,
                    Message = "The Input file validation Workflow is not used in the process.",
                    Recommendation = DefaultRecommendation,
                    RequiresUserInteraction = false
                });
            }

            foreach (var issue in argIssues)
            {
                results.Add(new RuleCheckResult
                {
                    RuleId = RuleId,
                    RuleName = RuleName,
                    Level = RuleLevel.Warning,
                    FilePath = filePath,
                    Message = issue,
                    Recommendation = DefaultRecommendation,
                    RequiresUserInteraction = false
                });
            }

            if (!fileExists)
            {
                results.Add(new RuleCheckResult
                {
                    RuleId = RuleId,
                    RuleName = RuleName,
                    Level = RuleLevel.Warning,
                    FilePath = filePath,
                    Message = "The Input file validation Workflow doesn't exist in the process.",
                    Recommendation = DefaultRecommendation,
                    RequiresUserInteraction = false
                });
            }

            return results;
        }

        private static string GetArgumentExpression(XElement argEl)
        {
            var valAttr = argEl.Attributes().FirstOrDefault(a =>
                string.Equals(a.Name.LocalName, "Value", StringComparison.OrdinalIgnoreCase));
            if (valAttr != null && !string.IsNullOrWhiteSpace(valAttr.Value))
                return valAttr.Value.Trim();

            var vb = argEl.Descendants().FirstOrDefault(d =>
                d.Name.LocalName.IndexOf("VisualBasicValue", StringComparison.OrdinalIgnoreCase) >= 0 ||
                d.Name.LocalName.IndexOf("VisualBasicReference", StringComparison.OrdinalIgnoreCase) >= 0 ||
                d.Name.LocalName.Equals("Literal", StringComparison.OrdinalIgnoreCase));
            if (vb != null && !string.IsNullOrWhiteSpace(vb.Value))
                return vb.Value.Trim();

            return argEl.Value.Trim();
        }

        private static bool IsEmptyArgumentExpression(string expr)
        {
            if (string.IsNullOrWhiteSpace(expr))
                return true;

            string normalized = expr.Trim();
            if (normalized.StartsWith("[") && normalized.EndsWith("]") && normalized.Length >= 2)
                normalized = normalized.Substring(1, normalized.Length - 2).Trim();

            return normalized.Equals("{}", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Equals("new string(){}", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Equals("new string() {}", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Equals("new String(){}", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Equals("new String() {}", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Equals("{\"\"}", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Equals("\"\"", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Equals("Nothing", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Equals("null", StringComparison.OrdinalIgnoreCase);
        }

        private static string? FindProjectDirectory(string filePath)
        {
            var dir = Path.GetDirectoryName(filePath);
            while (!string.IsNullOrEmpty(dir))
            {
                if (File.Exists(Path.Combine(dir, "project.json")))
                    return dir;
                dir = Path.GetDirectoryName(dir);
            }
            return Path.GetDirectoryName(filePath);
        }
    }
}
