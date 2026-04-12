using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using AnalyzerHelper.Interfaces;
using AnalyzerHelper.Models;

namespace AnalyzerHelper.Rules
{
    /// <summary>
    /// Scans every &lt;ui:InvokeWorkflowFile&gt; in the current XAML and compares
    /// the argument keys it passes against what the target .xaml file actually
    /// declares in its &lt;x:Members&gt; section.
    ///
    /// Fix (DefineAndFix):
    ///   • MISSING args  – adds an empty &lt;InArgument&gt; / &lt;OutArgument&gt; binding
    ///                     so UiPath no longer shows "needs refresh".
    ///   • EXTRA (stale) args – removes argument entries whose key no longer
    ///                     exists in the target workflow.
    ///
    /// Check:
    ///   • One RuleCheckResult row per argument whose value is empty / self-closing / {x:Null}
    ///     AFTER all add/remove fixes would be applied.
    ///     Message includes: Invoke Display Name, Workflow File, Argument Name, Type, Direction.
    /// </summary>
    public sealed class SyncInvokeArgumentsRule : IAnalyzerRuleWithFix
    {
        public string RuleId => "VF-020";
        public string RuleName => "SyncInvokeArguments";
        public string DefaultRecommendation =>
            "Sync InvokeWorkflowFile arguments with the target workflow's declared arguments. " +
            "Wire empty arguments to the appropriate variables.";
        public bool RequiresUserInteraction => false;

        // ── Namespaces ────────────────────────────────────────────────────────
        private static readonly XNamespace _actNs =
            XNamespace.Get("http://schemas.microsoft.com/netfx/2009/xaml/activities");
        private static readonly XNamespace _uiNs =
            XNamespace.Get("http://schemas.uipath.com/workflow/activities");
        private static readonly XNamespace _xNs =
            XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml");

        // =====================================================================
        //  Check  (report only — no mutation)
        //  Returns one result per empty/unwired argument in each invoke,
        //  after simulating the add-missing / remove-stale sync.
        // =====================================================================

        public IReadOnlyList<RuleCheckResult> Check(string filePath, string content)
        {
            var results = new List<RuleCheckResult>();
            if (string.IsNullOrWhiteSpace(content)) return results;

            XDocument doc;
            try { doc = XDocument.Parse(content, LoadOptions.PreserveWhitespace); }
            catch { return results; }

            string fileDir = Path.GetDirectoryName(filePath) ?? "";

            foreach (var invokeEl in doc.Descendants(_uiNs + "InvokeWorkflowFile"))
            {
                string displayName = GetAttr(invokeEl, "DisplayName") ?? "unnamed";
                string relPath = GetAttr(invokeEl, "WorkflowFileName") ?? "";
                if (string.IsNullOrWhiteSpace(relPath)) continue;

                string targetAbs = ResolveTarget(fileDir, relPath);
                if (targetAbs == null || !File.Exists(targetAbs)) continue;

                var targetArgs = ReadTargetArgs(targetAbs);
                if (targetArgs == null) continue;

                // Build what the invoke would look like AFTER sync
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

                var targetKeySet = new HashSet<string>(targetArgs.Keys, StringComparer.Ordinal);
                var currentKeySet = new HashSet<string>(currentKeys.Keys, StringComparer.Ordinal);

                var missing = targetKeySet.Except(currentKeySet).OrderBy(k => k).ToList();
                var extra = currentKeySet.Except(targetKeySet).OrderBy(k => k).ToList();

                // Report missing args (they will become empty bindings after fix)
                foreach (string key in missing)
                {
                    var info = targetArgs[key];
                    string direction = info.IsOut ? "Out" : "In";
                    results.Add(new RuleCheckResult
                    {
                        RuleId = RuleId,
                        RuleName = RuleName,
                        Level = RuleLevel.Warning,
                        Message = $"Empty invoke argument — Invoke: \"{displayName}\", " +
                                  $"Workflow: \"{relPath}\", " +
                                  $"Arg: \"{key}\", Type: {info.XType}, Direction: {direction} (missing, will be added empty).",
                        FilePath = filePath,
                        Recommendation = DefaultRecommendation,
                        RequiresUserInteraction = RequiresUserInteraction
                    });
                }

                // Report currently-existing args that are empty (excluding stale ones)
                foreach (var kvp in currentKeys)
                {
                    string key = kvp.Key;
                    XElement argEl = kvp.Value;

                    // Skip stale args — they'll be removed by fix
                    if (extra.Contains(key)) continue;

                    if (!IsEmptyArg(argEl)) continue;

                    string localName = argEl.Name.LocalName;
                    string typeArg = GetAttr(argEl, "TypeArguments") ?? "";
                    string direction = localName.StartsWith("Out", StringComparison.OrdinalIgnoreCase)
                        ? "Out" : "In";

                    results.Add(new RuleCheckResult
                    {
                        RuleId = RuleId,
                        RuleName = RuleName,
                        Level = RuleLevel.Warning,
                        Message = $"Empty invoke argument — Invoke: \"{displayName}\", " +
                                  $"Workflow: \"{relPath}\", " +
                                  $"Arg: \"{key}\", Type: {typeArg}, Direction: {direction}.",
                        FilePath = filePath,
                        Recommendation = DefaultRecommendation,
                        RequiresUserInteraction = RequiresUserInteraction
                    });
                }

                // Report stale args that will be removed
                foreach (string key in extra)
                {
                    results.Add(new RuleCheckResult
                    {
                        RuleId = RuleId,
                        RuleName = RuleName,
                        Level = RuleLevel.Info,
                        Message = $"Stale invoke argument (will be removed) — Invoke: \"{displayName}\", " +
                                  $"Workflow: \"{relPath}\", Arg: \"{key}\".",
                        FilePath = filePath,
                        Recommendation = DefaultRecommendation,
                        RequiresUserInteraction = RequiresUserInteraction
                    });
                }
            }

            return results;
        }

        // =====================================================================
        //  DefineAndFix  (detect + fix — operates on content string)
        //  Adds missing args (empty binding), removes stale args.
        // =====================================================================

        public bool DefineAndFix(string filePath, string content, out string newContent)
        {
            newContent = content;
            if (string.IsNullOrWhiteSpace(content)) return false;

            XDocument doc;
            try { doc = XDocument.Parse(content, LoadOptions.PreserveWhitespace); }
            catch { return false; }

            string fileDir = Path.GetDirectoryName(filePath) ?? "";
            bool docChanged = false;

            foreach (var invokeEl in doc.Descendants(_uiNs + "InvokeWorkflowFile").ToList())
            {
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
                var targetKeySet = new HashSet<string>(targetArgs.Keys, StringComparer.Ordinal);
                var currentKeySet = new HashSet<string>(currentKeys.Keys, StringComparer.Ordinal);

                var missing = targetKeySet.Except(currentKeySet).OrderBy(k => k).ToList();
                var extra = currentKeySet.Except(targetKeySet).OrderBy(k => k).ToList();

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

            if (!docChanged) return false;

            newContent = Serialise(doc, content);
            return newContent != content;
        }

        // =====================================================================
        //  IsEmptyArg — true when the argument element carries no real value
        // =====================================================================

        private static bool IsEmptyArg(XElement argEl)
        {
            // Has child elements → has a nested binding → not empty
            if (argEl.HasElements) return false;

            string text = argEl.Value; // empty string for self-closing elements

            if (string.IsNullOrWhiteSpace(text)) return true;
            if (text.Equals("{x:Null}", StringComparison.OrdinalIgnoreCase)) return true;
            if (text.Equals("[]", StringComparison.Ordinal)) return true;

            return false;
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
    }
}
