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
    /// VF-041 — Checks that BusinessRuleException is not thrown inside Automation workflows.
    /// Low-level automation components should use a flag or throw SystemException instead.
    /// </summary>
    public sealed class BEInsideAutomation : IAnalyzerRule
    {
        public string RuleId => "VF-041";
        public string RuleName => "BEInsideAutomation";
        public string DefaultRecommendation =>
            "Don't Throw BE inside Automation Workflows use A Flag and throw it in main Logic instead.";
        public bool RequiresUserInteraction => false;

        public IReadOnlyList<RuleCheckResult> Check(string filePath, string content)
        {
            var results = new List<RuleCheckResult>();

            if (string.IsNullOrWhiteSpace(content))
                return results;

            if (!filePath.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
                return results;

            if (!IsInAutomationFolder(filePath))
                return results;

            if (!XamlActivityHelper.TryParse(content, out var doc) || doc?.Root == null)
                return results;

            bool hasBusinessException = false;

            // 1. Check Throw activities anywhere in the document
            foreach (var el in doc.Root.Descendants())
            {
                string ln = el.Name.LocalName;
                if (ln.Equals("Throw", StringComparison.OrdinalIgnoreCase))
                {
                    string expr = GetExceptionExpression(el);
                    if (expr.IndexOf("businessruleexception", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        hasBusinessException = true;
                        break;
                    }
                }
            }

            // 2. Also check for instantiation of New BusinessRuleException outside Catch blocks
            if (!hasBusinessException)
            {
                foreach (var el in doc.Root.Descendants())
                {
                    if (IsInsideCatchBlock(el))
                        continue;

                    // Check attributes
                    foreach (var attr in el.Attributes())
                    {
                        if (attr.Value.IndexOf("new businessruleexception", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            hasBusinessException = true;
                            break;
                        }
                    }

                    if (hasBusinessException) break;

                    // Check text values
                    if (!el.HasElements && el.Value.IndexOf("new businessruleexception", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        hasBusinessException = true;
                        break;
                    }
                }
            }

            if (hasBusinessException)
            {
                string workflowDisplayName = Path.GetFileNameWithoutExtension(filePath);
                results.Add(new RuleCheckResult
                {
                    RuleId = RuleId,
                    RuleName = RuleName,
                    Level = RuleLevel.Warning,
                    FilePath = filePath,
                    Message = $"The following Automation Workflow: {workflowDisplayName} has Business Exception inside it.",
                    Recommendation = DefaultRecommendation,
                    RequiresUserInteraction = false
                });
            }

            return results;
        }

        private static bool IsInsideCatchBlock(XElement el)
        {
            var curr = el.Parent;
            while (curr != null)
            {
                string name = curr.Name.LocalName;
                if (name.Equals("Catch", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("TryCatch.Catches", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                curr = curr.Parent;
            }
            return false;
        }

        private static string GetExceptionExpression(XElement el)
        {
            var attr = el.Attributes().FirstOrDefault(a =>
                a.Name.LocalName.IndexOf("Exception", StringComparison.OrdinalIgnoreCase) >= 0);
            if (!string.IsNullOrWhiteSpace(attr?.Value)) return attr!.Value.Trim();

            var exChild = el.Elements().FirstOrDefault(e =>
                e.Name.LocalName.IndexOf("Exception", StringComparison.OrdinalIgnoreCase) >= 0);
            if (exChild == null) return "";
            var inArg = exChild.Descendants().FirstOrDefault(d =>
                d.Name.LocalName == "InArgument" || d.Name.LocalName.Contains("Literal"));
            return (inArg?.Value ?? exChild.Value ?? "").Trim();
        }

        private static bool IsInAutomationFolder(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return false;

            string normalized = filePath.Replace('\\', '/');
            var segments = normalized.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < segments.Length - 1; i++)
            {
                if (segments[i].IndexOf("automation", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
