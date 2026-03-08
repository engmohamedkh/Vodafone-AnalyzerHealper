using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UiPath.Studio.Activities.Api;
using UiPath.Studio.Activities.Api.Analyzer;
using UiPath.Studio.Activities.Api.Analyzer.Rules;
using UiPath.Studio.Analyzer.Models;

namespace SelectorAttributeSpace
{
    /// <summary>
    /// Validates selector attributes on UI automation activities.
    /// Recommended (stable): automationId, name, role, class, title.
    /// Not recommended (unstable): ctrlid, idx, dynamic aaname, session-based IDs.
    /// Note: The activity API often does not expose the selector (tree/children return null). For reliable
    /// validation use the workflow rule SelectorAttributeFromXaml (VF-063), which reads the XAML file
    /// and validates all Selector attributes the same way.
    /// </summary>
    public class SelectorAttribute : IRegisterAnalyzerConfiguration
    {
        const string RuleID = "VF-060";
        const string RuleName = "SelectorAttribute";
        const string WFAnalyzerVersion = "WorkflowAnalyzerV4";

        /// <summary>Activities that contain a selector (UI automation). Add more as needed.</summary>
        static readonly string[] ActivitiesWithSelector = new[]
        {
            "Click", "TypeInto", "Type Into", "GetText", "Get Text", "Hover", "SetValue", "Set Value",
            "SelectItem", "Select Item", "ElementExists", "Element Exists", "Activate", "FindElement", "Find Element",
            "WaitForElement", "Wait For Element", "AttachWindow", "Attach Window", "AttachBrowser", "Attach Browser",
            "BrowserScope", "WindowScope", "GetValue", "InputMethod", "SendHotkey", "Send Hotkey",
            "ClickImage", "HoverImage", "DoubleClick", "Double Click", "RightClick", "Right Click"
        };

        /// <summary>Selector attributes that are unstable - use causes an error.</summary>
        static readonly string[] UnstableAttributes = new[]
        {
            "ctrlid", "idx", "aaname",
            "sessionid", "session_id", "processid", "process_id",
            "dynamicid", "dynamic_id", "runtimeid", "runtime_id"
        };

        /// <summary>Recommended (stable) attributes - for recommendation message only.</summary>
        static readonly string RecommendedAttributes = "automationId, name, role, class, title";

