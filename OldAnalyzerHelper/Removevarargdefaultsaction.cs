using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace AnalyzerHelper
{
    /// <summary>
    /// Removes default values from Variable and Argument declarations,
    /// except for a set of exempt names that are expected to carry defaults.
    ///
    /// Handles all three forms found in UiPath XAML files:
    ///
    ///   Form A – Default as XML attribute on the Variable tag:
    ///     &lt;Variable Name="boolFlag" Default="False" /&gt;
    ///     → attribute removed → &lt;Variable Name="boolFlag" /&gt;
    ///
    ///   Form B – Default as child &lt;Variable.Default&gt; / &lt;Literal&gt; element:
    ///     &lt;Variable Name="boolFlag"&gt;
    ///       &lt;Variable.Default&gt;
    ///         &lt;Literal x:TypeArguments="x:Boolean"&gt;False&lt;/Literal&gt;
    ///       &lt;/Variable.Default&gt;
    ///     &lt;/Variable&gt;
    ///     → Variable.Default child removed; Variable collapsed to self-closing
    ///
    ///   Form C – Argument companion element (default value for an x:Property arg):
    ///     &lt;this:ClassName.in_intTimeoutM&gt;
    ///       &lt;InArgument x:TypeArguments="x:Int32"&gt;42&lt;/InArgument&gt;
    ///     &lt;/this:ClassName.in_intTimeoutM&gt;
    ///     → companion element removed entirely
    ///     (Empty InArgument / OutArgument with no content is NOT a real default
    ///      and is left alone — that is handled by CleanDefaultValues.)
    ///
    /// Exempt names (case-insensitive substring match):
    ///   strComponentName, strWorkflowName, strProcessIdentifier,
    ///   dctmailTemplates, crddctroboCred, dctselector,
    ///   booldctroboBool, intdctroboInt, dctroboText
    ///
    /// A name is exempt if any of the exempt keywords appears as a
    /// case-insensitive substring of the variable/argument name.
    /// This catches prefixed variants like "in_dctRoboText", "in_booldctRoboBool" etc.
    /// </summary>
    public class RemoveVarArgDefaultsAction : IFixerAction
    {
        public string Name => "Remove Variables / Arguments Default Values";

        public string Description =>
            "Removes default values from Variable and Argument declarations. " +
            "Handles both the inline Default=\"...\" attribute form and the " +
            "<Variable.Default><Literal> child-element form, as well as companion " +
            "<this:ClassName.argName> elements. " +
            "Exempt names (strComponentName, strWorkflowName, strProcessIdentifier, " +
            "dctmailTemplates, crddctroboCred, dctselector, booldctroboBool, " +
            "intdctroboInt, dctroboText) are never modified.";

        // ── Exempt keywords — case-insensitive substring match ────────────────
        private static readonly string[] _exemptKeywords =
        {
            "strcomponentname",
            "strworkflowname",
            "strprocessidentifier",
            "dctmailtemplates",
            "crddctrobocred",
            "dctselector",
            "booldctrobobool",
            "intdctroboint",
            "dctrobotext",
        };

        // ── Namespaces ────────────────────────────────────────────────────────
        private static readonly XNamespace _xNs =
            XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml");

        // ─────────────────────────────────────────────────────────────────────
        public bool Run(string filePath)
        {
            string original = File.ReadAllText(filePath);

            XDocument doc;
            try { doc = XDocument.Parse(original, LoadOptions.PreserveWhitespace); }
            catch { return false; }

            bool changed = false;

            // ── Pass 1: Variables – Form A (Default attribute) ────────────────
            changed |= RemoveVariableDefaultAttributes(doc);

            // ── Pass 2: Variables – Form B (Variable.Default child element) ───
            changed |= RemoveVariableDefaultChildren(doc);

            // ── Pass 3: Arguments – Form C (companion child element of root) ──
            changed |= RemoveArgumentCompanions(doc);

            // ── Pass 4: Arguments – Form D (this:ClassName.argName attribute) ─
            //    e.g. <Activity ... this:MyClass.in_intTimeoutL="456" ...>
            //    The default is stored directly as an attribute on the root
            //    Activity element using the "this:" (clr-namespace:) namespace.
            changed |= RemoveActivityAttributeDefaults(doc);

            if (!changed) return false;

            string updated = Serialise(doc, original);
            if (updated == original) return false;

            File.WriteAllText(filePath, updated);
            return true;
        }

        // =====================================================================
        //  Pass 1 – Remove Default="..." attribute from Variable tags
        // =====================================================================

        private static bool RemoveVariableDefaultAttributes(XDocument doc)
        {
            bool changed = false;

            foreach (var varEl in doc.Descendants()
                .Where(e => e.Name.LocalName == "Variable"))
            {
                string name = varEl.Attributes()
                    .FirstOrDefault(a => a.Name.LocalName == "Name")?.Value;

                if (IsExempt(name)) continue;

                var defaultAttr = varEl.Attributes()
                    .FirstOrDefault(a => a.Name.LocalName == "Default");

                if (defaultAttr == null) continue;

                defaultAttr.Remove();
                changed = true;
            }

            return changed;
        }

        // =====================================================================
        //  Pass 2 – Remove <Variable.Default> child element
        //
        //  Before:
        //    <Variable Name="strFlag">
        //      <Variable.Default>
        //        <Literal x:TypeArguments="x:Boolean">False</Literal>
        //      </Variable.Default>
        //    </Variable>
        //
        //  After:
        //    <Variable Name="strFlag" />
        // =====================================================================

        private static bool RemoveVariableDefaultChildren(XDocument doc)
        {
            bool changed = false;

            foreach (var varEl in doc.Descendants()
                .Where(e => e.Name.LocalName == "Variable"))
            {
                string name = varEl.Attributes()
                    .FirstOrDefault(a => a.Name.LocalName == "Name")?.Value;

                if (IsExempt(name)) continue;

                var defaultChild = varEl.Elements()
                    .FirstOrDefault(e => e.Name.LocalName == "Variable.Default");

                if (defaultChild == null) continue;

                RemoveClean(defaultChild);

                // If the Variable element is now empty, collapse any leftover
                // whitespace-only text content so it serialises as self-closing
                if (!varEl.HasElements)
                {
                    // Remove any whitespace-only text nodes left inside
                    var emptyTexts = varEl.Nodes()
                        .OfType<XText>()
                        .Where(t => string.IsNullOrWhiteSpace(t.Value))
                        .ToList();
                    foreach (var t in emptyTexts) t.Remove();
                }

                changed = true;
            }

            return changed;
        }

        // =====================================================================
        //  Pass 3 – Remove companion <this:ClassName.argName> elements
        //
        //  These are direct children of the root Activity element whose local
        //  name follows the pattern "ClassName.argName" (contains a dot).
        //  They hold default values for x:Property arguments.
        //
        //  A companion is only removed if it contains a REAL default — i.e. its
        //  child InArgument/OutArgument has actual content (child elements or
        //  non-whitespace text). An empty InArgument is noise handled elsewhere.
        //
        //  Example of a REAL default (remove):
        //    <this:MyClass.in_strSomething>
        //      <InArgument x:TypeArguments="x:String">
        //        <Literal ...>SomeValue</Literal>
        //      </InArgument>
        //    </this:MyClass.in_strSomething>
        //
        //  Example of empty companion (leave alone — CleanDefaultValues handles it):
        //    <this:MyClass.in_intTimeoutM>
        //      <InArgument x:TypeArguments="x:Int32" />
        //    </this:MyClass.in_intTimeoutM>
        // =====================================================================

        private static bool RemoveArgumentCompanions(XDocument doc)
        {
            if (doc.Root == null) return false;

            bool changed = false;

            var toRemove = new List<XElement>();

            foreach (var child in doc.Root.Elements())
            {
                string localName = child.Name.LocalName; // e.g. "ClassName.argName"
                int dot = localName.LastIndexOf('.');
                if (dot < 0) continue;

                string argName = localName.Substring(dot + 1);

                if (IsExempt(argName)) continue;

                // Only remove if it contains a real default value
                if (!HasRealDefault(child)) continue;

                toRemove.Add(child);
            }

            foreach (var el in toRemove)
            {
                RemoveClean(el);
                changed = true;
            }

            return changed;
        }

        /// <summary>
        /// Returns true when the companion element contains a non-trivial default:
        /// its InArgument/OutArgument child has child elements or non-whitespace text.
        /// </summary>
        private static bool HasRealDefault(XElement companionEl)
        {
            var argEl = companionEl.Elements().FirstOrDefault(e =>
                e.Name.LocalName == "InArgument" ||
                e.Name.LocalName == "OutArgument");

            if (argEl == null) return false;

            // Real default = has child elements OR non-whitespace text content
            return argEl.HasElements || !string.IsNullOrWhiteSpace(argEl.Value);
        }

        // =====================================================================
        //  Exempt check
        // =====================================================================

        // =====================================================================
        //  Pass 4 – Remove this:ClassName.argName attributes on the root Activity
        //
        //  UiPath stores argument default values as attributes directly on the
        //  root <Activity> element using the "this:" namespace prefix, e.g.:
        //
        //    <Activity ... this:MyClass.in_intTimeoutL="456" ...>
        //
        //  The attribute's XML namespace URI is "clr-namespace:" (the value of
        //  xmlns:this) and its LocalName is "ClassName.argName".
        //  We extract the arg name as everything after the last dot.
        // =====================================================================

        private static bool RemoveActivityAttributeDefaults(XDocument doc)
        {
            if (doc.Root == null) return false;

            // The "this:" namespace URI — always "clr-namespace:" in UiPath files
            const string thisNs = "clr-namespace:";

            var toRemove = doc.Root.Attributes()
                .Where(a =>
                    a.Name.NamespaceName == thisNs &&
                    a.Name.LocalName.Contains('.'))
                .Select(a =>
                {
                    int dot = a.Name.LocalName.LastIndexOf('.');
                    string arg = a.Name.LocalName.Substring(dot + 1);
                    return (attr: a, argName: arg);
                })
                .Where(x => !IsExempt(x.argName))
                .Select(x => x.attr)
                .ToList();

            foreach (var attr in toRemove)
                attr.Remove();

            return toRemove.Count > 0;
        }

        /// <summary>
        /// Returns true if <paramref name="varName"/> contains any of the exempt
        /// keywords as a case-insensitive substring.
        /// e.g. "in_dctRoboText" contains "dctrobotext" → exempt.
        /// </summary>
        private static bool IsExempt(string varName)
        {
            if (string.IsNullOrEmpty(varName)) return false;

            string lower = varName.ToLowerInvariant();
            foreach (var keyword in _exemptKeywords)
                if (lower.Contains(keyword))
                    return true;

            return false;
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
                int end = original.IndexOf("?>") + 2;
                body = original.Substring(0, end) +
                       (crlf ? "\r\n" : "\n") + body;
            }
            return body;
        }
    }
}