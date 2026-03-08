using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using UiPath.Studio.Activities.Api;
using UiPath.Studio.Activities.Api.Analyzer;
using UiPath.Studio.Activities.Api.Analyzer.Rules;
using UiPath.Studio.Analyzer.Models;

namespace SelectorAttributeValueFromXamlSpace
{
    /// <summary>
    /// Workflow-level rule: reads the workflow XAML file and validates all Selector="..." values
    /// so that attributes (title, name, aaname) have values containing a wildcard (*, ?, #).
    /// Same role as SelectorAttributeFromXaml (XAML-based) but validates attribute values like SelectorAttributeValue.
    /// </summary>
    public class SelectorAttributeValueFromXaml : IRegisterAnalyzerConfiguration
    {
        const string RuleID = "VF-064";
        const string RuleName = "SelectorAttributeValueFromXaml";
        const string WFAnalyzerVersion = "WorkflowAnalyzerV4";

        /// <summary>Attributes whose value MUST contain a wildcard (*, ?, or #).</summary>
        static readonly string[] AttributesValueMustContainWildcard = new[] { "title", "name", "aaname" };

        static readonly char[] WildcardChars = new[] { '*', '?', '#' };

        /// <summary>Matches Selector="...value..." where value can contain ' and escaped " as &quot;</summary>
        static readonly Regex SelectorDoubleQuoteRegex = new Regex(
            @"Selector\s*=\s*""((?:[^""]|&quot;)*)""",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>Matches Selector='...value...' where value can contain " and escaped ' as &apos;</summary>
        static readonly Regex SelectorSingleQuoteRegex = new Regex(
            @"Selector\s*=\s*'((?:[^']|&apos;)*)'",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>Matches attr="value" or attr='value' for title, name, aaname.</summary>
        static readonly Regex AttributeValueRegex = new Regex(
            @"(title|name|aaname)\s*=\s*[""']([^""']*)[""']",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        public void Initialize(IAnalyzerConfigurationService workflowAnalyzerConfigService)
        {
            if (!workflowAnalyzerConfigService.HasFeature(WFAnalyzerVersion))
                return;
            var newRule = new Rule<IWorkflowModel>(RuleName, RuleID, Evaluate)
            {
                ErrorLevel = System.Diagnostics.TraceLevel.Error,
                RecommendationMessage = "Selector attribute values for " + string.Join(", ", AttributesValueMustContainWildcard) + " must contain a wildcard (*, ?, or #)."
            };
            workflowAnalyzerConfigService.AddRule<IWorkflowModel>(newRule);
        }

        /// <summary>Get workflow XAML file path: try Directory + RelativePath with \ and /.</summary>
        private static string GetWorkflowFilePath(IWorkflowModel workflow)
        {
            string dir = workflow.Project?.Directory?.ToString()?.TrimEnd('\\', '/') ?? "";
            string rel = workflow.RelativePath?.Replace('/', Path.DirectorySeparatorChar).TrimStart('\\', '/') ?? "";
            if (string.IsNullOrEmpty(dir)) return null;
            string path = Path.Combine(dir, rel);
            return File.Exists(path) ? path : null;
        }

        /// <summary>UI activity tag names in XAML that contain a Selector.</summary>
        static readonly string[] UiActivityTagNames = new[]
        {
            "Click", "GetValue", "TypeInto", "Hover", "SetValue", "SelectItem", "ElementExists", "Activate",
            "FindElement", "WaitForElement", "AttachWindow", "AttachBrowser", "DoubleClick", "RightClick",
            "InputMethod", "SendHotkey"
        };

        /// <summary>Decode selector value from XAML attribute.</summary>
        private static string DecodeSelectorValue(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return raw;
            return raw.Replace("&lt;", "<").Replace("&gt;", ">").Replace("&quot;", "\"");
        }

        /// <summary>Normalize selector for comparison (collapse whitespace).</summary>
        private static string NormalizeSelectorForCompare(string decoded)
        {
            if (string.IsNullOrEmpty(decoded)) return decoded;
            return Regex.Replace(decoded.Trim(), @"\s+", " ");
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

        /// <summary>Get activity DisplayName for this selector using XDocument.</summary>
        private static string GetActivityDisplayNameFromSelector(string xamlContent, string selectorValue)
        {
            if (string.IsNullOrWhiteSpace(xamlContent) || string.IsNullOrWhiteSpace(selectorValue))
                return null;
            try
            {
                string decodedSelector = DecodeSelectorValue(selectorValue);
                string normalizedSelector = NormalizeSelectorForCompare(decodedSelector);
                var doc = XDocument.Parse(xamlContent);

                foreach (var target in doc.Descendants())
                {
                    if (target.Name.LocalName != "Target")
                        continue;
                    var selectorAttr = target.Attributes().FirstOrDefault(a =>
                        string.Equals(a.Name.LocalName, "Selector", StringComparison.OrdinalIgnoreCase));
                    if (selectorAttr == null)
                        continue;
                    string targetSelectorDecoded = selectorAttr.Value;
                    if (string.IsNullOrEmpty(targetSelectorDecoded))
                        continue;
                    string targetNormalized = NormalizeSelectorForCompare(targetSelectorDecoded);
                    if (targetNormalized != normalizedSelector)
                        continue;

                    foreach (var parent in target.Ancestors())
                    {
                        var displayAttr = parent.Attributes().FirstOrDefault(a =>
                            string.Equals(a.Name.LocalName, "DisplayName", StringComparison.OrdinalIgnoreCase));
                        if (displayAttr != null && !string.IsNullOrWhiteSpace(displayAttr.Value))
                        {
                            string localName = parent.Name.LocalName ?? "";
                            if (localName == "Target")
                                continue;
                            if (localName.EndsWith(".Target", StringComparison.OrdinalIgnoreCase))
                                continue;
                            return displayAttr.Value.Trim();
                        }
                    }
                }
            }
            catch
            {
                // XAML parse or traversal failed
            }
            return null;
        }

        /// <summary>Fallback: find activity DisplayName by scanning backward in raw XAML.</summary>
        private static string GetActivityDisplayNameForSelectorPosition(string xamlContent, int selectorPosition)
        {
            if (string.IsNullOrEmpty(xamlContent) || selectorPosition <= 0) return null;
            string textBefore = xamlContent.Substring(0, selectorPosition);
            int lastTagIndex = -1;
            foreach (string tag in UiActivityTagNames)
            {
                string openTag = "<ui:" + tag;
                int idx = textBefore.LastIndexOf(openTag, StringComparison.OrdinalIgnoreCase);
                if (idx > lastTagIndex)
                    lastTagIndex = idx;
            }
            if (lastTagIndex < 0) return null;
            int chunkEnd = Math.Min(lastTagIndex + 1200, textBefore.Length);
            string tagChunk = textBefore.Substring(lastTagIndex, chunkEnd - lastTagIndex);
            var displayNameMatch = Regex.Match(tagChunk, @"DisplayName\s*=\s*""((?:[^""]|&quot;)*)""", RegexOptions.CultureInvariant);
            if (displayNameMatch.Success && displayNameMatch.Groups.Count >= 2)
                return displayNameMatch.Groups[1].Value.Replace("&quot;", "\"").Trim();
            return null;
        }

        /// <summary>Extract all Selector attribute values from XAML string.</summary>
        private static IEnumerable<(string selectorValue, int position)> EnumerateSelectorsInXaml(string xamlContent)
        {
            if (string.IsNullOrEmpty(xamlContent)) yield break;
            foreach (Match m in SelectorDoubleQuoteRegex.Matches(xamlContent))
            {
                if (m.Success && m.Groups.Count >= 2)
                {
                    string val = m.Groups[1].Value;
                    if (!string.IsNullOrWhiteSpace(val))
                        yield return (val, m.Index);
                }
            }
            foreach (Match m in SelectorSingleQuoteRegex.Matches(xamlContent))
            {
                if (m.Success && m.Groups.Count >= 2)
                {
                    string val = m.Groups[1].Value;
                    if (!string.IsNullOrWhiteSpace(val))
                        yield return (val, m.Index);
                }
            }
        }

        private InspectionResult Evaluate(IWorkflowModel workflow, Rule theNewRule)
        {
            var messageList = new List<string>();
            try
            {
                string filePath = GetWorkflowFilePath(workflow);
                if (string.IsNullOrEmpty(filePath))
                    return new InspectionResult() { HasErrors = false };

                string xamlContent = File.ReadAllText(filePath);
                foreach (var (selectorValue, position) in EnumerateSelectorsInXaml(xamlContent))
                {
                    List<string> missingWildcard = FindAttributesMissingWildcardValue(selectorValue);
                    if (missingWildcard.Count > 0)
                    {
                        string activityName = GetActivityDisplayNameFromSelector(xamlContent, selectorValue)
                            ?? GetActivityDisplayNameForSelectorPosition(xamlContent, position);
                        string activityLabel = !string.IsNullOrWhiteSpace(activityName)
                            ? string.Format("Activity '{0}'", activityName)
                            : "Selector";
                        messageList.Add(string.Format("{0} has selector attribute value(s) without a wildcard (*, ?, #): {1}. Values for {2} must contain a wildcard.",
                            activityLabel, string.Join("; ", missingWildcard), string.Join(", ", AttributesValueMustContainWildcard)));
                    }
                }
            }
            catch (Exception)
            {
                return new InspectionResult() { HasErrors = false };
            }

            if (messageList.Count == 0)
                return new InspectionResult() { HasErrors = false };

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
