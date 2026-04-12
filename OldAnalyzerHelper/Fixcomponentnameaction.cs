using System.IO;
using System.Text.RegularExpressions;

namespace AnalyzerHelper
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
    public class FixComponentNameAction : IFixerAction
    {
        public string Name => "Fix Component / Workflow Name Variable";

        public string Description =>
            "Sets the Default value of any Variable named 'strComponentName' or " +
            "'strWorkflowName' to match the XAML file's own base name (without extension). ";

        // ── Form A: Default="..." as an attribute on the <Variable> tag ──────
        // Matches the whole opening tag when Name="strComponentName|strWorkflowName"
        // is present anywhere in it, capturing the Default="..." value.
        private static readonly Regex _attrPattern = new Regex(
            @"<Variable\b(?=[^>]*\bName=""(strComponentName|strWorkflowName)"")([^>]*)\bDefault=""([^""]*)""",
            RegexOptions.Compiled);

        // ── Form B: <Literal …>value</Literal> inside <Variable.Default> ─────
        // We first locate the Variable tag by name, then replace the Literal body.
        // Pattern matches the Literal element's inner text within a Variable.Default block.
        // Uses a two-step approach: find the enclosing Variable block, then fix the Literal.
        private static readonly Regex _variableBlockPattern = new Regex(
            @"<Variable\b[^>]*\bName=""(strComponentName|strWorkflowName)""[^>]*>(.*?)</Variable>",
            RegexOptions.Compiled | RegexOptions.Singleline);

        private static readonly Regex _literalValuePattern = new Regex(
            @"(<Literal\b[^>]*>)([^<]*)(</Literal>)",
            RegexOptions.Compiled);

        public bool Run(string filePath)
        {
            string baseName = Path.GetFileNameWithoutExtension(filePath);
            string original = File.ReadAllText(filePath);
            bool modified = false;

            // ── Pass 1: Fix Form A (Default as attribute) ────────────────────
            string result = _attrPattern.Replace(original, m =>
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
                string blockContent = m.Groups[2].Value; // everything between <Variable> and </Variable>

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
                    string leading = lit.Groups[2].Value.Length > currentValue.Length
                                      ? lit.Groups[2].Value.Substring(0, lit.Groups[2].Value.IndexOf(currentValue))
                                      : "";
                    string trailing = lit.Groups[2].Value.Length > currentValue.Length
                                      ? lit.Groups[2].Value.Substring(lit.Groups[2].Value.IndexOf(currentValue) + currentValue.Length)
                                      : "";

                    return $"{lit.Groups[1].Value}{leading}{baseName}{trailing}{lit.Groups[3].Value}";
                });

                if (fixedContent == blockContent)
                    return m.Value;

                return m.Value.Substring(0, m.Value.IndexOf(blockContent)) +
                       fixedContent +
                       "</Variable>";
            });

            if (modified)
                File.WriteAllText(filePath, result);

            return modified;
        }
    }
}