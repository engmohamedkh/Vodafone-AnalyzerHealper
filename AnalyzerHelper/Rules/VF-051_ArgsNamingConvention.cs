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
    /// VF-051 — Checks Arguments Naming Convention.
    /// Enforces argument directional prefixes (in_, out_, io_ / inout_), type prefix Hungarian notation
    /// (e.g. str, int, bool, dtm, dct, etc.), and checks for special characters in argument names
    /// taking case-sensitivity into account.
    /// </summary>
    public sealed class ArgsNamingConvention : IAnalyzerRule
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
            ("Browser", "brw"),
            ("String", "str"),
            ("Object", "obj")
        };

        private static readonly char[] SpecialChars = {
            ',', '\'', '.', '"', '?', '>', '<', ';', ':', '/', '\\', '|',
            ']', '[', '}', '{', '=', '+', '-', '(', ')', '*', '&', '^',
            '%', '$', '#', '@', '!', '`', '~', ' '
        };

        public string RuleId => "VF-051";
        public string RuleName => "Args Naming Convention";
        public string DefaultRecommendation =>
            "Please use the following Naming Convention without any Special Charachters: " +
            "'direction_argtypeArgName' for Arguments taking in consideration the case senstivity,";
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

            // Find workflow arguments declared in x:Members / x:Property
            var propertyElements = doc.Descendants()
                .Where(e => e.Name.LocalName.Equals("Property", StringComparison.OrdinalIgnoreCase) &&
                            (e.Parent?.Name.LocalName.Equals("Members", StringComparison.OrdinalIgnoreCase) == true ||
                             e.Attribute("Type")?.Value.IndexOf("Argument", StringComparison.OrdinalIgnoreCase) >= 0));

            foreach (var prop in propertyElements)
            {
                string? argName = prop.Attributes()
                    .FirstOrDefault(a => string.Equals(a.Name.LocalName, "Name", StringComparison.OrdinalIgnoreCase))
                    ?.Value;

                if (string.IsNullOrWhiteSpace(argName))
                    continue;

                string typeAttr = prop.Attributes()
                    .FirstOrDefault(a => string.Equals(a.Name.LocalName, "Type", StringComparison.OrdinalIgnoreCase))
                    ?.Value ?? string.Empty;

                // Determine expected direction from Type (InArgument, OutArgument, InOutArgument)
                string expectedDirection = "in";
                if (typeAttr.StartsWith("InOutArgument", StringComparison.OrdinalIgnoreCase))
                    expectedDirection = "inout";
                else if (typeAttr.StartsWith("OutArgument", StringComparison.OrdinalIgnoreCase))
                    expectedDirection = "out";
                else if (typeAttr.StartsWith("InArgument", StringComparison.OrdinalIgnoreCase))
                    expectedDirection = "in";

                // Direction in argument name (before first underscore)
                string[] parts = argName.Split('_');
                string directionInName = parts[0];

                bool validDirection = false;
                if (expectedDirection == "inout")
                {
                    validDirection = directionInName.Equals("inout") || directionInName.Equals("io");
                }
                else
                {
                    validDirection = directionInName.Equals(expectedDirection);
                }

                // Extract inner type between ( and )
                string innerType = typeAttr;
                int paren = typeAttr.IndexOf('(');
                if (paren >= 0)
                {
                    int close = typeAttr.LastIndexOf(')');
                    if (close > paren)
                        innerType = typeAttr.Substring(paren + 1, close - paren - 1);
                }

                string nameAfterDirection = argName.Length > directionInName.Length + 1 && argName[directionInName.Length] == '_'
                    ? argName.Substring(directionInName.Length + 1)
                    : argName;

                bool validPrefix = false;
                string? matchedPrefix = null;

                if (!string.IsNullOrEmpty(innerType))
                {
                    foreach (var (typeKey, prefix) in TypeMappings)
                    {
                        if (innerType.IndexOf(typeKey, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            matchedPrefix = prefix;
                            break;
                        }
                    }

                    if (!string.IsNullOrEmpty(matchedPrefix))
                    {
                        validPrefix = nameAfterDirection.StartsWith(matchedPrefix, StringComparison.Ordinal);
                    }
                }

                bool specialCharsExists = SpecialChars.Any(c => argName.Contains(c));

                if (!validPrefix || !validDirection || specialCharsExists)
                {
                    results.Add(new RuleCheckResult
                    {
                        RuleId = RuleId,
                        RuleName = RuleName,
                        Level = RuleLevel.Error,
                        FilePath = filePath,
                        Message = $"The following Argument: '{argName}' doesn't match the Needed Naming Convention 'direction_argtypeArgName' taking in consideration the case senstivity. [Direction Validity: {validDirection} ; Prefix Validity: {validPrefix} ; Special Chars: {specialCharsExists}].",
                        Recommendation = DefaultRecommendation,
                        RequiresUserInteraction = false
                    });
                }
            }

            return results;
        }
    }
}
