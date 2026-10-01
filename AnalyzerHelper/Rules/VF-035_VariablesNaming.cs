using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using AnalyzerHelper.Interfaces;
using AnalyzerHelper.Models;

namespace AnalyzerHelper.Rules
{
    /// <summary>
    /// VF-035 — Checks Variables Naming Convention.
    /// Enforces type prefix Hungarian notation (e.g. str, int, bool, dtm, dct, etc.)
    /// and checks for special characters in variable names taking case-sensitivity into account.
    /// </summary>
    public sealed class VariablesNaming : IAnalyzerRule
    {
        private static readonly (string TypeKey, string Prefix)[] TypeMappings = new[]
        {
            // Container / compound types first to prevent generic type args matching prematurely
            ("Dictionary", "dct"),
            ("List", "lst"),
            ("IEnumerable", "ie"),
            ("Array", "arr"),
            ("[]", "arr"),
            ("DataView", "dv"),
            ("DataTable", "dt"),
            ("DataRow", "dr"),
            ("NetworkCredential", "crd"),
            ("Credential", "crd"),
            ("SecureString", "sstr"),
            ("DateTime", "dtm"),
            ("TimeSpan", "tsp"),
            ("QueueItem", "qi"),
            ("JObject", "jobj"),
            ("DatabaseConnection", "db"),
            ("UiElement", "ui"),
            ("Regex", "rgx"),
            ("Searchoption", "sop"),
            ("Double", "dbl"),
            ("Decimal", "dec"),
            ("Int32", "int"),
            ("Int64", "int"),
            ("Int16", "int"),
            ("Integer", "int"),
            ("Boolean", "bool"),
            ("GenericValue", "gen"),
            ("String", "str"),
            ("Object", "obj")
        };

        private static readonly char[] SpecialChars = {
            ',', '\'', '.', '"', '?', '>', '<', ';', ':', '/', '\\', '|',
            ']', '[', '}', '{', '=', '+', '-', '(', ')', '*', '&', '^',
            '%', '$', '#', '@', '!', '`', '~', ' '
        };

        public string RuleId => "VF-035";
        public string RuleName => "VariablesNaming";
        public string DefaultRecommendation =>
            "Please use the following Naming Convention without any Special Characters: " +
            "'argtypeArgName' for Variables taking in consideration the case senstivity.";
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
                doc = XDocument.Parse(content, LoadOptions.SetLineInfo | LoadOptions.PreserveWhitespace);
            }
            catch
            {
                return results;
            }

            if (doc.Root == null)
                return results;

            var variableElements = doc.Descendants().Where(e => e.Name.LocalName == "Variable");

            foreach (var variableEl in variableElements)
            {
                string? varName = variableEl.Attributes()
                    .FirstOrDefault(a => string.Equals(a.Name.LocalName, "Name", StringComparison.OrdinalIgnoreCase))
                    ?.Value;

                if (string.IsNullOrWhiteSpace(varName))
                    continue;

                string typeAttr = variableEl.Attributes()
                    .FirstOrDefault(a =>
                        string.Equals(a.Name.LocalName, "TypeArguments", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(a.Name.LocalName, "Type", StringComparison.OrdinalIgnoreCase))
                    ?.Value ?? string.Empty;

                bool validPrefix = false;
                string? matchedPrefix = null;

                if (!string.IsNullOrEmpty(typeAttr))
                {
                    foreach (var (typeKey, prefix) in TypeMappings)
                    {
                        if (typeAttr.IndexOf(typeKey, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            matchedPrefix = prefix;
                            break;
                        }
                    }

                    if (!string.IsNullOrEmpty(matchedPrefix))
                    {
                        validPrefix = varName.StartsWith(matchedPrefix, StringComparison.Ordinal);
                    }
                }

                bool specialCharsExists = SpecialChars.Any(c => varName.Contains(c));

                if (!validPrefix || specialCharsExists)
                {
                    int line = 1;
                    if (variableEl is IXmlLineInfo lineInfo && lineInfo.HasLineInfo())
                    {
                        line = lineInfo.LineNumber;
                    }

                    results.Add(new RuleCheckResult
                    {
                        RuleId = RuleId,
                        RuleName = RuleName,
                        Level = RuleLevel.Warning,
                        FilePath = filePath,
                        Message = $"The following variable: '{varName}' doesn't match the Needed Naming Convention 'argtypeArgName' taking in consideration the case senstivity. [Prefix Validity: {validPrefix} ; Special Chars: {specialCharsExists}.]",
                        Recommendation = DefaultRecommendation,
                        RequiresUserInteraction = false
                    });
                }
            }

            return results;
        }
    }
}
