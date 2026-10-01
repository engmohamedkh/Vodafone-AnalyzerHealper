using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using AnalyzerHelper.Interfaces;
using AnalyzerHelper.Models;

namespace AnalyzerHelper.Rules
{
    /// <summary>
    /// VF-048 — Verifies that Orchestrator Queue activities are enclosed in a RetryScope.
    /// Queue activities (AddQueueItem, AddTransactionItem, BulkAddQueueItems, DeleteQueueItems,
    /// GetQueueItems, GetQueueItem, PostponeTransactionItem, SetTransactionProgress, SetTransactionStatus)
    /// must be wrapped in a RetryScope to handle transient network or service failures.
    /// </summary>
    public sealed class OrechestratorActivitesRetry : IAnalyzerRule
    {
        private static readonly HashSet<string> QueueActivities = new(StringComparer.OrdinalIgnoreCase)
        {
            "AddQueueItem",
            "AddTransactionItem",
            "BulkAddQueueItems",
            "DeleteQueueItems",
            "GetQueueItems",
            "GetQueueItem",
            "PostponeTransactionItem",
            "SetTransactionProgress",
            "SetTransactionStatus"
        };

        public string RuleId => "VF-048";
        public string RuleName => "OrechestratorActivitesRetry";
        public string DefaultRecommendation => "Make sure to include the queue activity in a retry scope.";
        public bool RequiresUserInteraction => false;

        public IReadOnlyList<RuleCheckResult> Check(string filePath, string content)
        {
            var results = new List<RuleCheckResult>();

            if (string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(content))
                return results;

            if (!filePath.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
                return results;

            if (!XamlActivityHelper.TryParse(content, out var doc) || doc?.Root == null)
                return results;

            foreach (var el in doc.Descendants())
            {
                // Skip property wrapper elements (e.g. Activity.Body, RetryScope.Condition)
                if (XamlActivityHelper.IsPropertyElement(el))
                    continue;

                string localName = el.Name.LocalName;
                if (!QueueActivities.Contains(localName))
                    continue;

                // Check if any ancestor is a RetryScope
                bool inRetryScope = el.Ancestors().Any(a =>
                    a.Name.LocalName.IndexOf("RetryScope", StringComparison.OrdinalIgnoreCase) >= 0);

                if (!inRetryScope)
                {
                    string displayName = XamlActivityHelper.GetDisplayName(el);
                    results.Add(new RuleCheckResult
                    {
                        RuleId = RuleId,
                        RuleName = RuleName,
                        Level = RuleLevel.Warning,
                        FilePath = filePath,
                        Message = $"The following activity: {displayName} is not included in Retry Scope.",
                        Recommendation = DefaultRecommendation,
                        RequiresUserInteraction = false
                    });
                }
            }

            return results;
        }
    }
}
