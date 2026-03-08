using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UiPath.Studio.Activities.Api;
using UiPath.Studio.Activities.Api.Analyzer;
using UiPath.Studio.Activities.Api.Analyzer.Rules;
using UiPath.Studio.Analyzer.Models;

namespace SelectorAttributeValueSpace
{
    /// <summary>
    /// Checks that selected selector attributes (title, name, aaname) must contain a wildcard (*, ?, #) in their value.
    /// If any of these attributes are present but their value does not contain a wildcard, the rule highlights an error.
    /// </summary>
    public class SelectorAttributeValue : IRegisterAnalyzerConfiguration
    {
        const string RuleID = "VF-061";
        const string RuleName = "SelectorAttributeValue";
        const string WFAnalyzerVersion = "WorkflowAnalyzerV4";

        /// <summary>Activities that contain a selector - same scope as SelectorAttribute.</summary>
        static readonly string[] ActivitiesWithSelector = new[]
        {
            "Click", "TypeInto", "Type Into", "GetText", "Get Text", "Hover", "SetValue", "Set Value",
            "SelectItem", "Select Item", "ElementExists", "Element Exists", "Activate", "FindElement", "Find Element",
            "WaitForElement", "Wait For Element", "AttachWindow", "Attach Window", "AttachBrowser", "Attach Browser",
            "BrowserScope", "WindowScope", "GetValue", "InputMethod", "SendHotkey", "Send Hotkey",
            "ClickImage", "HoverImage", "DoubleClick", "Double Click", "RightClick", "Right Click"
        };

        /// <summary>Attributes whose value MUST contain a wildcard (*, ?, or #).</summary>
        static readonly string[] AttributesValueMustContainWildcard = new[] { "title", "name", "aaname" };

        static readonly char[] WildcardChars = new[] { '*', '?', '#' };

        /// <summary>Matches attr="value" or attr='value' for our attribute names.</summary>
        static readonly Regex AttributeValueRegex = new Regex(
            @"(title|name|aaname)\s*=\s*[""']([^""']*)[""']",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        public void Initialize(IAnalyzerConfigurationService workflowAnalyzerConfigService)
        {
            if (!workflowAnalyzerConfigService.HasFeature(WFAnalyzerVersion))
                return;

            var newRule = new Rule<IActivityModel>(RuleName, RuleID, SelectorAttributeValueCheck)
            {
                ErrorLevel = System.Diagnostics.TraceLevel.Error,
                RecommendationMessage = "Selector attribute values for " + string.Join(", ", AttributesValueMustContainWildcard) + " must contain a wildcard (*, ?, or #)."
            };

            workflowAnalyzerConfigService.AddRule<IActivityModel>(newRule);
        }

        private static bool ActivityHasSelector(IActivityModel activity)
        {
            if (activity?.ToolboxName == null)
                return false;
            string toolbox = activity.ToolboxName.Trim();
            return ActivitiesWithSelector.Any(a => toolbox.IndexOf(a, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        /// <summary>Gets selector XML from the activity (direct argument or from Target child, same as SelectorAttribute).</summary>
        private static string GetSelectorExpression(IActivityModel activity)
        {
            if (activity.Arguments != null)
            {
                var selectorArg = activity.Arguments.FirstOrDefault(a =>
                    a?.DisplayName != null &&
                    (a.DisplayName.IndexOf("Selector", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     a.DisplayName.IndexOf("Target", StringComparison.OrdinalIgnoreCase) >= 0));
                if (selectorArg != null && !string.IsNullOrWhiteSpace(selectorArg.DefinedExpression) && LooksLikeSelectorXml(selectorArg.DefinedExpression))
                    return selectorArg.DefinedExpression;
            }
            if (activity.Children != null)
            {
                foreach (var child in activity.Children)
                {
                    string fromChild = GetSelectorFromActivityOrChildren(child);
                    if (!string.IsNullOrWhiteSpace(fromChild)) return fromChild;
                }
            }
            return null;
        }

        private static string GetSelectorFromActivityOrChildren(IActivityModel node)
        {
            if (node?.Arguments != null)
            {
                var selectorArg = node.Arguments.FirstOrDefault(a =>
                    a?.DisplayName != null && a.DisplayName.IndexOf("Selector", StringComparison.OrdinalIgnoreCase) >= 0);
                if (selectorArg != null && !string.IsNullOrWhiteSpace(selectorArg.DefinedExpression) && LooksLikeSelectorXml(selectorArg.DefinedExpression))
                    return selectorArg.DefinedExpression;
            }
            if (node?.Children != null)
                foreach (var child in node.Children)
                {
                    string s = GetSelectorFromActivityOrChildren(child);
                    if (!string.IsNullOrWhiteSpace(s)) return s;
                }
            return null;
        }

        private static bool LooksLikeSelectorXml(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            string decoded = value.Replace("&lt;", "<").Replace("&gt;", ">").Replace("&quot;", "\"");
            return decoded.IndexOf("<wnd", StringComparison.OrdinalIgnoreCase) >= 0
                   || decoded.IndexOf("<ctrl", StringComparison.OrdinalIgnoreCase) >= 0
                   || (decoded.IndexOf("app=", StringComparison.OrdinalIgnoreCase) >= 0 && decoded.IndexOf("ctrlid", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        /// <summary>Finds attributes (title, name, aaname) whose value does NOT contain *, ?, or # (missing required wildcard).</summary>
        private static List<string> FindAttributesMissingWildcardValue(string selectorXml)
        {
            if (string.IsNullOrWhiteSpace(selectorXml))
                return new List<string>();
            string decoded = selectorXml.Replace("&lt;", "<").Replace("&gt;", ">").Replace("&quot;", "\"");
            var issues = new List<string>();
            foreach (Match m in AttributeValueRegex.Matches(decoded))
            {
                if (m.Success && m.Groups.Count >= 3)
                {
                    string attrName = m.Groups[1].Value.Trim();
                    string value = m.Groups[2].Value;
                    if (value.IndexOfAny(WildcardChars) < 0)
                        issues.Add(string.Format("{0}=\"{1}\"", attrName, value));
                }
            }
            return issues;
        }

        private InspectionResult SelectorAttributeValueCheck(IActivityModel activity, Rule theNewRule)
        {
            if (!ActivityHasSelector(activity))
                return new InspectionResult() { HasErrors = false };

            string selectorExpression = GetSelectorExpression(activity);
            if (string.IsNullOrWhiteSpace(selectorExpression))
                return new InspectionResult() { HasErrors = false };

            List<string> missingWildcard = FindAttributesMissingWildcardValue(selectorExpression);
            if (missingWildcard.Count == 0)
                return new InspectionResult() { HasErrors = false };

            var messageList = new List<string>
            {
                string.Format("Activity '{0}' has selector attribute value(s) without a wildcard (*, ?, #): {1}. Values for {2} must contain a wildcard.",
                    activity.DisplayName,
                    string.Join("; ", missingWildcard),
                    string.Join(", ", AttributesValueMustContainWildcard))
            };

            return new InspectionResult()
            {
                HasErrors = true,
                Messages = messageList,
                RecommendationMessage = "Selector attribute values for " + string.Join(", ", AttributesValueMustContainWildcard) + " must contain a wildcard (*, ?, or #).",
                ErrorLevel = theNewRule.DefaultErrorLevel
            };
        }
    }
}
