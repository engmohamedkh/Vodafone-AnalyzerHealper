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
    /// VF-042 — Ensures that workflows in the Automation folder interact with only one target application.
    /// Low-level automation workflows should not deal with multiple applications or multiple screens.
    /// </summary>
    public sealed class MultipleAppsSequence : IAnalyzerRule
    {
        public string RuleId => "VF-042";
        public string RuleName => "Multiple Apps Sequence";
        public string DefaultRecommendation =>
            "Please consider splitting the sequence so that every sequence deals with only 1 Application or 1 screen.";
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

            var targetApps = new List<string>();

            foreach (var el in doc.Root.Descendants())
            {
                if (XamlActivityHelper.IsStructural(el))
                    continue;

                if (IsInsideCommentOut(el))
                    continue;

                string? appType = GetTargetAppType(el);
                if (appType != null)
                {
                    targetApps.Add(appType);
                }
            }

            if (targetApps.Count > 1)
            {
                var distinctTargetApps = targetApps.Distinct().ToList();

                // Exceptional cases: Excel / Range activities (excluding PDF)
                bool isExcelExempt = distinctTargetApps.All(x =>
                    x.IndexOf("excel", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (x.IndexOf("range", StringComparison.OrdinalIgnoreCase) >= 0 &&
                     x.IndexOf("pdf", StringComparison.OrdinalIgnoreCase) < 0));

                if (!isExcelExempt)
                {
                    string workflowDisplayName = Path.GetFileNameWithoutExtension(filePath);
                    string message;

                    if (distinctTargetApps.Count > 1)
                    {
                        message = $"The following Workflow: '{workflowDisplayName}' Deals with {targetApps.Count} Target Applications. Please consider splitting to 1 sequence for each of the following: '{string.Join("/", distinctTargetApps)}'";
                    }
                    else
                    {
                        message = $"The following Workflow: '{workflowDisplayName}' has '{targetApps.Count} {distinctTargetApps[0]} activities'. Please consider splitting it.";
                    }

                    results.Add(new RuleCheckResult
                    {
                        RuleId = RuleId,
                        RuleName = RuleName,
                        Level = RuleLevel.Error,
                        FilePath = filePath,
                        Message = message,
                        Recommendation = DefaultRecommendation,
                        RequiresUserInteraction = false
                    });
                }
            }

            return results;
        }

        private static string? GetTargetAppType(XElement el)
        {
            string ln = el.Name.LocalName;

            // Browser
            if (ln.Equals("BrowserScope", StringComparison.OrdinalIgnoreCase) ||
                ln.Equals("OpenBrowser", StringComparison.OrdinalIgnoreCase) ||
                ln.Equals("AttachBrowser", StringComparison.OrdinalIgnoreCase))
            {
                return "BrowserScope";
            }

            // Window
            if (ln.Equals("WindowScope", StringComparison.OrdinalIgnoreCase) ||
                ln.Equals("OpenApplication", StringComparison.OrdinalIgnoreCase) ||
                ln.Equals("AttachWindow", StringComparison.OrdinalIgnoreCase))
            {
                return "WindowScope";
            }

            // Excel Scope
            if (ln.Equals("ExcelApplicationScope", StringComparison.OrdinalIgnoreCase) ||
                ln.Equals("ExcelProcessScopeX", StringComparison.OrdinalIgnoreCase) ||
                ln.Equals("UseExcelFile", StringComparison.OrdinalIgnoreCase))
            {
                return "ExcelApplicationScope";
            }

            // Exchange
            if (ln.IndexOf("ExchangeScope", StringComparison.OrdinalIgnoreCase) >= 0 ||
                ln.IndexOf("ExchangeApplicationScope", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "ExchangeScope";
            }

            // SharePoint
            if (ln.IndexOf("SharePoint", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "SharePoint";
            }

            // S3
            if (ln.IndexOf("S3", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "S3";
            }

            // Range (Excel operations)
            if (ln.IndexOf("Range", StringComparison.OrdinalIgnoreCase) >= 0 &&
                ln.IndexOf("PDF", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return "Range";
            }

            // PDF
            if (ln.IndexOf("PDF", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "PDF";
            }

            return null;
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
