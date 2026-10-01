using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AnalyzerHelper.Interfaces;
using AnalyzerHelper.Models;

namespace AnalyzerHelper.Rules
{
    /// <summary>
    /// Sets the default value of any Variable named "strComponentName" or
    /// "strWorkflowName" to the XAML file's base name (no extension).
    ///
    /// Handles BOTH forms found in UiPath XAML files:
    ///
    /// Form A – Default as XML attribute (single-line):
    ///   &lt;Variable x:TypeArguments="x:String" Default="OldName" Name="strWorkflowName" /&gt;
    ///
    /// Form B – Default as child Literal element (multi-line):
    ///   &lt;Variable x:TypeArguments="x:String" Name="strComponentName"&gt;
    ///     &lt;Variable.Default&gt;
    ///       &lt;Literal x:TypeArguments="x:String"&gt;OldName&lt;/Literal&gt;
    ///     &lt;/Variable.Default&gt;
    ///   &lt;/Variable&gt;
    /// </summary>
    public sealed class FixComponentNameRule : IAnalyzerRuleWithFix
    {
        public string RuleId => "VF-021";
        public string RuleName => "FixComponentName";
        public string DefaultRecommendation =>
            "Set the Default value of 'strComponentName' and 'strWorkflowName' " +
            "variables to match the XAML file's own base name (without extension).";
        public bool RequiresUserInteraction => false;

        // ── Form A: Default="..." as an attribute on the <Variable> tag ──────
        // Matches the whole opening tag when Name="strComponentName|strWorkflowName"
        // is present anywhere in it, capturing the Default="..." value.
        private static readonly Regex _attrPattern = new Regex(
            @"<Variable\b(?=[^>]*\bName=""(strComponentName|strWorkflowName)"")([^>]*)\bDefault=""([^""]*)""",
            RegexOptions.Compiled);

        // ── Form B: <Literal …>value</Literal> inside <Variable.Default> ─────
        // We first locate the Variable tag by name, then replace the Literal body.
        private static readonly Regex _variableBlockPattern = new Regex(
            @"<Variable\b[^>]*\bName=""(strComponentName|strWorkflowName)""[^>]*>(.*?)</Variable>",
            RegexOptions.Compiled | RegexOptions.Singleline);

        private static readonly Regex _literalValuePattern = new Regex(
            @"(<Literal\b[^>]*>)([^<]*)(</Literal>)",
            RegexOptions.Compiled);

        // =====================================================================
        //  Check  (report only — no mutation)
        // =====================================================================

        public IReadOnlyList<RuleCheckResult> Check(string filePath, string content)
        {
            var results = new List<RuleCheckResult>();
            if (string.IsNullOrWhiteSpace(content)) return results;

            string baseName = Path.GetFileNameWithoutExtension(filePath);

            // Check Form A: Default attribute mismatch
            foreach (Match m in _attrPattern.Matches(content))
            {
                string varName = m.Groups[1].Value;
                string currentDefault = m.Groups[3].Value;
                if (currentDefault != baseName)
                {
                    results.Add(new RuleCheckResult
                    {
                        RuleId = RuleId,
                        RuleName = RuleName,
                        Level = RuleLevel.Warning,
                        Message = $"Variable \"{varName}\" has Default=\"{currentDefault}\" " +
                                  $"but file name is \"{baseName}\".",
                        FilePath = filePath,
                        Recommendation = $"Set '{varName}' default value to \"{baseName}\" to match the XAML file name.",
                        RequiresUserInteraction = RequiresUserInteraction
                    });
                }
            }

            // Check Form B: Literal value mismatch
            foreach (Match m in _variableBlockPattern.Matches(content))
            {
                string varName = m.Groups[1].Value;
                string blockContent = m.Groups[2].Value;

                if (!blockContent.Contains("<Variable.Default>")) continue;

                foreach (Match lit in _literalValuePattern.Matches(blockContent))
                {
                    string currentValue = lit.Groups[2].Value.Trim();
                    if (currentValue != baseName)
                    {
                        results.Add(new RuleCheckResult
                        {
                            RuleId = RuleId,
                            RuleName = RuleName,
                            Level = RuleLevel.Warning,
                            Message = $"Variable \"{varName}\" has Literal default \"{currentValue}\" " +
                                      $"but file name is \"{baseName}\".",
                            FilePath = filePath,
                            Recommendation = $"Set '{varName}' default value to \"{baseName}\" to match the XAML file name.",
                            RequiresUserInteraction = RequiresUserInteraction
                        });
                    }
                }
            }

            return results;
        }

        // =====================================================================
        //  DefineAndFix  (detect + fix — operates on content string)
        // =====================================================================

        public bool DefineAndFix(string filePath, string content, out string newContent)
        {
            newContent = content;
            if (string.IsNullOrWhiteSpace(content)) return false;

            string baseName = Path.GetFileNameWithoutExtension(filePath);
            bool modified = false;

            // ── Pass 1: Fix Form A (Default as attribute) ────────────────────
            string result = _attrPattern.Replace(newContent, m =>
            {
                string currentDefault = m.Groups[3].Value;
                if (currentDefault == baseName)
                    return m.Value;

                modified = true;
                // Groups[2] = everything between <Variable and Default="..."
                return $"<Variable{m.Groups[2].Value}Default=\"{baseName}\"";
            });

            // ── Pass 2: Fix Form B (Default as child Literal element) ────────
            result = _variableBlockPattern.Replace(result, m =>
            {
                string blockContent = m.Groups[2].Value;

                // Only touch it if there's a Variable.Default / Literal section
                if (!blockContent.Contains("<Variable.Default>"))
                    return m.Value;

                string fixedContent = _literalValuePattern.Replace(blockContent, lit =>
                {
                    string currentValue = lit.Groups[2].Value.Trim();
                    if (currentValue == baseName)
                        return lit.Value;

                    modified = true;
                    // Preserve the original whitespace wrapping around the value
                    string rawValue = lit.Groups[2].Value;
                    int valueIdx = rawValue.IndexOf(currentValue, StringComparison.Ordinal);
                    string leading = valueIdx >= 0
                        ? rawValue.Substring(0, valueIdx)
                        : "";
                    string trailing = valueIdx >= 0 && valueIdx + currentValue.Length < rawValue.Length
                        ? rawValue.Substring(valueIdx + currentValue.Length)
                        : "";

                    return $"{lit.Groups[1].Value}{leading}{baseName}{trailing}{lit.Groups[3].Value}";
                });

                if (fixedContent == blockContent)
                    return m.Value;

                return m.Value.Substring(0, m.Value.IndexOf(blockContent)) +
                       fixedContent +
                       "</Variable>";
            });

            if (!modified) return false;

            newContent = result;
            return true;
        }
    }
}
