using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AnalyzerHelper.Interfaces;
using AnalyzerHelper.Models;

namespace AnalyzerHelper.Rules
{
    /// <summary>VF-045 — hard-coded DelayBefore/DelayAfter numeric values (needs Config/variable design).</summary>
    public sealed class HardCodedDelaysRule : IAnalyzerRule
    {
        private static readonly Regex DigitsOnly = new(@"^\d+$", RegexOptions.Compiled);

        public string RuleId => "VF-045";
        public string RuleName => "HardCodedDelays";
        public string DefaultRecommendation => "Do not use hardcoded delays; move values to Config or variables.";
        public bool RequiresUserInteraction => false;

        public IReadOnlyList<RuleCheckResult> Check(string filePath, string content)
        {
            var results = new List<RuleCheckResult>();
            if (!XamlActivityHelper.TryParse(content, out var doc) || doc == null) return results;

            foreach (var el in XamlActivityHelper.EnumerateActivities(doc))
            {
                string display = XamlActivityHelper.GetDisplayName(el);
                foreach (var attr in el.Attributes())
                {
                    if (attr.Name.LocalName.IndexOf("Delay", StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    if (string.IsNullOrWhiteSpace(attr.Value)) continue;
                    if (!DigitsOnly.IsMatch(attr.Value.Trim())) continue;

                    results.Add(new RuleCheckResult
                    {
                        RuleId = RuleId,
                        RuleName = RuleName,
                        Level = RuleLevel.Warning,
                        Message = $"The following activity {display} has hard coded {attr.Name.LocalName} value {attr.Value}.",
                        FilePath = filePath,
                        Recommendation = DefaultRecommendation
                    });
                }

                // Child property elements like ui:Click.DelayBefore
                foreach (var child in el.Elements().Where(e =>
                    e.Name.LocalName.IndexOf("Delay", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    string val = child.Value?.Trim()
                        ?? child.Descendants().FirstOrDefault(d => d.Name.LocalName == "InArgument" || d.Name.LocalName == "Literal")?.Value
                        ?? "";
                    if (string.IsNullOrWhiteSpace(val) || !DigitsOnly.IsMatch(val.Trim())) continue;

                    results.Add(new RuleCheckResult
                    {
                        RuleId = RuleId,
                        RuleName = RuleName,
                        Level = RuleLevel.Warning,
                        Message = $"The following activity {display} has hard coded {child.Name.LocalName} value {val}.",
                        FilePath = filePath,
                        Recommendation = DefaultRecommendation
                    });
                }
            }
            return results;
        }
    }
}
