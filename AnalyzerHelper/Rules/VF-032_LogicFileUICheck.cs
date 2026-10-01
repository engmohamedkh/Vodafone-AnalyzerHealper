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
    /// VF-032 — Checks that Logic and Subprocess workflows do not contain direct UI automation activities.
    /// Enforces separation of concerns by keeping UI automation strictly within Automation/ layer workflows.
    /// </summary>
    public sealed class LogicFileUICheck : IAnalyzerRule
    {
        private static readonly Dictionary<string, string> UiActivityTagToDefaultName = new(StringComparer.OrdinalIgnoreCase)
        {
            { "Click", "Click" },
            { "GetValue", "Get Text" },
            { "GetText", "Get Text" },
            { "TypeInto", "Type Into" },
            { "Activate", "Activate" },
            { "UiElementExists", "Element Exists" },
            { "ElementExists", "Element Exists" },
            { "BrowserScope", "Attach Browser" },
            { "AttachBrowser", "Attach Browser" },
            { "OpenBrowser", "Open Browser" },
            { "WindowScope", "Attach Window" },
            { "AttachWindow", "Attach Window" },
            { "OpenApplication", "Open Application" }
        };

        public string RuleId => "VF-032";
        public string RuleName => "LogicFileUICheck";
        public string DefaultRecommendation => "Remove UI activities (Click, Get Text, Type Into, Activate, Element Exists, Attach Browser, Attach Window) from Logic and Subprocess workflows.";
        public bool RequiresUserInteraction => false;

        public IReadOnlyList<RuleCheckResult> Check(string filePath, string content)
        {
            var results = new List<RuleCheckResult>();

            // Only inspect files under Logic or Subprocess directories
            if (!IsInLogicOrSubprocessFolder(filePath))
            {
                return results;
            }

            if (!XamlActivityHelper.TryParse(content, out var doc) || doc?.Root == null)
            {
                return results;
            }

            string workflowDisplayName = Path.GetFileNameWithoutExtension(filePath);

            foreach (var el in doc.Root.Descendants())
            {
                if (el == doc.Root) continue;

                string localName = el.Name.LocalName;

                // Skip property wrapper elements (e.g. Activate.Target, BrowserScope.Body)
                if (localName.Contains('.')) continue;

                if (UiActivityTagToDefaultName.TryGetValue(localName, out var defaultName))
                {
                    string? displayNameAttr = el.Attributes().FirstOrDefault(a =>
                        string.Equals(a.Name.LocalName, "DisplayName", StringComparison.OrdinalIgnoreCase))?.Value;

                    string activityName = !string.IsNullOrWhiteSpace(displayNameAttr)
                        ? displayNameAttr.Trim()
                        : defaultName;

                    results.Add(new RuleCheckResult
                    {
                        RuleId = RuleId,
                        RuleName = RuleName,
                        Level = RuleLevel.Error,
                        Message = $"The following logic workflow: {workflowDisplayName} has a UI activity {activityName}.",
                        FilePath = filePath,
                        Recommendation = DefaultRecommendation
                    });
                }
            }

            return results;
        }

        /// <summary>
        /// True if filePath lies under a directory named like "Logic" or "Subprocess".
        /// </summary>
        private static bool IsInLogicOrSubprocessFolder(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return false;

            string normalized = filePath.Replace('\\', '/');
            var segments = normalized.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            // Check parent directory segments (excluding the file name itself)
            for (int i = 0; i < segments.Length - 1; i++)
            {
                string seg = segments[i];
                if (seg.IndexOf("logic", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    seg.IndexOf("subprocess", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
