using System;
using System.Collections.Generic;
using System.Linq;
using AnalyzerHelper.Interfaces;
using AnalyzerHelper.Models;

namespace AnalyzerHelper.Rules
{
    /// <summary>VF-036 — Microsoft Office activities (license awareness, not XAML fix).</summary>
    public sealed class MicrosoftOfficeActivitiesRule : IAnalyzerRule
    {
        private static readonly string[] OfficeTokens = { "Excel", "Word", "Database" };
        private const string ExcludedArgument = "WorkbookPath";
        private const string ExcelScope = "ExcelApplicationScope";

        public string RuleId => "VF-036";
        public string RuleName => "MicrosoftOfficeActivities";
        public string DefaultRecommendation => "Make sure the process has a Microsoft Office license where required.";
        public bool RequiresUserInteraction => false;

        public IReadOnlyList<RuleCheckResult> Check(string filePath, string content)
        {
            var results = new List<RuleCheckResult>();
            if (!XamlActivityHelper.TryParse(content, out var doc) || doc == null) return results;

            foreach (var el in XamlActivityHelper.EnumerateActivities(doc))
            {
                bool matchesOffice = OfficeTokens.Any(t =>
                    XamlActivityHelper.NameContains(el, t) ||
                    (XamlActivityHelper.GetTypeHint(el).IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0));
                if (!matchesOffice) continue;

                // Exclude workbook-path based activities unless ExcelApplicationScope
                bool isExcelScope = XamlActivityHelper.NameContains(el, ExcelScope);
                bool hasWorkbookPath =
                    el.Attributes().Any(a =>
                        a.Name.LocalName.IndexOf("WorkbookPath", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        (a.Name.LocalName.IndexOf("Workbook", StringComparison.OrdinalIgnoreCase) >= 0 &&
                         a.Name.LocalName.IndexOf("Path", StringComparison.OrdinalIgnoreCase) >= 0))
                    || el.Descendants().Any(d =>
                        d.Name.LocalName.IndexOf("WorkbookPath", StringComparison.OrdinalIgnoreCase) >= 0);

                // Studio logic: report if NO workbook path arg OR is ExcelApplicationScope
                if (hasWorkbookPath && !isExcelScope) continue;

                results.Add(new RuleCheckResult
                {
                    RuleId = RuleId,
                    RuleName = RuleName,
                    Level = RuleLevel.Warning,
                    Message = $"The following activity: {XamlActivityHelper.GetDisplayName(el)} requires Microsoft Office to be Installed.",
                    FilePath = filePath,
                    Recommendation = DefaultRecommendation
                });
            }
            return results;
        }
    }
}
