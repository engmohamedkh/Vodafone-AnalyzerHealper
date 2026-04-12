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
    /// Scans every &lt;ui:InvokeWorkflowFile&gt; in the current XAML and compares
    /// the argument keys it passes against what the target .xaml file actually
    /// declares in its &lt;x:Members&gt; section.
    ///
    /// What it fixes in the XAML file:
    ///   • MISSING args  – adds an empty &lt;InArgument&gt; / &lt;OutArgument&gt; binding
    ///                     so UiPath no longer shows "needs refresh".
    ///   • EXTRA (stale) args – removes argument entries whose key no longer
    ///                     exists in the target workflow.
    ///
    /// What it reports to the desktop (pure C# StreamWriter — no Python):
    ///   • Empty Invoke Arguments.csv
    ///     One row per argument whose value is empty / self-closing / {x:Null}
    ///     AFTER all add/remove fixes have been applied.
    ///     Columns: Caller File | Invoke Display Name | Workflow File |
    ///              Argument Name | Argument Type | Direction
    ///
    /// The Python / openpyxl section has been removed entirely.
    /// Target files that cannot be found on disk are skipped.
    /// </summary>
    public class SyncInvokeArgumentsAction : IFixerAction
    {
        public string Name => "Sync Invokes Arguments with Workflows";

        public string Description =>
            "Compares every InvokeWorkflowFile's argument keys against the target " +
            "workflow's declared x:Property arguments. Adds missing args (empty binding) " +
            "and removes stale args. Then writes 'Empty Invoke Arguments.csv' to the " +
            "Desktop listing every argument that still has no wired value.";

        // ── Namespaces ────────────────────────────────────────────────────────
        private static readonly XNamespace _actNs =
            XNamespace.Get("http://schemas.microsoft.com/netfx/2009/xaml/activities");
        private static readonly XNamespace _uiNs =
            XNamespace.Get("http://schemas.uipath.com/workflow/activities");
        private static readonly XNamespace _xNs =
            XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml");

        // ─────────────────────────────────────────────────────────────────────
        public bool Run(string filePath)
        {
            string original = File.ReadAllText(filePath);
            string fileDir = Path.GetDirectoryName(filePath) ?? "";
            string callerName = Path.GetFileName(filePath);

            XDocument doc;
            try { doc = XDocument.Parse(original, LoadOptions.PreserveWhitespace); }
            catch { return false; }

            bool docChanged = false;

            // =================================================================
            //  PASS 1 — Fix: add missing args, remove stale args
            // =================================================================
            foreach (var invokeEl in doc.Descendants(_uiNs + "InvokeWorkflowFile").ToList())
            {
                string displayName = GetAttr(invokeEl, "DisplayName") ?? "unnamed";
                string relPath = GetAttr(invokeEl, "WorkflowFileName");
                if (string.IsNullOrWhiteSpace(relPath)) continue;

                // ── Resolve target path ───────────────────────────────────
                string targetAbs = ResolveTarget(fileDir, relPath);
                if (targetAbs == null || !File.Exists(targetAbs)) continue;

                // ── Read what the target workflow declares ─────────────────
                var targetArgs = ReadTargetArgs(targetAbs);
                if (targetArgs == null) continue;

                // ── Read what this invoke currently passes ─────────────────
                var argsContainer = invokeEl
                    .Elements(_uiNs + "InvokeWorkflowFile.Arguments")
                    .FirstOrDefault();

                var currentKeys = new Dictionary<string, XElement>(StringComparer.Ordinal);
                if (argsContainer != null)
                    foreach (var argEl in argsContainer.Elements())
                    {
                        string key = GetAttr(argEl, "Key");
                        if (!string.IsNullOrEmpty(key))
                            currentKeys[key] = argEl;
                    }

                // ── Diff ───────────────────────────────────────────────────
                var targetKeys = new HashSet<string>(targetArgs.Keys, StringComparer.Ordinal);
                var currentSet = new HashSet<string>(currentKeys.Keys, StringComparer.Ordinal);

                var missing = targetKeys.Except(currentSet).OrderBy(k => k).ToList();
                var extra = currentSet.Except(targetKeys).OrderBy(k => k).ToList();

                // ── Fix: add missing args ─────────────────────────────────
                if (missing.Count > 0)
                {
                    if (argsContainer == null)
                    {
                        argsContainer = new XElement(_uiNs + "InvokeWorkflowFile.Arguments");
                        invokeEl.AddFirst(argsContainer);
                    }
                    foreach (string key in missing)
                    {
                        var info = targetArgs[key];
                        string elem = info.IsOut ? "OutArgument" : "InArgument";

                        var newArg = new XElement(_actNs + elem,
                            new XAttribute(_xNs + "TypeArguments", info.XType),
                            new XAttribute(_xNs + "Key", key));

                        argsContainer.Add(new XText("  "));
                        argsContainer.Add(newArg);
                        docChanged = true;
                    }
                }

                // ── Fix: remove stale (extra) args ────────────────────────
                if (extra.Count > 0 && argsContainer != null)
                    foreach (string key in extra)
                        if (currentKeys.TryGetValue(key, out var staleEl))
                        {
                            RemoveClean(staleEl);
                            docChanged = true;
                        }
            }

            // =================================================================
            //  PASS 2 — Report: scan every invoke for empty/unwired arguments
            //
            //  An argument counts as "empty" when:
            //    • It is self-closing  →  <InArgument x:Key="x" />
            //    • Its text content is {x:Null}, empty, or whitespace only
            //    • It has no child elements and no non-whitespace text
            //
            //  Wired example:   <InArgument x:Key="x">[varName]</InArgument>  ← skip
            //  Unwired example: <InArgument x:Key="x" />                      ← report
            // =================================================================
            var csvRows = new List<EmptyCsvRow>();

            foreach (var invokeEl in doc.Descendants(_uiNs + "InvokeWorkflowFile"))
            {
                string displayName = GetAttr(invokeEl, "DisplayName") ?? "unnamed";
                string relPath = GetAttr(invokeEl, "WorkflowFileName") ?? "";

                var argsContainer = invokeEl
                    .Elements(_uiNs + "InvokeWorkflowFile.Arguments")
                    .FirstOrDefault();

                if (argsContainer == null) continue;

                foreach (var argEl in argsContainer.Elements())
                {
                    string key = GetAttr(argEl, "Key");
                    string typeArg = GetAttr(argEl, "TypeArguments") ?? "";
                    string localName = argEl.Name.LocalName;   // InArgument / OutArgument

                    if (string.IsNullOrEmpty(key)) continue;
                    if (!IsEmptyArg(argEl)) continue;   // has a real value → skip

                    string direction = localName.StartsWith("Out", StringComparison.OrdinalIgnoreCase)
                        ? "Out" : "In";

                    csvRows.Add(new EmptyCsvRow
                    {
                        CallerFile = callerName,
                        InvokeName = displayName,
                        WorkflowFile = relPath,
                        ArgName = key,
                        ArgType = typeArg,
                        Direction = direction,
                    });
                }
            }

            // ── Write CSV to Desktop ──────────────────────────────────────
            WriteCsv(csvRows);

            // =================================================================
            //  Save fixed XAML
            // =================================================================
            if (!docChanged) return false;

            string updated = Serialise(doc, original);
            if (updated == original) return false;

            File.WriteAllText(filePath, updated);
            return true;
        }

        // =====================================================================
        //  IsEmptyArg — true when the argument element carries no real value
        // =====================================================================
        private static bool IsEmptyArg(XElement argEl)
        {
            // Has child elements → has a nested binding → not empty
            if (argEl.HasElements) return false;

            string text = argEl.Value;   // empty string for self-closing elements

            if (string.IsNullOrWhiteSpace(text)) return true;
            if (text.Equals("{x:Null}", StringComparison.OrdinalIgnoreCase)) return true;
            if (text.Equals("[]", StringComparison.Ordinal)) return true;

            return false;
        }

        // =====================================================================
        //  WriteCsv — pure C# StreamWriter, no Python, no external libs
        // =====================================================================
        private static void WriteCsv(List<EmptyCsvRow> rows)
        {
            try
            {
                if (rows.Count == 0) return;

                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string path = Path.Combine(desktop, "Empty Invoke Arguments.csv");

                // Append to the file so multiple processed files accumulate
                bool writeHeader = !File.Exists(path);

                using var sw = new StreamWriter(path, append: true, Encoding.UTF8);

                if (writeHeader)
                    sw.WriteLine(CsvRow(
                        "Caller File",
                        "Invoke Display Name",
                        "Workflow File",
                        "Argument Name",
                        "Argument Type",
                        "Direction"));

                foreach (var r in rows)
                    sw.WriteLine(CsvRow(
                        r.CallerFile,
                        r.InvokeName,
                        r.WorkflowFile,
                        r.ArgName,
                        r.ArgType,
                        r.Direction));
            }
            catch { /* report failure must never crash the fixer */ }
        }

        // =====================================================================
        //  Read target workflow's x:Members declarations
        // =====================================================================
        private static Dictionary<string, ArgInfo> ReadTargetArgs(string path)
        {
            string xml;
            try { xml = File.ReadAllText(path); }
            catch { return null; }

            XDocument doc;
            try { doc = XDocument.Parse(xml, LoadOptions.PreserveWhitespace); }
            catch { return null; }

            var result = new Dictionary<string, ArgInfo>(StringComparer.Ordinal);
            var members = doc.Descendants(_xNs + "Members").FirstOrDefault();
            if (members == null) return result;

            foreach (var prop in members.Elements(_xNs + "Property"))
            {
                string name = (string)prop.Attribute("Name");
                string type = (string)prop.Attribute("Type");
                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(type)) continue;

                bool isOut = type.StartsWith("OutArgument", StringComparison.OrdinalIgnoreCase);

                string xType = "x:Object";
                int paren = type.IndexOf('(');
                if (paren >= 0)
                {
                    int close = type.LastIndexOf(')');
                    if (close > paren)
                        xType = type.Substring(paren + 1, close - paren - 1);
                }

                result[name] = new ArgInfo { IsOut = isOut, XType = xType };
            }

            return result;
        }

        // =====================================================================
        //  Helpers
        // =====================================================================
        private static string ResolveTarget(string fileDir, string relPath)
        {
            try
            {
                string norm = relPath.Replace('/', Path.DirectorySeparatorChar)
                                     .Replace('\\', Path.DirectorySeparatorChar);
                return Path.GetFullPath(Path.Combine(fileDir, norm));
            }
            catch { return null; }
        }

        /// <summary>Attribute lookup by local name — namespace-agnostic.</summary>
        private static string GetAttr(XElement el, string localName)
            => el.Attributes()
                 .FirstOrDefault(a => a.Name.LocalName == localName)?.Value;

        private static void RemoveClean(XElement el)
        {
            if (el.PreviousNode is XText t && string.IsNullOrWhiteSpace(t.Value))
                t.Remove();
            el.Remove();
        }

        private static string CsvRow(params string[] fields)
            => string.Join(",", fields.Select(CsvEscape));

        private static string CsvEscape(string s)
        {
            if (s == null) return "\"\"";
            // Always quote every field — safest for values that may contain commas
            return "\"" + s.Replace("\"", "\"\"") + "\"";
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
                body = original.Substring(0, end) + (crlf ? "\r\n" : "\n") + body;
            }
            return body;
        }

        // =====================================================================
        //  Data models
        // =====================================================================
        private class ArgInfo
        {
            public bool IsOut { get; set; }
            public string XType { get; set; }
        }

        private class EmptyCsvRow
        {
            public string CallerFile { get; set; }
            public string InvokeName { get; set; }
            public string WorkflowFile { get; set; }
            public string ArgName { get; set; }
            public string ArgType { get; set; }
            public string Direction { get; set; }
        }
    }
}