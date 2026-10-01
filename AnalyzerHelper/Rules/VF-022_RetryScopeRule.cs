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
    /// VF-022 — Checks that workflows in the Automation folder use a TryCatch wrapping a RetryScope with a valid condition.
    /// </summary>
    public sealed class RetryScopeRule : IAnalyzerRule
    {
        public string RuleId => "VF-022";
        public string RuleName => "RetryScope";
        public string DefaultRecommendation => "Please add retry scope activity and its condition.";
        public bool RequiresUserInteraction => false;

        public IReadOnlyList<RuleCheckResult> Check(string filePath, string content)
        {
            var results = new List<RuleCheckResult>();

            if (string.IsNullOrWhiteSpace(content))
                return results;

            if (!filePath.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
                return results;

            string workflowDisplayName = Path.GetFileNameWithoutExtension(filePath);

            // Exclude Main & Process
            if (workflowDisplayName.Equals("Main", StringComparison.OrdinalIgnoreCase) ||
                workflowDisplayName.Equals("Process", StringComparison.OrdinalIgnoreCase))
            {
                return results;
            }

            // Only apply to workflows in Automation folder
            if (!IsInAutomationFolder(filePath))
            {
                return results;
            }

            if (!XamlActivityHelper.TryParse(content, out var doc) || doc?.Root == null)
            {
                results.Add(CreateResult(filePath, $"The following workflow: {workflowDisplayName} is empty."));
                return results;
            }

            var rootActivity = doc.Root.Elements().FirstOrDefault(e =>
                !XamlActivityHelper.IsPropertyElement(e) &&
                !e.Name.LocalName.StartsWith("WorkflowViewState", StringComparison.OrdinalIgnoreCase) &&
                !e.Name.LocalName.StartsWith("ViewStateService", StringComparison.OrdinalIgnoreCase) &&
                !e.Name.LocalName.Equals("Members", StringComparison.OrdinalIgnoreCase) &&
                !e.Name.LocalName.Equals("VisualBasic.Settings", StringComparison.OrdinalIgnoreCase) &&
                !e.Name.LocalName.Equals("TextExpression.NamespacesForImplementation", StringComparison.OrdinalIgnoreCase) &&
                !e.Name.LocalName.Equals("TextExpression.ReferencesForImplementation", StringComparison.OrdinalIgnoreCase));

            if (rootActivity == null)
            {
                results.Add(CreateResult(filePath, $"The following workflow: {workflowDisplayName} is empty."));
                return results;
            }

            if (!rootActivity.Name.LocalName.Equals("Sequence", StringComparison.OrdinalIgnoreCase) &&
                !rootActivity.Name.LocalName.Equals("Flowchart", StringComparison.OrdinalIgnoreCase))
            {
                return results;
            }

            var messageList = new List<string>();

            // Look for TryCatch in root or its descendants
            var tryCatch = rootActivity.DescendantsAndSelf()
                .FirstOrDefault(e => e.Name.LocalName.Equals("TryCatch", StringComparison.OrdinalIgnoreCase) && !IsInsideCommentOut(e));

            if (tryCatch == null)
            {
                messageList.Add($"The following workflow: {workflowDisplayName} doesn't have try catch activity.");
            }
            else
            {
                // Inside TryCatch (usually in Try block), look for RetryScope
                var retryScope = tryCatch.Descendants()
                    .FirstOrDefault(e => e.Name.LocalName.IndexOf("RetryScope", StringComparison.OrdinalIgnoreCase) >= 0 && !IsInsideCommentOut(e));

                if (retryScope == null)
                {
                    messageList.Add($"The following workflow: {workflowDisplayName} doesn't have retry scope activity.");
                }
                else
                {
                    // Check condition of RetryScope
                    var conditionProp = retryScope.Elements().FirstOrDefault(e =>
                        e.Name.LocalName.EndsWith(".Condition", StringComparison.OrdinalIgnoreCase));

                    bool hasConditionActivity = false;

                    if (conditionProp != null)
                    {
                        var conditionActivity = conditionProp.Descendants()
                            .FirstOrDefault(e => !XamlActivityHelper.IsPropertyElement(e) &&
                                                 !e.Name.LocalName.Equals("ActivityFunc", StringComparison.OrdinalIgnoreCase));

                        if (conditionActivity != null)
                        {
                            hasConditionActivity = true;
                        }
                    }

                    if (!hasConditionActivity)
                    {
                        messageList.Add($"The following workflow: {workflowDisplayName} has retry scope activity but doesn't have condition activity.");
                    }
                }

                // Check if TryCatch contains another TryCatch in the Catch block
                var catchesProp = tryCatch.Elements().FirstOrDefault(e =>
                    e.Name.LocalName.EndsWith(".Catches", StringComparison.OrdinalIgnoreCase));

                if (catchesProp != null)
                {
                    var innerTryCatch = catchesProp.Descendants()
                        .FirstOrDefault(e => e.Name.LocalName.Equals("TryCatch", StringComparison.OrdinalIgnoreCase) && !IsInsideCommentOut(e));

                    if (innerTryCatch != null)
                    {
                        messageList.Add($"The following workflow: {workflowDisplayName} has a TryCatch activity but the Catch block does not contain another TryCatch activity.");
                    }
                }
            }

            foreach (var msg in messageList)
            {
                results.Add(CreateResult(filePath, msg));
            }

            return results;
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

        private RuleCheckResult CreateResult(string filePath, string message) =>
            new RuleCheckResult
            {
                RuleId = RuleId,
                RuleName = RuleName,
                Level = RuleLevel.Warning,
                FilePath = filePath,
                Message = message,
                Recommendation = DefaultRecommendation,
                RequiresUserInteraction = false
            };
    }
}
