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
    /// VF-057 (also covers VF-061 / VF-064) — Validates selector attribute values in XAML.
    /// Ensures that key selector attributes (title, name, aaname) contain wildcards (*, ?, #)
    /// and are not composed solely of wildcard characters.
    /// </summary>
    public sealed class SelectorAttributeValueFromXaml : IAnalyzerRule
    {
        public string RuleId => "VF-057";
        public string RuleName => "static Selector";
        public string DefaultRecommendation =>
            "Selector attribute values for " + string.Join(", ", AttributesValueMustContainWildcard) +
            " must contain a wildcard (*, ?, or #) and cannot be only wildcard characters.";
        public bool RequiresUserInteraction => false;

        private static readonly string[] AttributesValueMustContainWildcard = { "title", "name", "aaname" };
        private static readonly char[] WildcardChars = { '*', '?', '#' };

        private static readonly Regex SelectorDoubleQuoteRegex = new(
            @"Selector\s*=\s*""((?:[^""]|&quot;)*)""",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex SelectorSingleQuoteRegex = new(
            @"Selector\s*=\s*'((?:[^']|&apos;)*)'",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex AttributeValueRegex = new(
            @"(title|name|aaname)\s*=\s*[""']([^""']*)[""']",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        private static readonly string[] UiActivityTagNames =
        {
            "Click", "GetValue", "TypeInto", "Hover", "SetValue", "SelectItem", "ElementExists", "Activate",
            "FindElement", "WaitForElement", "AttachWindow", "AttachBrowser", "DoubleClick", "RightClick",
            "InputMethod", "SendHotkey"
        };

        private sealed class SelectorAttributeIssue
        {
            public string? MissingWildcardText { get; set; }
            public string? WildcardOnlyText { get; set; }
        }

        public IReadOnlyList<RuleCheckResult> Check(string filePath, string content)
        {
            var results = new List<RuleCheckResult>();

            if (string.IsNullOrWhiteSpace(filePath) || !filePath.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
                return results;

            if (string.IsNullOrWhiteSpace(content))
                return results;

            try
            {
                XamlActivityHelper.TryParse(content, out var doc);

                foreach (var (selectorValue, position) in EnumerateSelectorsInXaml(content))
                {
                    var issue = FindSelectorAttributeIssues(selectorValue);
                    if (string.IsNullOrEmpty(issue.MissingWildcardText) && string.IsNullOrEmpty(issue.WildcardOnlyText))
                        continue;

                    string? activityName = GetActivityDisplayNameFromDoc(doc, selectorValue)
                                          ?? GetActivityDisplayNameForSelectorPosition(content, position);

                    string activityLabel = !string.IsNullOrWhiteSpace(activityName)
                        ? $"Activity '{activityName}'"
                        : "Selector";

                    if (!string.IsNullOrEmpty(issue.MissingWildcardText))
                    {
                        results.Add(new RuleCheckResult
                        {
                            RuleId = RuleId,
                            RuleName = RuleName,
                            Level = RuleLevel.Warning,
                            Message = $"{activityLabel} has selector attribute value(s) without a wildcard (*, ?, #): {issue.MissingWildcardText}.",
                            FilePath = filePath,
                            Recommendation = DefaultRecommendation
                        });
                    }

                    if (!string.IsNullOrEmpty(issue.WildcardOnlyText))
                    {
                        results.Add(new RuleCheckResult
                        {
                            RuleId = RuleId,
                            RuleName = RuleName,
                            Level = RuleLevel.Warning,
                            Message = $"{activityLabel} has selector attribute value(s) made only of wildcard characters (*, ?, #): {issue.WildcardOnlyText}.",
                            FilePath = filePath,
                            Recommendation = DefaultRecommendation
                        });
                    }
                }
            }
            catch
            {
                // Return accumulated results or empty on parse failure
            }

            return results;
        }

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

        private static SelectorAttributeIssue FindSelectorAttributeIssues(string selectorXml)
        {
            var issue = new SelectorAttributeIssue();

            if (string.IsNullOrWhiteSpace(selectorXml))
                return issue;

            string decoded = DecodeSelectorValue(selectorXml);
            var missingWildcard = new List<string>();
            var wildcardOnly = new List<string>();

            foreach (Match m in AttributeValueRegex.Matches(decoded))
            {
                if (!m.Success || m.Groups.Count < 3) continue;

                string attrName = m.Groups[1].Value.Trim();
                string value = m.Groups[2].Value;

                if (value.IndexOfAny(WildcardChars) < 0)
                {
                    missingWildcard.Add($"{attrName}=\"{value}\"");
                }
                else if (value.Length > 0 && value.All(c => WildcardChars.Contains(c)))
                {
                    wildcardOnly.Add($"{attrName}=\"{value}\"");
                }
            }

            issue.MissingWildcardText = missingWildcard.Count > 0 ? string.Join("; ", missingWildcard) : null;
            issue.WildcardOnlyText = wildcardOnly.Count > 0 ? string.Join("; ", wildcardOnly) : null;
            return issue;
        }

        private static string DecodeSelectorValue(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return raw;
            return raw.Replace("&lt;", "<").Replace("&gt;", ">").Replace("&quot;", "\"").Replace("&apos;", "'");
        }

        private static string NormalizeSelectorForCompare(string decoded)
        {
            if (string.IsNullOrEmpty(decoded)) return decoded;
            return Regex.Replace(decoded.Trim(), @"\s+", " ");
        }

        private static string? GetActivityDisplayNameFromDoc(XDocument? doc, string selectorValue)
        {
            if (doc == null || string.IsNullOrWhiteSpace(selectorValue)) return null;

            try
            {
                string decodedSelector = DecodeSelectorValue(selectorValue);
                string normalizedSelector = NormalizeSelectorForCompare(decodedSelector);

                foreach (var el in doc.Descendants())
                {
                    var selectorAttr = el.Attributes().FirstOrDefault(a =>
                        string.Equals(a.Name.LocalName, "Selector", StringComparison.OrdinalIgnoreCase));
                    if (selectorAttr == null || string.IsNullOrEmpty(selectorAttr.Value))
                        continue;

                    string targetNormalized = NormalizeSelectorForCompare(DecodeSelectorValue(selectorAttr.Value));
                    if (targetNormalized != normalizedSelector)
                        continue;

                    // If el is an activity itself, or find enclosing activity ancestor
                    XElement candidate = el;
                    if (el.Name.LocalName.Equals("Target", StringComparison.OrdinalIgnoreCase) ||
                        el.Name.LocalName.EndsWith(".Target", StringComparison.OrdinalIgnoreCase))
                    {
                        var ancestor = el.Ancestors().FirstOrDefault(a =>
                            !XamlActivityHelper.IsPropertyElement(a) &&
                            !a.Name.LocalName.Equals("Target", StringComparison.OrdinalIgnoreCase) &&
                            !a.Name.LocalName.EndsWith(".Target", StringComparison.OrdinalIgnoreCase));
                        if (ancestor != null) candidate = ancestor;
                    }

                    string display = XamlActivityHelper.GetDisplayName(candidate);
                    if (!string.IsNullOrWhiteSpace(display) && !display.Equals("Target", StringComparison.OrdinalIgnoreCase))
                        return display;
                }
            }
            catch
            {
                // Fallback to text position scanning
            }

            return null;
        }

        private static string? GetActivityDisplayNameForSelectorPosition(string xamlContent, int selectorPosition)
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
    }
}
