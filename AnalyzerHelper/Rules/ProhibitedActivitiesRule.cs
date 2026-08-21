using System.Collections.Generic;
using AnalyzerHelper.Interfaces;
using AnalyzerHelper.Models;

namespace AnalyzerHelper.Rules
{
    /// <summary>VF-016 — flags prohibited activities (Exchange, Outlook, WriteLine, MessageBox, DeleteQueueItems).</summary>
    public sealed class ProhibitedActivitiesRule : IAnalyzerRule
    {
        private static readonly string[] Prohibited =
            { "Exchange", "Outlook", "WriteLine", "MessageBox", "DeleteQueueItems" };

        public string RuleId => "VF-016";
        public string RuleName => "ProhibitedActivities";
        public string DefaultRecommendation => "Use the allowed activities instead.";
        public bool RequiresUserInteraction => false;

        public IReadOnlyList<RuleCheckResult> Check(string filePath, string content)
        {
            var results = new List<RuleCheckResult>();
            if (!XamlActivityHelper.TryParse(content, out var doc) || doc == null) return results;

            foreach (var el in XamlActivityHelper.EnumerateActivities(doc))
            {
                foreach (var token in Prohibited)
                {
                    if (!XamlActivityHelper.NameContains(el, token)) continue;
                    results.Add(new RuleCheckResult
                    {
                        RuleId = RuleId,
                        RuleName = RuleName,
                        Level = RuleLevel.Error,
                        Message = $"The following activity: {XamlActivityHelper.GetDisplayName(el)} is existing in the prohibited activity list. Please don't use it.",
                        FilePath = filePath,
                        Recommendation = DefaultRecommendation
                    });
                    break;
                }
            }
            return results;
        }
    }
}
