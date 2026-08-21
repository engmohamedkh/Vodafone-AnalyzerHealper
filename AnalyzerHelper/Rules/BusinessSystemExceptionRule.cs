using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using AnalyzerHelper.Interfaces;
using AnalyzerHelper.Models;

namespace AnalyzerHelper.Rules
{
    /// <summary>VF-015 — inventory of BusinessRuleException / SystemException throws (report only).</summary>
    public sealed class BusinessSystemExceptionRule : IAnalyzerRule
    {
        public string RuleId => "VF-015";
        public string RuleName => "BusinessSystemException";
        public string DefaultRecommendation => "Inventory of System Exceptions and Business Exceptions in the workflow.";
        public bool RequiresUserInteraction => false;

        public IReadOnlyList<RuleCheckResult> Check(string filePath, string content)
        {
            var results = new List<RuleCheckResult>();
            if (!XamlActivityHelper.TryParse(content, out var doc) || doc == null) return results;

            foreach (var el in XamlActivityHelper.EnumerateActivities(doc))
            {
                string ln = XamlActivityHelper.GetLocalName(el);
                if (!ln.Equals("Throw", StringComparison.OrdinalIgnoreCase) &&
                    !XamlActivityHelper.NameContains(el, "Throw"))
                    continue;
                if (XamlActivityHelper.NameContains(el, "Rethrow") ||
                    ln.Equals("Rethrow", StringComparison.OrdinalIgnoreCase))
                    continue;

                string expr = GetExceptionExpression(el);
                if (string.IsNullOrWhiteSpace(expr)) continue;

                string lower = expr.ToLowerInvariant();
                if (lower.Contains("businessruleexception"))
                {
                    results.Add(new RuleCheckResult
                    {
                        RuleId = RuleId,
                        RuleName = RuleName,
                        Level = RuleLevel.Info,
                        Message = $"Business exception found: {expr}.",
                        FilePath = filePath,
                        Recommendation = DefaultRecommendation
                    });
                }
                else if (lower.Contains("systemexception"))
                {
                    results.Add(new RuleCheckResult
                    {
                        RuleId = RuleId,
                        RuleName = RuleName,
                        Level = RuleLevel.Info,
                        Message = $"System exception found: {expr}.",
                        FilePath = filePath,
                        Recommendation = DefaultRecommendation
                    });
                }
            }
            return results;
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
    }
}
