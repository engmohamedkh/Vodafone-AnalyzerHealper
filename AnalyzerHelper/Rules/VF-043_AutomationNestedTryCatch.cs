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
    /// VF-043 — Checks if there are any nested TryCatch activities within Automation workflows.
    /// In the automation layer, workflows should have a single top-level TryCatch or propagate errors to Logic.
    /// </summary>
    public sealed class AutomationNestedTryCatch : IAnalyzerRule
    {
        public string RuleId => "VF-043";
        public string RuleName => "Nested TryCatch";
        public string DefaultRecommendation => "Remove any Nested TryCatches from the Logic.";
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

            bool hasNestedTryCatch = false;

            foreach (var el in doc.Root.Descendants())
            {
                if (!el.Name.LocalName.Equals("TryCatch", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (IsInsideCommentOut(el))
                    continue;

                // Check if any ancestor is also a TryCatch outside CommentOut
                if (el.Ancestors().Any(a => a.Name.LocalName.Equals("TryCatch", StringComparison.OrdinalIgnoreCase) && !IsInsideCommentOut(a)))
                {
                    hasNestedTryCatch = true;
                    break;
                }
            }

            if (hasNestedTryCatch)
            {
                string workflowDisplayName = Path.GetFileNameWithoutExtension(filePath);
                results.Add(new RuleCheckResult
                {
                    RuleId = RuleId,
                    RuleName = RuleName,
                    Level = RuleLevel.Warning,
                    FilePath = filePath,
                    Message = $"The following Workflow: '{workflowDisplayName}' has Nested Try/Catch Activities.",
                    Recommendation = DefaultRecommendation,
                    RequiresUserInteraction = false
                });
            }

            return results;
        }

        private static bool IsInsideCommentOut(XElement el)
        {
            var curr = el.Parent;
            while (curr != null)
            {
                if (curr.Name.LocalName.Equals("CommentOut", StringComparison.OrdinalIgnoreCase))
                    return true;
                curr = curr.Parent;
            }
            return false;
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
