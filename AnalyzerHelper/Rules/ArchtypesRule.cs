using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using AnalyzerHelper.Interfaces;
using AnalyzerHelper.Models;

namespace AnalyzerHelper.Rules
{
    /// <summary>VF-025 — inventory of archetype / analytics_log activities (report only).</summary>
    public sealed class ArchtypesRule : IAnalyzerRule
    {
        public string RuleId => "VF-025";
        public string RuleName => "Archtypes";
        public string DefaultRecommendation => "List of all archetype logs used in the project/workflow.";
        public bool RequiresUserInteraction => false;

        public IReadOnlyList<RuleCheckResult> Check(string filePath, string content)
        {
            var results = new List<RuleCheckResult>();
            if (!XamlActivityHelper.TryParse(content, out var doc) || doc == null) return results;

            foreach (var el in XamlActivityHelper.EnumerateActivities(doc))
            {
                if (!XamlActivityHelper.NameContains(el, "analytics_log") &&
                    !XamlActivityHelper.NameContains(el, "Analytics_Log") &&
                    !XamlActivityHelper.NameContains(el, "AnalyticsLog"))
                    continue;

                string archetype = GetArchetypeExpression(el);
                string display = XamlActivityHelper.GetDisplayName(el);
                results.Add(new RuleCheckResult
                {
                    RuleId = RuleId,
                    RuleName = RuleName,
                    Level = RuleLevel.Info,
                    Message = string.IsNullOrWhiteSpace(archetype)
                        ? $"The following activity {display} is an archetype/analytics log."
                        : $"The following activity {display} has archtypes code: {archetype}",
                    FilePath = filePath,
                    Recommendation = DefaultRecommendation
                });
            }
            return results;
        }

        private static string GetArchetypeExpression(XElement el)
        {
            var attr = el.Attributes().FirstOrDefault(a =>
                a.Name.LocalName.IndexOf("archetype", StringComparison.OrdinalIgnoreCase) >= 0);
            if (!string.IsNullOrWhiteSpace(attr?.Value)) return attr!.Value.Trim();

            var child = el.Descendants().FirstOrDefault(e =>
                e.Name.LocalName.IndexOf("archetype", StringComparison.OrdinalIgnoreCase) >= 0);
            return (child?.Value ?? "").Trim();
        }
    }
}
