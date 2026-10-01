using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using AnalyzerHelper.Interfaces;
using AnalyzerHelper.Models;

namespace AnalyzerHelper.Rules
{
    /// <summary>
    /// VF-014 — Checks all InvokeWorkflowFile activities to ensure every argument has an assigned value.
    /// Flags any empty or unassigned (e.g. {x:Null}) In, Out, or InOut arguments.
    /// </summary>
    public sealed class MissingInOutArgument : IAnalyzerRule
    {
        public string RuleId => "VF-014";
        public string RuleName => "MissingInOutArgument";
        public string DefaultRecommendation => "Kindly add default value for the mentioned argument.";
        public bool RequiresUserInteraction => false;

        private static readonly HashSet<string> IgnoredArgs = new(StringComparer.OrdinalIgnoreCase)
        {
            "WorkflowFileName", "ContinueOnError", "Timeout", "LogEntry", "LogExit",
            "ArgumentsVariable", "LogLevel", "Level"
        };

        public IReadOnlyList<RuleCheckResult> Check(string filePath, string content)
        {
            var results = new List<RuleCheckResult>();

            if (string.IsNullOrWhiteSpace(content))
                return results;

            if (!filePath.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
                return results;

            if (content.IndexOf("InvokeWorkflowFile", StringComparison.OrdinalIgnoreCase) < 0)
                return results;

            if (!XamlActivityHelper.TryParse(content, out var doc) || doc?.Root == null)
                return results;

            foreach (var el in doc.Root.Descendants())
            {
                if (el.Name.LocalName.IndexOf("InvokeWorkflowFile", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                if (IsInsideCommentOut(el))
                    continue;

                string activityDisplayName = XamlActivityHelper.GetDisplayName(el);

                // Find .Arguments element or direct argument children
                var argsContainer = el.Elements().FirstOrDefault(e =>
                    e.Name.LocalName.EndsWith(".Arguments", StringComparison.OrdinalIgnoreCase));
                var parent = argsContainer ?? el;

                foreach (var argEl in parent.Elements())
                {
                    string ln = argEl.Name.LocalName;
                    if (!ln.EndsWith("Argument", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string direction = "In";
                    if (ln.StartsWith("InOutArgument", StringComparison.OrdinalIgnoreCase))
                        direction = "InOut";
                    else if (ln.StartsWith("OutArgument", StringComparison.OrdinalIgnoreCase))
                        direction = "Out";
                    else if (ln.StartsWith("InArgument", StringComparison.OrdinalIgnoreCase))
                        direction = "In";

                    string key = argEl.Attributes().FirstOrDefault(a =>
                        a.Name.LocalName.Equals("Key", StringComparison.OrdinalIgnoreCase) ||
                        a.Name.LocalName.Equals("Name", StringComparison.OrdinalIgnoreCase))?.Value ?? "";

                    if (string.IsNullOrWhiteSpace(key))
                        continue;

                    if (IgnoredArgs.Contains(key))
                        continue;

                    if (IsEmptyArgument(argEl))
                    {
                        results.Add(new RuleCheckResult
                        {
                            RuleId = RuleId,
                            RuleName = RuleName,
                            Level = RuleLevel.Error,
                            FilePath = filePath,
                            Message = $"The following activity {activityDisplayName} has empty {direction} argument: {key}.",
                            Recommendation = DefaultRecommendation,
                            RequiresUserInteraction = false
                        });
                    }
                }
            }

            return results;
        }

        private static bool IsEmptyArgument(XElement argEl)
        {
            if (argEl.IsEmpty) return true;
            string val = argEl.Value?.Trim() ?? "";
            if (string.IsNullOrEmpty(val)) return true;
            if (string.Equals(val, "{x:Null}", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
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
