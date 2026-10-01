using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using AnalyzerHelper.Interfaces;
using AnalyzerHelper.Models;

namespace AnalyzerHelper.Rules
{
    /// <summary>VF-005 — hard-coded passwords / insecure password properties (cannot invent secrets).</summary>
    public sealed class HardCodedPasswordsRule : IAnalyzerRule
    {
        public string RuleId => "VF-005";
        public string RuleName => "HardCodedPasswords";
        public string DefaultRecommendation =>
            "Do not use hardcoded passwords. Prefer SecureString from Orchestrator/Config credentials.";
        public bool RequiresUserInteraction => false;

        // Config credential dictionary or approved password variable/argument (e.g. crddctroboCred, strCitrixPassword, in_strPassword)
        private static readonly Regex ConfigCredentialRegex = new Regex(
            @"\b(?:in_|io_|out_)?(?:crddctroboCred\s*\(|str_?\w*password\b)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public IReadOnlyList<RuleCheckResult> Check(string filePath, string content)
        {
            var results = new List<RuleCheckResult>();
            if (!XamlActivityHelper.TryParse(content, out var doc) || doc == null) return results;

            foreach (var el in XamlActivityHelper.EnumerateActivities(doc))
            {
                var messages = new List<string>();
                string display = XamlActivityHelper.GetDisplayName(el);

                // Password string properties with a value
                foreach (var attr in el.Attributes())
                {
                    string n = attr.Name.LocalName;
                    if (n.IndexOf("Password", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (string.IsNullOrWhiteSpace(attr.Value)) continue;

                    string expr = attr.Value;

                    // Config credential SecureString is valid (reusable components often bind Password = crddctroboCred("key").SecureString)
                    if (IsConfigCredentialExpression(expr) || IsApprovedStringVariableExpression(expr))
                        continue;

                    bool isSecureName = n.IndexOf("SecurePassword", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                        n.IndexOf("Secure", StringComparison.OrdinalIgnoreCase) >= 0;
                    bool usesSecureStringExpr = expr.IndexOf(".SecureString", StringComparison.OrdinalIgnoreCase) >= 0;

                    if (!isSecureName && !usesSecureStringExpr)
                    {
                        messages.Add($"The following activity {display} is using string password property. Please use the secure string instead.");
                    }
                    else if (IsHardCodedSecurePasswordExpression(expr))
                    {
                        messages.Add($"The following activity {display} has hard coded secure password value.");
                    }
                }

                foreach (var varEl in el.Descendants().Where(e => e.Name.LocalName == "Variable"))
                {
                    if (!IsSecureStringVariableWithHardCodedDefault(varEl)) continue;
                    messages.Add($"The following activity {display} has hard coded password variable.");
                }

                foreach (var msg in messages.Distinct())
                {
                    results.Add(new RuleCheckResult
                    {
                        RuleId = RuleId,
                        RuleName = RuleName,
                        Level = RuleLevel.Error,
                        Message = msg,
                        FilePath = filePath,
                        Recommendation = DefaultRecommendation
                    });
                }
            }

            // Also scan workflow-level variables
            foreach (var varEl in doc.Descendants().Where(e => e.Name.LocalName == "Variable"))
            {
                if (!IsSecureStringVariableWithHardCodedDefault(varEl)) continue;

                string name = varEl.Attributes()
                    .FirstOrDefault(a => string.Equals(a.Name.LocalName, "Name", StringComparison.OrdinalIgnoreCase))
                    ?.Value ?? "(unnamed)";
                results.Add(new RuleCheckResult
                {
                    RuleId = RuleId,
                    RuleName = RuleName,
                    Level = RuleLevel.Error,
                    Message = $"SecureString variable '{name}' has a default/hard-coded value.",
                    FilePath = filePath,
                    Recommendation = DefaultRecommendation
                });
            }

            return results;
        }

        /// <summary>True when expression loads a credential from Config (e.g. crddctroboCred("name").SecureString).</summary>
        private static bool IsConfigCredentialExpression(string expr) =>
            !string.IsNullOrWhiteSpace(expr) && ConfigCredentialRegex.IsMatch(expr);

        /// <summary>
        /// NetworkCredential("user","literalPassword") style hardcoding.
        /// Config credential dictionary access is not treated as hardcoded.
        /// </summary>
        private static bool IsHardCodedSecurePasswordExpression(string expr)
        {
            if (string.IsNullOrWhiteSpace(expr)) return false;
            if (IsConfigCredentialExpression(expr)) return false;
            return expr.Contains("\",\"");
        }

        private static bool IsSecureStringVariableWithHardCodedDefault(XElement varEl)
        {
            string typeHint = string.Join(" ", varEl.Attributes().Select(a => a.Value));
            if (typeHint.IndexOf("SecureString", StringComparison.OrdinalIgnoreCase) < 0) return false;

            string defaultExpr = GetVariableDefaultExpression(varEl);
            if (string.IsNullOrWhiteSpace(defaultExpr)) return false;

            // Default from Config credentials is valid at runtime
            if (IsConfigCredentialExpression(defaultExpr)) return false;

            return true;
        }

        private static string GetVariableDefaultExpression(XElement varEl)
        {
            var defaultAttr = varEl.Attributes()
                .FirstOrDefault(a => string.Equals(a.Name.LocalName, "Default", StringComparison.OrdinalIgnoreCase));
            if (defaultAttr != null && !string.IsNullOrWhiteSpace(defaultAttr.Value))
                return defaultAttr.Value;

            var defaultEl = varEl.Elements().FirstOrDefault(e => e.Name.LocalName == "Variable.Default");
            if (defaultEl == null) return null;

            // Prefer nested expression text; fall back to concatenated values
            string text = string.Concat(defaultEl.DescendantNodes().OfType<XText>().Select(t => t.Value)).Trim();
            if (!string.IsNullOrWhiteSpace(text)) return text;

            var valueAttr = defaultEl.DescendantsAndSelf()
                .SelectMany(e => e.Attributes())
                .FirstOrDefault(a =>
                    string.Equals(a.Name.LocalName, "ExpressionText", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(a.Name.LocalName, "Value", StringComparison.OrdinalIgnoreCase));
            return valueAttr?.Value;
        }

        private static bool IsApprovedStringVariableExpression(string expr)
        {
            if (string.IsNullOrWhiteSpace(expr)) return false;
            string trimmed = expr.Trim();
            // Check if expression is a variable/argument reference (e.g. [strPassword] or strPassword without literal quotes)
            bool isBracketVar = trimmed.StartsWith("[") && trimmed.EndsWith("]");
            bool hasNoStringQuotes = !trimmed.StartsWith("\"") && !trimmed.EndsWith("\"");
            return isBracketVar || hasNoStringQuotes;
        }
    }
}
