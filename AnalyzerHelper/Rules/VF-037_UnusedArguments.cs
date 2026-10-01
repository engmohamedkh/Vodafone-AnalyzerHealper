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
    /// VF-037 — UnusedArguments
    /// Validates that declared workflow arguments are actually used in activity expressions
    /// or elements in the workflow body.
    /// Legacy rule: VodafoneWorkFlowsCustomRules\UnusedArguments.cs
    /// </summary>
    public sealed class UnusedArguments : IAnalyzerRule
    {
        public string RuleId => "VF-037";
        public string RuleName => "UnusedArguments";
        public string DefaultRecommendation => "Remove any Unused Arguments.";
        public bool RequiresUserInteraction => false;

        public IReadOnlyList<RuleCheckResult> Check(string filePath, string content)
        {
            var results = new List<RuleCheckResult>();

            if (string.IsNullOrWhiteSpace(content))
                return results;

            if (!filePath.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
                return results;

            XDocument doc;
            try
            {
                doc = XDocument.Parse(content, LoadOptions.PreserveWhitespace);
            }
            catch
            {
                return results;
            }

            if (doc.Root == null)
                return results;

            // Find workflow arguments declared in x:Members / x:Property
            var propertyElements = doc.Descendants()
                .Where(e => e.Name.LocalName.Equals("Property", StringComparison.OrdinalIgnoreCase) &&
                            (e.Parent?.Name.LocalName.Equals("Members", StringComparison.OrdinalIgnoreCase) == true ||
                             (e.Attribute("Type")?.Value.IndexOf("Argument", StringComparison.OrdinalIgnoreCase) >= 0)));

            var argNames = new List<string>();
            foreach (var prop in propertyElements)
            {
                string? name = prop.Attributes()
                    .FirstOrDefault(a => string.Equals(a.Name.LocalName, "Name", StringComparison.OrdinalIgnoreCase))
                    ?.Value?.Trim();

                if (!string.IsNullOrWhiteSpace(name) && !argNames.Contains(name, StringComparer.Ordinal))
                {
                    argNames.Add(name);
                }
            }

            if (argNames.Count == 0)
                return results;

            string bodyXml = ExtractBodyXml(content);
            string fileName = Path.GetFileName(filePath);

            foreach (string argName in argNames)
            {
                if (!IsUsedInBody(argName, bodyXml))
                {
                    results.Add(new RuleCheckResult
                    {
                        RuleId = RuleId,
                        RuleName = RuleName,
                        Level = RuleLevel.Warning,
                        Message = string.Format("The following workflow: '{0}' has an Unused Argument '{1}'.", fileName, argName),
                        FilePath = filePath,
                        Recommendation = DefaultRecommendation,
                        RequiresUserInteraction = RequiresUserInteraction
                    });
                }
            }

            return results;
        }

        private static string ExtractBodyXml(string content)
        {
            // Strip <x:Members>...</x:Members> and self-closing <x:Members />
            string body = Regex.Replace(content, @"<x:Members\b[\s\S]*?(?:/>|</x:Members>)", "", RegexOptions.IgnoreCase);

            // Strip <this:WorkflowName.argName>...</this:WorkflowName.argName> or self-closing companion tags
            body = Regex.Replace(body, @"<this:[^\s>]+[\s\S]*?</this:[^\s>]+>", "", RegexOptions.IgnoreCase);
            body = Regex.Replace(body, @"<this:[^\s>]+/>", "", RegexOptions.IgnoreCase);

            return body;
        }

        private static bool IsUsedInBody(string argName, string bodyXml)
        {
            if (string.IsNullOrWhiteSpace(bodyXml) || string.IsNullOrWhiteSpace(argName))
                return false;

            int idx = 0;
            while (true)
            {
                idx = bodyXml.IndexOf(argName, idx, StringComparison.Ordinal);
                if (idx < 0)
                    return false;

                bool leftOk = idx == 0 || (!char.IsLetterOrDigit(bodyXml[idx - 1]) && bodyXml[idx - 1] != '_');
                bool rightOk = idx + argName.Length >= bodyXml.Length || (!char.IsLetterOrDigit(bodyXml[idx + argName.Length]) && bodyXml[idx + argName.Length] != '_');

                if (leftOk && rightOk)
                    return true;

                idx++;
            }
        }
    }
}
