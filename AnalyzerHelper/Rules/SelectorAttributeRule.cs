using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AnalyzerHelper.Interfaces;
using AnalyzerHelper.Models;

namespace AnalyzerHelper.Rules
{
    /// <summary>VF-060 — flags unstable selector attributes in XAML (no safe autofix).</summary>
    public sealed class SelectorAttributeRule : IAnalyzerRule
    {
        private static readonly string[] UnstableAttributes =
        {
            "ctrlid", "idx", "aaname",
            "sessionid", "session_id", "processid", "process_id",
            "dynamicid", "dynamic_id", "runtimeid", "runtime_id"
        };

        private const string Recommended = "automationId, name, role, class, title";

        private static readonly Regex SelectorDoubleQuoteRegex = new(
            @"Selector\s*=\s*""((?:[^""]|&quot;)*)""",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex SelectorSingleQuoteRegex = new(
            @"Selector\s*=\s*'((?:[^']|&apos;)*)'",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex AttributeNameRegex = new(
            @"\s+([a-zA-Z][a-zA-Z0-9_]*)\s*=",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public string RuleId => "VF-060";
        public string RuleName => "SelectorAttribute";
        public string DefaultRecommendation =>
            "Use stable selector attributes: " + Recommended + ". Avoid: ctrlid, idx, dynamic aaname, session-based IDs.";
        public bool RequiresUserInteraction => false;

        public IReadOnlyList<RuleCheckResult> Check(string filePath, string content)
        {
            var results = new List<RuleCheckResult>();
            if (string.IsNullOrWhiteSpace(content)) return results;

            foreach (var selector in EnumerateSelectors(content))
            {
                var unstable = FindUnstable(selector);
                if (unstable.Count == 0) continue;

                results.Add(new RuleCheckResult
                {
                    RuleId = RuleId,
                    RuleName = RuleName,
                    Level = RuleLevel.Error,
                    Message = $"Selector uses unstable attribute(s): {string.Join(", ", unstable)}. Use stable attributes instead: {Recommended}.",
                    FilePath = filePath,
                    Recommendation = DefaultRecommendation
                });
            }
            return results;
        }

        private static IEnumerable<string> EnumerateSelectors(string xaml)
        {
            foreach (Match m in SelectorDoubleQuoteRegex.Matches(xaml))
            {
                if (m.Success && m.Groups.Count >= 2 && !string.IsNullOrWhiteSpace(m.Groups[1].Value))
                    yield return m.Groups[1].Value;
            }
            foreach (Match m in SelectorSingleQuoteRegex.Matches(xaml))
            {
                if (m.Success && m.Groups.Count >= 2 && !string.IsNullOrWhiteSpace(m.Groups[1].Value))
                    yield return m.Groups[1].Value;
            }
        }

        private static List<string> FindUnstable(string selectorXml)
        {
            string decoded = selectorXml.Replace("&lt;", "<").Replace("&gt;", ">").Replace("&quot;", "\"");
            var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match m in AttributeNameRegex.Matches(decoded))
            {
                if (!m.Success || m.Groups.Count < 2) continue;
                string attr = m.Groups[1].Value.Trim();
                if (UnstableAttributes.Any(u => string.Equals(attr, u, StringComparison.OrdinalIgnoreCase)))
                    found.Add(attr);
                if (attr.IndexOf("session", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    attr.IndexOf("id", StringComparison.OrdinalIgnoreCase) >= 0)
                    found.Add(attr);
            }
            return found.ToList();
        }
    }
}
