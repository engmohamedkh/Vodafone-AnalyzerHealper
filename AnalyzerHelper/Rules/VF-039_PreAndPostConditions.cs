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
    /// VF-039 — Checking Pre and Post Conditions in Automation workflows,
    /// and Pre Condition only in Read workflows.
    /// Requires Pre Condition (and Post Condition for non-Read) sequences containing
    /// at least one Dynamic Wait activity and one If activity.
    /// </summary>
    public sealed class PreAndPostConditions : IAnalyzerRule
    {
        private static readonly HashSet<string> DynamicWaitList = new(StringComparer.OrdinalIgnoreCase)
        {
            "UiElementExists", "FindChildren", "WaitUiElementAppear", "FindRelative",
            "WaitImageAppear", "FindImageMatches", "ImageFound", "OCRTextExists",
            "TextExists", "ReadStatusbar", "GetValue", "PathExists", "GetFullText",
            "GetVisibleText", "ExtractData", "GetOCRText", "OnImageAppear",
            "OnImageVanish", "WaitImageVanish", "OnUiElementAppear", "OnUiElementVanish",
            "AnchorBase", "AnchorContextAware", "GetAncestor", "WaitUiElementVanish",
            "WaitAttribute", "GetAttribute"
        };

        private static readonly string[] PreConditionKeywords = { "pre condition", "precondition" };
        private static readonly string[] PostConditionKeywords = { "post condition", "postcondition" };

        public string RuleId => "VF-039";
        public string RuleName => "PreAndPostConditions";
        public string DefaultRecommendation => "Checking Pre and Post Conditions in the automation Workflows and pre condition only in read workflows .";
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

            string workflowDisplayName = Path.GetFileNameWithoutExtension(filePath);
            bool isReadWorkflow = IsReadWorkflow(filePath);

            // Scope searching to RetryScope if present, otherwise whole document
            var retryScope = doc.Root.Descendants()
                .FirstOrDefault(e => e.Name.LocalName.Equals("RetryScope", StringComparison.OrdinalIgnoreCase));

            var searchContainer = retryScope ?? doc.Root;

            var sequences = searchContainer.Descendants()
                .Where(e => e.Name.LocalName.Equals("Sequence", StringComparison.OrdinalIgnoreCase));

            XElement? preConditionSeq = null;
            XElement? postConditionSeq = null;

            foreach (var seq in sequences)
            {
                string? displayName = seq.Attributes()
                    .FirstOrDefault(a => string.Equals(a.Name.LocalName, "DisplayName", StringComparison.OrdinalIgnoreCase))
                    ?.Value;

                if (string.IsNullOrWhiteSpace(displayName))
                    continue;

                if (preConditionSeq == null && MatchesKeywords(displayName, PreConditionKeywords))
                {
                    preConditionSeq = seq;
                }
                else if (postConditionSeq == null && MatchesKeywords(displayName, PostConditionKeywords))
                {
                    postConditionSeq = seq;
                }
            }

            int preDynamicWaitCount = preConditionSeq != null ? CountDynamicWaits(preConditionSeq) : 0;
            int preIfCount = preConditionSeq != null ? CountIfs(preConditionSeq) : 0;
            bool preConditionValid = preConditionSeq != null && preDynamicWaitCount >= 1 && preIfCount >= 1;

            if (isReadWorkflow)
            {
                if (!preConditionValid)
                {
                    results.Add(new RuleCheckResult
                    {
                        RuleId = RuleId,
                        RuleName = RuleName,
                        Level = RuleLevel.Warning,
                        FilePath = filePath,
                        Message = $"The following Workflow: {workflowDisplayName} does not have pre condition.",
                        Recommendation = DefaultRecommendation,
                        RequiresUserInteraction = false
                    });
                }
            }
            else
            {
                int postDynamicWaitCount = postConditionSeq != null ? CountDynamicWaits(postConditionSeq) : 0;
                int postIfCount = postConditionSeq != null ? CountIfs(postConditionSeq) : 0;
                bool postConditionValid = postConditionSeq != null && postDynamicWaitCount >= 1 && postIfCount >= 1;

                if (!preConditionValid || !postConditionValid)
                {
                    results.Add(new RuleCheckResult
                    {
                        RuleId = RuleId,
                        RuleName = RuleName,
                        Level = RuleLevel.Warning,
                        FilePath = filePath,
                        Message = $"The following Workflow: {workflowDisplayName} does not have pre or post condition.",
                        Recommendation = DefaultRecommendation,
                        RequiresUserInteraction = false
                    });
                }
            }

            return results;
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

        private static bool IsReadWorkflow(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return false;

            string normalized = filePath.Replace('\\', '/');
            var segments = normalized.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < segments.Length - 1; i++)
            {
                if (segments[i].IndexOf("read", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool MatchesKeywords(string text, string[] keywords)
        {
            foreach (var kw in keywords)
            {
                if (text.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        private static int CountDynamicWaits(XElement container)
        {
            return container.Descendants()
                .Count(e => DynamicWaitList.Contains(e.Name.LocalName));
        }

        private static int CountIfs(XElement container)
        {
            return container.Descendants()
                .Count(e => e.Name.LocalName.Equals("If", StringComparison.OrdinalIgnoreCase));
        }
    }
}