        static readonly Regex AttributeNameRegex = new Regex(
            @"\s+([a-zA-Z][a-zA-Z0-9_]*)\s*=",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public void Initialize(IAnalyzerConfigurationService workflowAnalyzerConfigService)
        {
            if (!workflowAnalyzerConfigService.HasFeature(WFAnalyzerVersion))
                return;

            var newRule = new Rule<IActivityModel>(RuleName, RuleID, SelectorAttributeCheck)
            {
                ErrorLevel = System.Diagnostics.TraceLevel.Error,
                RecommendationMessage = "Use stable selector attributes: " + RecommendedAttributes + ". Avoid: ctrlid, idx, dynamic aaname, session-based IDs."
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

        /// <summary>Gets selector XML from the activity. Tries: (1) Selector/Target argument, (2) children, (3) any argument value in tree that looks like selector XML.</summary>
        private static string GetSelectorExpression(IActivityModel activity)
        {
            // 1) Direct argument named Selector or Target
            if (activity.Arguments != null)
            {
                var selectorArg = activity.Arguments.FirstOrDefault(a =>
                    a?.DisplayName != null &&
                    (a.DisplayName.IndexOf("Selector", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     a.DisplayName.IndexOf("Target", StringComparison.OrdinalIgnoreCase) >= 0));
                if (selectorArg != null && !string.IsNullOrWhiteSpace(selectorArg.DefinedExpression) && LooksLikeSelectorXml(selectorArg.DefinedExpression))
                    return selectorArg.DefinedExpression;
            }
            // 2) Recurse into children for Selector argument
            if (activity.Children != null)
            {
                foreach (var child in activity.Children)
                {
                    string fromChild = GetSelectorFromActivityOrChildren(child);
                    if (!string.IsNullOrWhiteSpace(fromChild))
                        return fromChild;
                }
            }
            // 3) Fallback: scan ALL argument values in this activity and its descendants for anything that looks like selector XML (API may not expose "Selector" by name)
            return GetSelectorFromAnyArgumentInTree(activity);
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

        /// <summary>Scans every argument DefinedExpression in the activity tree and returns the first that looks like selector XML.</summary>
        private static string GetSelectorFromAnyArgumentInTree(IActivityModel node)
        {
            if (node == null) return null;
            if (node.Arguments != null)
            {
                foreach (var arg in node.Arguments)
                {
                    string expr = arg?.DefinedExpression;
                    if (!string.IsNullOrWhiteSpace(expr) && LooksLikeSelectorXml(expr))
                        return expr;
                }
            }
            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    string s = GetSelectorFromAnyArgumentInTree(child);
                    if (!string.IsNullOrWhiteSpace(s)) return s;
                }
            }
            return null;
        }

        private static bool LooksLikeSelectorXml(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            string decoded = value.Replace("&lt;", "<").Replace("&gt;", ">").Replace("&quot;", "\"");
            return decoded.IndexOf("<wnd", StringComparison.OrdinalIgnoreCase) >= 0
                   || decoded.IndexOf("<ctrl", StringComparison.OrdinalIgnoreCase) >= 0
                   || decoded.IndexOf("app=", StringComparison.OrdinalIgnoreCase) >= 0
                   || decoded.IndexOf("ctrlid", StringComparison.OrdinalIgnoreCase) >= 0
                   || decoded.IndexOf("automationid", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static List<string> FindUnstableAttributesInSelector(string selectorXml)
        {
            if (string.IsNullOrWhiteSpace(selectorXml))
                return new List<string>();
            // Selector may be stored HTML-encoded in XAML (e.g. &lt; &gt; &quot;)
            string decoded = selectorXml.Replace("&lt;", "<").Replace("&gt;", ">").Replace("&quot;", "\"");
            var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match m in AttributeNameRegex.Matches(decoded))
            {
                if (m.Success && m.Groups.Count >= 2)
                {
                    string attr = m.Groups[1].Value.Trim();
                    if (UnstableAttributes.Any(u => string.Equals(attr, u, StringComparison.OrdinalIgnoreCase)))
                        found.Add(attr);
                    // session-based: any attribute name containing "session" and "id"
                    if (attr.IndexOf("session", StringComparison.OrdinalIgnoreCase) >= 0 &&
                        attr.IndexOf("id", StringComparison.OrdinalIgnoreCase) >= 0)
                        found.Add(attr);
                }
            }
            return found.ToList();
        }

        private InspectionResult SelectorAttributeCheck(IActivityModel activity, Rule theNewRule)
        {
			// Check if this activity From Activities which have Selector Attributes or not
			if (!ActivityHasSelector(activity))
                return new InspectionResult() { HasErrors = false };

			// Return Seletor As Xml String
			string selectorExpression = GetSelectorExpression(activity);
            if (string.IsNullOrWhiteSpace(selectorExpression))
                return new InspectionResult() { HasErrors = false };
			// Check if Contain Unstable Attributes or Not
			List<string> unstableFound = FindUnstableAttributesInSelector(selectorExpression);
            if (unstableFound.Count == 0)
                return new InspectionResult() { HasErrors = false };
			// Output Error Message
			var messageList = new List<string>
            {
                string.Format("Activity '{0}' uses unstable selector attribute(s): {1}. Use stable attributes instead: {2}.",
                    activity.DisplayName,
                    string.Join(", ", unstableFound),
                    RecommendedAttributes)
            };

            return new InspectionResult()
            {
                HasErrors = true,
                Messages = messageList,
                RecommendationMessage = "Use stable selector attributes: " + RecommendedAttributes + ". Avoid: ctrlid, idx, dynamic aaname, session-based IDs.",
                ErrorLevel = theNewRule.DefaultErrorLevel
            };
        }
    }
}
