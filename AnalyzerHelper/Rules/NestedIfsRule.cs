using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using AnalyzerHelper.Interfaces;
using AnalyzerHelper.Models;

namespace AnalyzerHelper.Rules
{
    /// <summary>VF-047 — nested If / loop structures (refactor needs design).</summary>
    public sealed class NestedIfsRule : IAnalyzerRule
    {
        private static readonly string[] LoopNames =
            { "InterruptibleWhile", "InterruptibleDoWhile", "ForEach", "ForEachRow", "While", "DoWhile" };

        public string RuleId => "VF-047";
        public string RuleName => "Nested IFs";
        public string DefaultRecommendation => "Remove nested IFs/Loops from the logic where possible.";
        public bool RequiresUserInteraction => false;

        public IReadOnlyList<RuleCheckResult> Check(string filePath, string content)
        {
            var results = new List<RuleCheckResult>();
            if (!XamlActivityHelper.TryParse(content, out var doc) || doc == null) return results;

            var messages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var el in doc.Descendants().Where(e => !XamlActivityHelper.IsPropertyElement(e)))
            {
                string ln = XamlActivityHelper.GetLocalName(el);
                if (ln.Equals("If", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var nested in FindNested(el, "If"))
                    {
                        messages.Add(
                            $"The following If Activity: '{XamlActivityHelper.GetDisplayName(nested)}' is Nested to another bigger one '{XamlActivityHelper.GetDisplayName(el)}'. please Remove any Nested Ifs.");
                    }
                }
                else if (LoopNames.Any(l => ln.Equals(l, StringComparison.OrdinalIgnoreCase)))
                {
                    foreach (var nested in FindNestedLoops(el))
                    {
                        messages.Add(
                            $"The following Loop Activity: '{XamlActivityHelper.GetDisplayName(nested)}' is Nested to another bigger one '{XamlActivityHelper.GetDisplayName(el)}'. please Remove any Nested Loops.");
                    }
                }
            }

            foreach (var msg in messages)
            {
                results.Add(new RuleCheckResult
                {
                    RuleId = RuleId,
                    RuleName = RuleName,
                    Level = RuleLevel.Warning,
                    Message = msg,
                    FilePath = filePath,
                    Recommendation = DefaultRecommendation
                });
            }
            return results;
        }

        private static IEnumerable<XElement> FindNested(XElement parent, string activityName)
        {
            foreach (var child in parent.Elements())
            {
                // Skip direct property wrappers but search inside Then/Else/body
                if (XamlActivityHelper.IsPropertyElement(child))
                {
                    foreach (var found in FindNestedInSubtree(child, activityName, skipSelf: true))
                        yield return found;
                }
                else
                {
                    foreach (var found in FindNestedInSubtree(child, activityName, skipSelf: false))
                        yield return found;
                }
            }
        }

        private static IEnumerable<XElement> FindNestedInSubtree(XElement node, string activityName, bool skipSelf)
        {
            if (!skipSelf &&
                !XamlActivityHelper.IsPropertyElement(node) &&
                XamlActivityHelper.GetLocalName(node).Equals(activityName, StringComparison.OrdinalIgnoreCase))
            {
                yield return node;
                yield break; // Studio stops at first nested match per branch style; still walk siblings
            }

            foreach (var child in node.Elements())
            {
                foreach (var found in FindNestedInSubtree(child, activityName, skipSelf: false))
                    yield return found;
            }
        }

        private static IEnumerable<XElement> FindNestedLoops(XElement parent)
        {
            foreach (var child in parent.Elements())
            {
                foreach (var found in FindNestedLoopInSubtree(child))
                    yield return found;
            }
        }

        private static IEnumerable<XElement> FindNestedLoopInSubtree(XElement node)
        {
            if (!XamlActivityHelper.IsPropertyElement(node))
            {
                string ln = XamlActivityHelper.GetLocalName(node);
                if (LoopNames.Any(l => ln.Equals(l, StringComparison.OrdinalIgnoreCase)))
                {
                    yield return node;
                    yield break;
                }
            }

            foreach (var child in node.Elements())
            {
                foreach (var found in FindNestedLoopInSubtree(child))
                    yield return found;
            }
        }
    }
}
