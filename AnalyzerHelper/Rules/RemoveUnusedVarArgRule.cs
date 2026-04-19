using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using AnalyzerHelper.Interfaces;
using AnalyzerHelper.Models;

namespace AnalyzerHelper.Rules
{
    /// <summary>
    /// Removes unused Variables and Arguments from UiPath XAML files.
    ///
    /// Rules:
    ///  1. Usage is determined ONLY from the workflow BODY (everything from the
    ///     first &lt;Sequence&gt; or &lt;Flowchart&gt; onward). The header region
    ///     (&lt;x:Members&gt;, companion &lt;this:ClassName.arg&gt; elements) is
    ///     intentionally excluded so declarations don't count as self-usages.
    ///     Self-closing &lt;Variable /&gt; nodes under that root are also stripped
    ///     from the usage string (their Name= would otherwise match as a "use"),
    ///     while Default= values are still scanned for references to other names.
    ///  2. A name is "used" if it appears:
    ///       a. Inside a [...] VB expression in the body.
    ///       b. As an XML attribute NAME bound to a value (output bindings like
    ///          out_boolActionSuccess="[True]").
    ///  3. Selector safety: if a declared name appears inside any Selector="..."
    ///     value in the body, it is NEVER removed.
    ///     (Only DECLARED names are checked — not every token in the selector.)
    ///  4. Duplicate scope: if the same variable Name is declared in both an
    ///     outer and inner scope, the INNER declaration is removed. Unused-variable
    ///     reporting ignores only those inner elements — not every declaration
    ///     with that name (same idea as per-argument removal).
    ///  5. When an argument is removed, its companion &lt;this:ClassName.argName&gt;
    ///     default-value element is also removed.
    /// </summary>
    public sealed class RemoveUnusedVarArgRule : IAnalyzerRuleWithFix
    {
        public string RuleId => "VF-019";
        public string RuleName => "RemoveUnusedVarArg";
        public string DefaultRecommendation =>
            "Variables and arguments use the same usage rules (body expressions, " +
            "selector values, attribute names, element text). Remove unused ones; " +
            "for duplicate variable names, remove inner shadowing declarations only. " +
            "Names referenced inside Selector attributes are always kept.";
        public bool RequiresUserInteraction => false;

        // ── Namespace ─────────────────────────────────────────────────────────
        private static readonly XNamespace _xNs =
            "http://schemas.microsoft.com/winfx/2006/xaml";

        // ── Patterns ──────────────────────────────────────────────────────────

        // Any [...] VB expression value
        private static readonly Regex _expr =
            new Regex(@"\[([^\[\]]+)\]", RegexOptions.Compiled);

        // Selector="..." attribute (may be XML-entity-encoded, may span lines)
        private static readonly Regex _selectorAttr =
            new Regex(@"\bSelector=""([^""]*?)""",
                RegexOptions.Compiled | RegexOptions.Singleline);

