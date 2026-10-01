using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using AnalyzerHelper.Interfaces;
using AnalyzerHelper.Models;

namespace AnalyzerHelper.Rules
{
    /// <summary>
    /// VF-044 — Detects hardcoded numeric timeout values (TimeoutMS) in UI activities,
    /// enforcing the use of Config variables or arguments.
    /// </summary>
    public sealed class HardCodedTimeOut : IAnalyzerRule
    {
        private static readonly Regex DigitsOnly = new(@"^\d+$", RegexOptions.Compiled);

        public string RuleId => "VF-044";
        public string RuleName => "HardCoded TimeOut";
        public string DefaultRecommendation => "Don't use hardcoded numeric values for TimeoutMS. Move values to Config or variables.";
        public bool RequiresUserInteraction => false;

        public IReadOnlyList<RuleCheckResult> Check(string filePath, string content)
        {
            var results = new List<RuleCheckResult>();

            if (string.IsNullOrWhiteSpace(content))
                return results;

            if (!filePath.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
                return results;

            if (!XamlActivityHelper.TryParse(content, out var doc) || doc?.Root == null)
                return results;

            string workflowDisplayName = Path.GetFileNameWithoutExtension(filePath);
            var flaggedActivities = new HashSet<XElement>();

            foreach (var el in XamlActivityHelper.EnumerateActivities(doc))
            {
                if (IsInsideCommentOut(el))
                    continue;

                XElement owner = GetEnclosingActivity(el);
                if (flaggedActivities.Contains(owner))
                    continue;

                string display = GetActivityDisplayName(el);
                bool flagged = false;

                // 1. Check attributes (e.g. TimeoutMS="30000")
                foreach (var attr in el.Attributes())
                {
                    if (attr.Name.LocalName.IndexOf("TimeoutMS", StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    if (string.IsNullOrWhiteSpace(attr.Value))
                        continue;

                    if (DigitsOnly.IsMatch(attr.Value.Trim()))
                    {
                        results.Add(new RuleCheckResult
                        {
                            RuleId = RuleId,
                            RuleName = RuleName,
                            Level = RuleLevel.Warning,
                            FilePath = filePath,
                            Message = $"The activity '{display}' in '{workflowDisplayName}' has hardcoded TimeoutMS value '{attr.Value}'.",
                            Recommendation = DefaultRecommendation,
                            RequiresUserInteraction = false
                        });
                        flaggedActivities.Add(owner);
                        flagged = true;
                        break;
                    }
                }

                if (flagged) continue;

                // 2. Check child property elements (e.g. <ui:Click.TimeoutMS><InArgument ...>30000</InArgument></ui:Click.TimeoutMS>)
                foreach (var child in el.Elements().Where(e =>
                    e.Name.LocalName.IndexOf("TimeoutMS", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    string val = child.Descendants()
                        .FirstOrDefault(d => d.Name.LocalName == "InArgument" || d.Name.LocalName == "Literal")?.Value?.Trim()
                        ?? child.Value?.Trim()
                        ?? "";

                    if (!string.IsNullOrWhiteSpace(val) && DigitsOnly.IsMatch(val))
                    {
                        results.Add(new RuleCheckResult
                        {
                            RuleId = RuleId,
                            RuleName = RuleName,
                            Level = RuleLevel.Warning,
                            FilePath = filePath,
                            Message = $"The activity '{display}' in '{workflowDisplayName}' has hardcoded TimeoutMS value '{val}'.",
                            Recommendation = DefaultRecommendation,
                            RequiresUserInteraction = false
                        });
                        flaggedActivities.Add(owner);
                        break;
                    }
                }
            }

            return results;
        }

        private static string GetActivityDisplayName(XElement el)
        {
            var activityElement = GetEnclosingActivity(el);
            string display = XamlActivityHelper.GetDisplayName(activityElement);

            // Fallback safety: If display is still "Target", walk up past property elements to find the real activity
            if (string.Equals(display, "Target", StringComparison.OrdinalIgnoreCase) && activityElement.Parent != null)
            {
                var parent = activityElement.Parent;
                while (parent != null && (parent.Name.LocalName.Equals("Target", StringComparison.OrdinalIgnoreCase) || parent.Name.LocalName.Contains('.')))
                {
                    parent = parent.Parent;
                }
                if (parent != null)
                {
                    display = XamlActivityHelper.GetDisplayName(parent);
                }
            }

            return display;
        }

        private static XElement GetEnclosingActivity(XElement el)
        {
            var curr = el;
            while (curr != null)
            {
                string ln = curr.Name.LocalName;

                // If curr is Target or a property wrapper (contains '.'), move up to parent activity
                if (ln.Equals("Target", StringComparison.OrdinalIgnoreCase) ||
                    ln.Contains('.'))
                {
                    if (curr.Parent != null)
                    {
                        curr = curr.Parent;
                        continue;
                    }
                }

                return curr;
            }

            return el;
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
    }
}
