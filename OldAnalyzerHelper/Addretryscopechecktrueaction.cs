using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace AnalyzerHelper
{
    /// <summary>
    /// Finds every &lt;ui:RetryScope.Condition&gt; block whose &lt;ActivityFunc&gt; is
    /// empty (no child elements) and inserts a &lt;ui:CheckTrue Expression="True"/&gt;
    /// inside it.
    ///
    /// Before (needs fixing):
    ///   &lt;ui:RetryScope.Condition&gt;
    ///     &lt;ActivityFunc x:TypeArguments="x:Boolean" /&gt;
    ///   &lt;/ui:RetryScope.Condition&gt;
    ///
    /// After:
    ///   &lt;ui:RetryScope.Condition&gt;
    ///     &lt;ActivityFunc x:TypeArguments="x:Boolean"&gt;
    ///       &lt;ui:CheckTrue ErrorMessage="{x:Null}" DisplayName="Check True"
    ///                     Expression="True"
    ///                     sap:VirtualizedContainerService.HintSize="334,129"
    ///                     sap2010:WorkflowViewState.IdRef="CheckTrue_N" /&gt;
    ///     &lt;/ActivityFunc&gt;
    ///   &lt;/ui:RetryScope.Condition&gt;
    ///
    /// The CheckTrue_N counter continues from the highest N already present in the file.
    /// </summary>
    public class AddRetryScopeCheckTrueAction : IFixerAction
    {
        public string Name => "Add CheckTrue to Empty RetryScope Condition";

        public string Description =>
            "Finds RetryScope activities whose Condition block contains an empty " +
            "ActivityFunc (no CheckTrue or other condition set) and inserts a " +
            "CheckTrue with Expression=\"True\" so the retry loop always evaluates " +
            "correctly. The CheckTrue_N IdRef counter continues from the highest N " +
            "already present in the file.";

        // Finds the highest existing CheckTrue_N counter in the whole file
        private static readonly Regex _checkTrueIdPattern =
            new Regex(@"CheckTrue_(\d+)", RegexOptions.Compiled);

        // Matches a self-closing ActivityFunc inside a RetryScope.Condition that
        // has NO child content — i.e. the empty form:
        //   <ActivityFunc x:TypeArguments="x:Boolean" />
        // We use XML manipulation rather than regex for correctness, but we need
        // the counter from the raw text first.

        public bool Run(string filePath)
        {
            string original = File.ReadAllText(filePath);

            // Quick bail-out: no RetryScope.Condition at all
            if (!original.Contains("RetryScope.Condition"))
                return false;

            // Parse with XDocument for safe structural manipulation
            XDocument doc;
            try { doc = XDocument.Parse(original, LoadOptions.PreserveWhitespace); }
            catch { return false; }

            // Find the highest existing CheckTrue_N so we can continue the sequence
            int nextId = HighestCheckTrueId(original) + 1;
            bool modified = false;

            // Walk every element whose local name is "RetryScope.Condition"
            // (namespace-independent — could be ui:RetryScope.Condition)
            foreach (var condEl in doc.Descendants())
            {
                if (condEl.Name.LocalName != "RetryScope.Condition") continue;

                // Look for a direct-child ActivityFunc
                foreach (var funcEl in condEl.Elements())
                {
                    if (funcEl.Name.LocalName != "ActivityFunc") continue;

                    // Already has children? → already has a condition, skip
                    if (funcEl.HasElements) continue;

                    // It's empty — insert CheckTrue inside it
                    InsertCheckTrue(funcEl, nextId, doc);
                    nextId++;
                    modified = true;
                }
            }

            if (!modified) return false;

            string updated = Serialise(doc, original);
            if (updated == original) return false;

            File.WriteAllText(filePath, updated);
            return true;
        }

        // ── Insert CheckTrue as the only child of an empty ActivityFunc ───────
        private static void InsertCheckTrue(XElement funcEl, int id, XDocument doc)
        {
            // Resolve the "ui" namespace prefix from the document
            // (it is declared on the root as xmlns:ui="http://schemas.uipath.com/workflow/activities")
            XNamespace uiNs = ResolveNamespace(doc, "ui",
                                    "http://schemas.uipath.com/workflow/activities");
            XNamespace sapNs = ResolveNamespace(doc, "sap",
                                    "http://schemas.microsoft.com/netfx/2009/xaml/activities/presentation");
            XNamespace sap10Ns = ResolveNamespace(doc, "sap2010",
                                    "http://schemas.microsoft.com/netfx/2010/xaml/activities/presentation");

            // Build:
            //   <ui:CheckTrue ErrorMessage="{x:Null}" DisplayName="Check True"
            //                 Expression="True"
            //                 sap:VirtualizedContainerService.HintSize="334,129"
            //                 sap2010:WorkflowViewState.IdRef="CheckTrue_N" />
            var checkTrue = new XElement(uiNs + "CheckTrue",
                new XAttribute("ErrorMessage", "{x:Null}"),
                new XAttribute("DisplayName", "Check True"),
                new XAttribute("Expression", "True"),
                new XAttribute(sapNs + "VirtualizedContainerService.HintSize", "334,129"),
                new XAttribute(sap10Ns + "WorkflowViewState.IdRef", $"CheckTrue_{id}")
            );

            // Convert the self-closing <ActivityFunc … /> to an element with a child.
            // XElement.Add() automatically expands self-closing to open/close form.
            funcEl.Add(checkTrue);
        }

        // ── Namespace resolver ────────────────────────────────────────────────
        /// <summary>
        /// Looks up the namespace URI for a given prefix in the document's root
        /// element declarations. Falls back to <paramref name="fallback"/> if not found.
        /// </summary>
        private static XNamespace ResolveNamespace(XDocument doc, string prefix, string fallback)
        {
            if (doc.Root == null) return XNamespace.Get(fallback);

            foreach (var attr in doc.Root.Attributes())
            {
                // xmlns:prefix="uri"
                if (attr.IsNamespaceDeclaration &&
                    attr.Name.LocalName == prefix)
                    return XNamespace.Get(attr.Value);
            }
            return XNamespace.Get(fallback);
        }

        // ── Counter helper ────────────────────────────────────────────────────
        private static int HighestCheckTrueId(string xml)
        {
            int max = 0;
            foreach (Match m in _checkTrueIdPattern.Matches(xml))
                if (int.TryParse(m.Groups[1].Value, out int n) && n > max)
                    max = n;
            return max;
        }

        // ── Serialisation ─────────────────────────────────────────────────────
        private static string Serialise(XDocument doc, string original)
        {
            bool crlf = original.Contains("\r\n");

            var sb = new System.Text.StringBuilder();
            var settings = new XmlWriterSettings
            {
                OmitXmlDeclaration = true,
                Indent = true,
                IndentChars = "  ",
                NewLineChars = crlf ? "\r\n" : "\n",
                NewLineHandling = NewLineHandling.Replace,
            };

            using (var sw = new System.IO.StringWriter(sb))
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