        // Self-closing Variable under the workflow root lives inside ExtractBody text;
        // remove for usage scan so Name="x" is not treated as a reference to x.
        private static readonly Regex _variableSelfClosing =
            new Regex(@"<Variable\b[\s\S]*?/>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex _variableDefaultAttr =
            new Regex(@"\bDefault\s*=\s*""([^""]*)""", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // =====================================================================
        //  Check  (report only — no mutation)
        // =====================================================================

        public IReadOnlyList<RuleCheckResult> Check(string filePath, string content)
        {
            var results = new List<RuleCheckResult>();
            if (string.IsNullOrWhiteSpace(content)) return results;

            XDocument doc;
            try { doc = XDocument.Parse(content, LoadOptions.PreserveWhitespace); }
            catch { return results; }

            string bodyForUsage = BuildBodyXmlForUsageScan(ExtractBody(content));

            var allVars = CollectVariables(doc);
            var allArgs = CollectArguments(doc);

            var allDeclaredNames = new HashSet<string>(
                allVars.Select(v => v.VarName).Concat(allArgs.Select(a => a.ArgName)),
                StringComparer.Ordinal);

            var usedNames = FindUsedNames(bodyForUsage, allDeclaredNames);

            // Inner-scope duplicate variables (shadowing): remove inner declarations only.
            // Exclude inner duplicate *elements* from the unused list — not the name globally,
            // or an unused outer with the same name would never be reported (arguments have no duplicate case).
            var innerDuplicateVarElements = new HashSet<XElement>();
            var duplicateInnerVarNames = new List<string>();
            foreach (var grp in allVars.GroupBy(v => v.VarName))
            {
                var ordered = grp.OrderBy(v => v.Depth).ToList();
                if (ordered.Count <= 1) continue;
                foreach (var inner in ordered.Skip(1))
                {
                    innerDuplicateVarElements.Add(inner.Element);
                    duplicateInnerVarNames.Add(inner.VarName);
                }
            }

            // Genuinely unused variables (same usage rules as arguments; per declaration, not per name).
            var unusedVarNames = allVars
                .Where(v => !usedNames.Contains(v.VarName) && !innerDuplicateVarElements.Contains(v.Element))
                .Select(v => v.VarName)
                .Distinct()
                .ToList();

            // Unused arguments
            var unusedArgNames = allArgs
                .Where(a => !usedNames.Contains(a.ArgName))
                .Select(a => a.ArgName)
                .Distinct()
                .ToList();

            if (duplicateInnerVarNames.Count > 0)
            {
                results.Add(new RuleCheckResult
                {
                    RuleId = RuleId,
                    RuleName = RuleName,
                    Level = RuleLevel.Warning,
                    Message = "Duplicate inner variables: " +
                              string.Join(", ", duplicateInnerVarNames.Distinct()) + ".",
                    FilePath = filePath,
                    Recommendation = DefaultRecommendation,
                    RequiresUserInteraction = RequiresUserInteraction
                });
            }

            if (unusedVarNames.Count > 0)
            {
                results.Add(new RuleCheckResult
                {
                    RuleId = RuleId,
                    RuleName = RuleName,
                    Level = RuleLevel.Warning,
                    Message = "Unused variables: " +
                              string.Join(", ", unusedVarNames) + ".",
                    FilePath = filePath,
                    Recommendation = DefaultRecommendation,
                    RequiresUserInteraction = RequiresUserInteraction
                });
            }

            if (unusedArgNames.Count > 0)
            {
                results.Add(new RuleCheckResult
                {
                    RuleId = RuleId,
                    RuleName = RuleName,
                    Level = RuleLevel.Warning,
                    Message = "Unused arguments: " +
                              string.Join(", ", unusedArgNames) + ".",
                    FilePath = filePath,
                    Recommendation = DefaultRecommendation,
                    RequiresUserInteraction = RequiresUserInteraction
                });
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

            XDocument doc;
            try { doc = XDocument.Parse(content, LoadOptions.PreserveWhitespace); }
            catch { return false; }

            // ── 1. Split into header and body regions ─────────────────────────
            string bodyForUsage = BuildBodyXmlForUsageScan(ExtractBody(content));

            // ── 2. Collect all declared names ─────────────────────────────────
            var allVars = CollectVariables(doc);
            var allArgs = CollectArguments(doc);

            var allDeclaredNames = new HashSet<string>(
                allVars.Select(v => v.VarName).Concat(allArgs.Select(a => a.ArgName)),
                StringComparer.Ordinal);

            // ── 3. Find which declared names are actually USED in the body ────
            var usedNames = FindUsedNames(bodyForUsage, allDeclaredNames);

            // ── 4. Decide which variables to remove ───────────────────────────

            var toRemoveVars = new HashSet<XElement>();

            // 4a. Inner-scope duplicates → remove inner, keep outermost
            foreach (var grp in allVars.GroupBy(v => v.VarName))
            {
                var ordered = grp.OrderBy(v => v.Depth).ToList();
                if (ordered.Count > 1)
                    foreach (var inner in ordered.Skip(1))
                        toRemoveVars.Add(inner.Element);
            }

            // 4b. Genuinely unused
            foreach (var v in allVars)
            {
                if (!toRemoveVars.Contains(v.Element) && !usedNames.Contains(v.VarName))
                    toRemoveVars.Add(v.Element);
            }

            // ── 5. Decide which arguments to remove ───────────────────────────
            var toRemoveArgs = allArgs
                .Where(a => !usedNames.Contains(a.ArgName))
                .Select(a => a.Element)
                .ToHashSet();

            // ── 6. Companion elements for removed arguments ───────────────────
            var removedArgNames = allArgs
                .Where(a => toRemoveArgs.Contains(a.Element))
                .Select(a => a.ArgName)
                .ToHashSet();

            var companions = CollectArgCompanions(doc, removedArgNames);

            if (toRemoveVars.Count == 0 && toRemoveArgs.Count == 0 && companions.Count == 0)
                return false;

            // ── 7. Remove ─────────────────────────────────────────────────────
            foreach (var el in toRemoveVars.Concat(toRemoveArgs).Concat(companions))
                RemoveClean(el);

            CleanEmptyVariableContainers(doc);

            // ── 8. Serialise ──────────────────────────────────────────────────
            newContent = Serialise(doc, content);
            return newContent != content;
        }

        // =====================================================================
        //  Body extraction
        // =====================================================================

        /// <summary>
        /// Returns the XML text starting from the first root activity element
        /// (&lt;Sequence&gt; or &lt;Flowchart&gt;). Everything before it
        /// (x:Members, companion this: elements, metadata) is excluded so those
        /// declarations are not mistaken for usages.
        /// </summary>
        private static string ExtractBody(string xml)
        {
            int seqIdx = IndexOfTag(xml, "<Sequence ");
            int flowIdx = IndexOfTag(xml, "<Flowchart ");
            int start = PickFirst(seqIdx, flowIdx);
            return start >= 0 ? xml.Substring(start) : xml;
        }

        private static int IndexOfTag(string xml, string tagPrefix)
            => xml.IndexOf(tagPrefix, StringComparison.Ordinal);

        private static int PickFirst(int a, int b)
        {
            if (a < 0) return b;
            if (b < 0) return a;
            return Math.Min(a, b);
        }

        /// <summary>
        /// Removes self-closing &lt;Variable /&gt; markup from the body string so
        /// <c>Name="unusedVar"</c> is not counted as a usage. Appends <c>Default="..."</c>
        /// values so expressions that reference other variables still register.
        /// </summary>
        private static string BuildBodyXmlForUsageScan(string rawBodyFromExtract)
        {
            if (string.IsNullOrEmpty(rawBodyFromExtract))
                return rawBodyFromExtract;

            var defaultExprs = new StringBuilder();
            string stripped = _variableSelfClosing.Replace(rawBodyFromExtract, m =>
            {
                foreach (Match dm in _variableDefaultAttr.Matches(m.Value))
                    defaultExprs.Append(' ').Append(dm.Groups[1].Value);
                return " ";
            });

            if (defaultExprs.Length > 0)
                stripped += " " + defaultExprs;

            return stripped;
        }

        // =====================================================================
        //  Collecting declarations
        // =====================================================================

        private static List<VarDecl> CollectVariables(XDocument doc)
        {
            var result = new List<VarDecl>();
            WalkVars(doc.Root, 0, result);
            return result;
        }

        private static void WalkVars(XElement el, int depth, List<VarDecl> result)
        {
            if (el == null) return;

            if (el.Name.LocalName.EndsWith(".Variables"))
            {
                foreach (var v in el.Elements().Where(e => e.Name.LocalName == "Variable"))
                {
                    string name = v.Attributes()
                        .FirstOrDefault(a => a.Name.LocalName == "Name")?.Value;
                    if (!string.IsNullOrEmpty(name))
                        result.Add(new VarDecl { VarName = name, Depth = depth, Element = v });
                }
            }

            foreach (var child in el.Elements())
                WalkVars(child, depth + 1, result);
        }

        private static List<ArgDecl> CollectArguments(XDocument doc)
        {
            var result = new List<ArgDecl>();
            var members = doc.Descendants(_xNs + "Members").FirstOrDefault();
            if (members == null) return result;

            foreach (var prop in members.Elements(_xNs + "Property"))
            {
                string name = (string)prop.Attribute("Name");
                if (!string.IsNullOrEmpty(name))
                    result.Add(new ArgDecl { ArgName = name, Element = prop });
            }
            return result;
        }

        /// <summary>
        /// Finds companion &lt;this:ClassName.argName&gt; elements that hold
        /// default values for arguments. These are direct children of the root
        /// Activity element whose local name ends with ".argName".
        /// </summary>
        private static List<XElement> CollectArgCompanions(
            XDocument doc, HashSet<string> argNames)
        {
            if (argNames.Count == 0) return new List<XElement>();

            return (doc.Root?.Elements() ?? Enumerable.Empty<XElement>())
                .Where(el =>
                {
                    int dot = el.Name.LocalName.LastIndexOf('.');
                    return dot >= 0 && argNames.Contains(el.Name.LocalName.Substring(dot + 1));
                })
                .ToList();
        }

        // =====================================================================
        //  Usage detection  (runs on BODY only)
        // =====================================================================

        /// <summary>
        /// Returns the subset of <paramref name="declaredNames"/> that are
        /// actually referenced in <paramref name="bodyXml"/>.
        /// </summary>
        private static HashSet<string> FindUsedNames(
            string bodyXml, HashSet<string> declaredNames)
        {
            var used = new HashSet<string>(StringComparer.Ordinal);

            // ── A. Names used in [...] VB expressions ─────────────────────────
            foreach (Match m in _expr.Matches(bodyXml))
            {
                string expr = m.Groups[1].Value;
                foreach (string name in declaredNames)
                    if (IsWord(name, expr))
                        used.Add(name);
            }

            // ── B. Names used inside Selector="..." values ────────────────────
            //    Only check DECLARED names — not every HTML token in the selector.
            foreach (Match m in _selectorAttr.Matches(bodyXml))
            {
                string selectorRaw = m.Groups[1].Value;
                string selectorDecoded = System.Net.WebUtility.HtmlDecode(selectorRaw);

                foreach (string name in declaredNames)
                {
                    if (used.Contains(name)) continue; // already found
                    if (IsWord(name, selectorRaw) || IsWord(name, selectorDecoded))
                        used.Add(name);
                }
            }

            // ── C. Names used as XML attribute NAMES (output bindings) ─────────
            //    e.g.  out_boolActionSuccess="[True]"
            //    These appear as attribute names, not values, so expression
            //    scanning misses them.
            foreach (string name in declaredNames)
            {
                if (used.Contains(name)) continue;

                var pattern = new Regex(
                    $@"\b{Regex.Escape(name)}\s*=""",
                    RegexOptions.Compiled);

                if (pattern.IsMatch(bodyXml))
                    used.Add(name);
            }

            // ── D. Names used as element TEXT content (OutArgument assignment) ──
            //    e.g.  <OutArgument x:TypeArguments="x:Int32">[out_intinputFileLastRow]</OutArgument>
            //    The name appears as raw text between tags, NOT inside [...] in an
            //    attribute value. XDocument may normalize whitespace so the raw
            //    string scan is the safest approach.
            //    Pattern:  >[name]<  or  >[expr containing name]<
            foreach (string name in declaredNames)
            {
                if (used.Contains(name)) continue;

                // Scan raw text nodes: anything between > and < that contains the name
                var textContent = new Regex(
                    $@">([^<]*\b{Regex.Escape(name)}\b[^<]*)<",
                    RegexOptions.Compiled);

                if (textContent.IsMatch(bodyXml))
                    used.Add(name);
            }

            // ── E. Names appearing anywhere bare in body as a whole-word match ──
            //    Final safety net: if the name appears as a word anywhere in the
            //    body XML, mark as used. This catches edge cases like names in
            //    annotation text. False negatives (wrongly removing used args) are
            //    far worse than false positives (keeping unused args).
            foreach (string name in declaredNames)
            {
                if (used.Contains(name)) continue;

                if (IsWord(name, bodyXml))
                    used.Add(name);
            }

            return used;
        }

        /// <summary>
        /// Returns true when <paramref name="name"/> appears as a complete
        /// identifier token (word-boundary on both sides) inside <paramref name="text"/>.
        /// </summary>
        private static bool IsWord(string name, string text)
        {
            int idx = 0;
            while (true)
            {
                idx = text.IndexOf(name, idx, StringComparison.Ordinal);
                if (idx < 0) return false;

                bool leftOk = idx == 0 ||
                               (!char.IsLetterOrDigit(text[idx - 1]) && text[idx - 1] != '_');
                bool rightOk = idx + name.Length >= text.Length ||
                               (!char.IsLetterOrDigit(text[idx + name.Length]) &&
                                text[idx + name.Length] != '_');

                if (leftOk && rightOk) return true;
                idx++;
            }
        }

        // =====================================================================
        //  XML helpers
        // =====================================================================

        private static void RemoveClean(XElement el)
        {
            var prev = el.PreviousNode as XText;
            if (prev != null && string.IsNullOrWhiteSpace(prev.Value))
                prev.Remove();
            el.Remove();
        }

        private static void CleanEmptyVariableContainers(XDocument doc)
        {
            var empty = doc.Descendants()
                .Where(e => e.Name.LocalName.EndsWith(".Variables") && !e.Elements().Any())
                .ToList();
            foreach (var ec in empty) RemoveClean(ec);
        }

        // =====================================================================
        //  Serialisation
        // =====================================================================

        private static string Serialise(XDocument doc, string original)
        {
            bool crlf = original.Contains("\r\n");
            var sb = new StringBuilder();

            var settings = new XmlWriterSettings
            {
                OmitXmlDeclaration = true,
                Indent = true,
                IndentChars = "  ",
                NewLineChars = crlf ? "\r\n" : "\n",
                NewLineHandling = NewLineHandling.Replace,
            };

            using (var sw = new StringWriter(sb))
            using (var xw = XmlWriter.Create(sw, settings))
                doc.Save(xw);

            string body = sb.ToString();
            if (original.TrimStart().StartsWith("<?xml"))
            {
                int end = original.IndexOf("?>", StringComparison.Ordinal) + 2;
                body = original.Substring(0, end) +
                       (crlf ? "\r\n" : "\n") + body;
            }
            return body;
        }

        // =====================================================================
        //  Data classes
        // =====================================================================

        private class VarDecl
        {
            public string VarName { get; set; }
            public int Depth { get; set; }
            public XElement Element { get; set; }
        }

        private class ArgDecl
        {
            public string ArgName { get; set; }
            public XElement Element { get; set; }
        }
    }
}