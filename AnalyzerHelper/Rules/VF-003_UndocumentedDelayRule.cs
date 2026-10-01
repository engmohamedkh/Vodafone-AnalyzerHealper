using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using AnalyzerHelper.Interfaces;
using AnalyzerHelper.Models;

namespace AnalyzerHelper.Rules
{
    /// <summary>VF-003 — flags Delay / DelayUntil activities (delete vs DelayBefore/After is judgment).</summary>
    public sealed class UndocumentedDelayRule : IAnalyzerRule
    {
        private static readonly Regex HardcodedTimeSpan = new(
            @":\d{2}|TimeSpan\.From|\d{2}:\d{2}:\d{2}",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public string RuleId => "VF-003";
        public string RuleName => "UndocumentedDelay";
        public string DefaultRecommendation => "Please use DelayBefore or DelayAfter instead, or remove unnecessary delays.";
        public bool RequiresUserInteraction => false;

        public IReadOnlyList<RuleCheckResult> Check(string filePath, string content)
        {
            var results = new List<RuleCheckResult>();
            if (!XamlActivityHelper.TryParse(content, out var doc) || doc == null) return results;

            foreach (var el in XamlActivityHelper.EnumerateActivities(doc))
            {
                string ln = XamlActivityHelper.GetLocalName(el);
                if (!ln.Equals("Delay", StringComparison.OrdinalIgnoreCase) &&
                    !ln.Equals("DelayUntil", StringComparison.OrdinalIgnoreCase))
                    continue;

                string display = XamlActivityHelper.GetDisplayName(el);
                var messages = new List<string>
                {
                    $"The following activity: {display} is a delay activity. Try to delete it if it's not necessary."
                };

                string duration = GetDurationExpression(el);
                if (!string.IsNullOrEmpty(duration) && HardcodedTimeSpan.IsMatch(duration))
                    messages.Add($"The following delay activity: {display} has hardcoded value.");

                results.Add(new RuleCheckResult
                {
                    RuleId = RuleId,
                    RuleName = RuleName,
                    Level = RuleLevel.Warning,
                    Message = string.Join(" ", messages),
                    FilePath = filePath,
                    Recommendation = DefaultRecommendation
                });
            }
            return results;
        }

        private static string GetDurationExpression(XElement el)
        {
            foreach (var attr in el.Attributes())
            {
                string n = attr.Name.LocalName;
                if (n.IndexOf("Duration", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("Date", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.Equals("Until", StringComparison.OrdinalIgnoreCase))
                    return attr.Value ?? "";
            }

            var durationChild = el.Elements().FirstOrDefault(e =>
                e.Name.LocalName.IndexOf("Duration", StringComparison.OrdinalIgnoreCase) >= 0 ||
                e.Name.LocalName.IndexOf("Until", StringComparison.OrdinalIgnoreCase) >= 0);
            if (durationChild == null) return "";
            var inArg = durationChild.Descendants().FirstOrDefault(e =>
                e.Name.LocalName == "InArgument" || e.Name.LocalName == "Literal");
            return inArg?.Value ?? durationChild.Value ?? "";
        }
    }
}
