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
    /// VF-050 — Validates that UploadItems workflows invoke the workqueue duplicate check
    /// workflow (Logic\IAP_CheckReferenceinQueueItems.xaml) and pass non-empty arguments.
    /// </summary>
    public sealed class WQDuplicates : IAnalyzerRule
    {
        private static readonly HashSet<string> ExemptArgs = new(StringComparer.OrdinalIgnoreCase)
        {
            "workflowfilename",
            "continueonerror",
            "timeout",
            "logentry",
            "logexit",
            "argumentsvariable",
            "loglevel"
        };

        public string RuleId => "VF-050";
        public string RuleName => "WQ Duplication Check";
        public string DefaultRecommendation => "Please Use WQ Duplication Check file to validate Queue References";
        public bool RequiresUserInteraction => false;

        public IReadOnlyList<RuleCheckResult> Check(string filePath, string content)
        {
            var results = new List<RuleCheckResult>();

            if (string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(content))
                return results;

            if (!filePath.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
                return results;

            // Target workflows: UploadItems (e.g. UploadItems.xaml, Stage_UploadItems.xaml)
            string fileName = Path.GetFileName(filePath);
            string[] parts = fileName.Split('_');
            string lastPart = parts.Length > 0 ? parts[parts.Length - 1] : fileName;

            if (!lastPart.Equals("uploaditems.xaml", StringComparison.OrdinalIgnoreCase))
                return results;

            if (!XamlActivityHelper.TryParse(content, out var doc) || doc?.Root == null)
                return results;

            bool duplicateCheckFound = false;
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

                if (wfTarget != null && wfTarget.IndexOf("IAP_CheckReferenceinQueueItems.xaml", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    duplicateCheckFound = true;
                    string invokeDisplayName = XamlActivityHelper.GetDisplayName(el);

                    // Inspect arguments container
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

                            if (string.IsNullOrWhiteSpace(argName))
                                continue;

                            string normalizedArgName = argName.Replace(" ", "").ToLowerInvariant();
                            if (ExemptArgs.Contains(normalizedArgName))
                                continue;

                            string expr = GetArgumentExpression(argEl);
                            if (IsEmptyArgumentExpression(expr))
                            {
                                argIssues.Add($"The WQ Duplication invoke '{invokeDisplayName}' has empty {direction} argument: {argName}.");
                            }
                        }
                    }

                    break;
                }
            }

            if (!duplicateCheckFound)
            {
                results.Add(new RuleCheckResult
                {
                    RuleId = RuleId,
                    RuleName = RuleName,
                    Level = RuleLevel.Warning,
                    FilePath = filePath,
                    Message = "The WQ Duplication Check Workflow is not used in the process.",
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

            return normalized.Equals("\"\"", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Equals("{}", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Equals("Nothing", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Equals("null", StringComparison.OrdinalIgnoreCase);
        }
    }
}
