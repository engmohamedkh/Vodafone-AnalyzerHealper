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
    /// VF-020 — Keep InvokeWorkflowFile argument lists in sync with the invoked XAML.
    ///
    /// Autofix (refresh):
    ///   • Target workflow gained new x:Property args → add empty bindings on the invoke
    ///   • Target workflow removed args → remove stale bindings from the invoke
    ///
    /// Validate / report:
    ///   • Out-of-sync invoke (missing or stale keys) → Error
    ///   • Argument present but not assigned (empty / {x:Null}) → Error so developer wires it
    /// </summary>
    public sealed class SyncInvokeArgumentsRule : IAnalyzerRuleWithFix
    {
        public string RuleId => "VF-020";
        public string RuleName => "SyncInvokeArguments";
        public string DefaultRecommendation =>
            "Refresh InvokeWorkflowFile arguments to match the target workflow, then wire any empty arguments.";
        public bool RequiresUserInteraction => false;

        private static readonly XNamespace ActNs =
            XNamespace.Get("http://schemas.microsoft.com/netfx/2009/xaml/activities");
        private static readonly XNamespace UiNs =
            XNamespace.Get("http://schemas.uipath.com/workflow/activities");
        private static readonly XNamespace XNs =
            XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml");

        public IReadOnlyList<RuleCheckResult> Check(string filePath, string content)
        {
            var results = new List<RuleCheckResult>();
            if (string.IsNullOrWhiteSpace(content)) return results;
            if (content.IndexOf("InvokeWorkflowFile", StringComparison.OrdinalIgnoreCase) < 0)
                return results;

            if (!TryLoad(content, out var doc) || doc == null)
                return results;

            string fileDir = Path.GetDirectoryName(filePath) ?? "";

            foreach (var invokeEl in EnumerateInvokes(doc))
            {
                string displayName = GetAttr(invokeEl, "DisplayName") ?? "unnamed";
                string relPath = GetAttr(invokeEl, "WorkflowFileName") ?? "";
                if (string.IsNullOrWhiteSpace(relPath)) continue;

                string? targetAbs = ResolveTarget(fileDir, relPath);
                if (targetAbs == null || !File.Exists(targetAbs))
                {
                    results.Add(MakeResult(filePath, RuleLevel.Warning,
                        $"Invoke \"{displayName}\" target workflow not found: \"{relPath}\".",
                        "Check WorkflowFileName path relative to the project root (project.json)."));
                    continue;
                }

                var targetArgs = ReadTargetArgs(targetAbs);
                if (targetArgs == null)
                {
                    results.Add(MakeResult(filePath, RuleLevel.Warning,
                        $"Invoke \"{displayName}\" — could not read arguments from \"{relPath}\".",
                        "Ensure the target XAML is valid and declares arguments in x:Members."));
                    continue;
                }

                var currentKeys = ReadInvokeArgElements(invokeEl);
                var targetKeySet = new HashSet<string>(targetArgs.Keys, StringComparer.Ordinal);
                var currentKeySet = new HashSet<string>(currentKeys.Keys, StringComparer.Ordinal);

                var missing = targetKeySet.Except(currentKeySet).OrderBy(k => k).ToList();
                var extra = currentKeySet.Except(targetKeySet).OrderBy(k => k).ToList();

                // Out of sync with saved target — needs refresh (autofix)
                foreach (string key in missing)
                {
                    var info = targetArgs[key];
                    results.Add(MakeResult(filePath, RuleLevel.Error,
                        $"Invoke out of sync — missing arg on invoke — Invoke: \"{displayName}\", " +
                        $"Workflow: \"{relPath}\", Arg: \"{key}\", Type: {info.XType}, Direction: {info.Direction}. " +
                        "Target was updated; refresh the invoke (autofix), then assign a value.",
                        "Apply autofix to sync arguments, then wire the new argument."));
                }

                foreach (string key in extra)
                {
                    results.Add(MakeResult(filePath, RuleLevel.Error,
                        $"Invoke out of sync — stale arg on invoke — Invoke: \"{displayName}\", " +
                        $"Workflow: \"{relPath}\", Arg: \"{key}\". " +
                        "Argument no longer exists on the target workflow.",
                        "Apply autofix to remove the stale argument."));
                }

                // Present on invoke but not assigned — developer must fill (not autofilled with a value)
                foreach (var kvp in currentKeys)
                {
                    if (extra.Contains(kvp.Key)) continue; // will be removed by sync
                    if (!IsEmptyArg(kvp.Value)) continue;

                    string typeArg = GetAttr(kvp.Value, "TypeArguments") ?? "";
                    string direction = DirectionFromElement(kvp.Value);
                    if (targetArgs.TryGetValue(kvp.Key, out var info))
                    {
                        typeArg = string.IsNullOrEmpty(typeArg) ? info.XType : typeArg;
                        direction = info.Direction;
                    }

                    results.Add(MakeResult(filePath, RuleLevel.Error,
                        $"Missing argument assignment — Invoke: \"{displayName}\", " +
                        $"Workflow: \"{relPath}\", Arg: \"{kvp.Key}\", Type: {typeArg}, Direction: {direction}.",
                        "Open the invoke and assign a variable/value to this argument."));
                }
            }

            return results;
        }

        public bool DefineAndFix(string filePath, string content, out string newContent)
        {
            newContent = content;
            if (string.IsNullOrWhiteSpace(content)) return false;
            if (content.IndexOf("InvokeWorkflowFile", StringComparison.OrdinalIgnoreCase) < 0)
                return false;

            if (!TryLoad(content, out var doc) || doc == null)
                return false;

            string fileDir = Path.GetDirectoryName(filePath) ?? "";
            bool docChanged = false;

            foreach (var invokeEl in EnumerateInvokes(doc).ToList())
            {
                string? relPath = GetAttr(invokeEl, "WorkflowFileName");
                if (string.IsNullOrWhiteSpace(relPath)) continue;

                string? targetAbs = ResolveTarget(fileDir, relPath!);
                if (targetAbs == null || !File.Exists(targetAbs)) continue;

                var targetArgs = ReadTargetArgs(targetAbs);
                if (targetArgs == null) continue;

                var argsContainer = GetOrCreateArgsContainer(invokeEl);
                var currentKeys = ReadInvokeArgElements(invokeEl);

                var targetKeySet = new HashSet<string>(targetArgs.Keys, StringComparer.Ordinal);
                var currentKeySet = new HashSet<string>(currentKeys.Keys, StringComparer.Ordinal);

                var missing = targetKeySet.Except(currentKeySet).OrderBy(k => k).ToList();
                var extra = currentKeySet.Except(targetKeySet).OrderBy(k => k).ToList();

                // Add missing (refresh)
                XElement insertParent = GetArgInsertParent(argsContainer);
                foreach (string key in missing)
                {
                    var info = targetArgs[key];
                    string elemName = info.ElementName; // InArgument / OutArgument / InOutArgument

                    var newArg = new XElement(ActNs + elemName,
                        new XAttribute(XNs + "TypeArguments", info.XType),
                        new XAttribute(XNs + "Key", key));

                    insertParent.Add(new XText(Environment.NewLine + "  "));
                    insertParent.Add(newArg);
                    docChanged = true;
                }

                // Remove stale
                foreach (string key in extra)
                {
                    if (currentKeys.TryGetValue(key, out var staleEl))
                    {
                        RemoveClean(staleEl);
                        docChanged = true;
                    }
                }
            }

            if (!docChanged) return false;

            newContent = Serialise(doc, content);
            return newContent != content;
        }

        // ── Invoke / argument discovery (LocalName — resilient to xmlns prefix) ──

        private static IEnumerable<XElement> EnumerateInvokes(XDocument doc) =>
            doc.Descendants().Where(e =>
                e.Name.LocalName.Equals("InvokeWorkflowFile", StringComparison.OrdinalIgnoreCase));

        private static XElement? FindArgsContainer(XElement invokeEl) =>
            invokeEl.Elements().FirstOrDefault(e =>
                e.Name.LocalName.Equals("InvokeWorkflowFile.Arguments", StringComparison.OrdinalIgnoreCase));

        private static XElement GetOrCreateArgsContainer(XElement invokeEl)
        {
            var existing = FindArgsContainer(invokeEl);
            if (existing != null) return existing;

            var created = new XElement(UiNs + "InvokeWorkflowFile.Arguments");
            invokeEl.AddFirst(created);
            return created;
        }

        /// <summary>Parent that directly holds InArgument/OutArgument (container or inner Dictionary).</summary>
        private static XElement GetArgInsertParent(XElement argsContainer)
        {
            var dict = argsContainer.Elements()
                .FirstOrDefault(e => e.Name.LocalName.Equals("Dictionary", StringComparison.OrdinalIgnoreCase));
            return dict ?? argsContainer;
        }

        private static Dictionary<string, XElement> ReadInvokeArgElements(XElement invokeEl)
        {
            var result = new Dictionary<string, XElement>(StringComparer.Ordinal);
            var argsContainer = FindArgsContainer(invokeEl);
            if (argsContainer == null) return result;

            foreach (var argEl in EnumerateArgElements(argsContainer))
            {
                string? key = GetAttr(argEl, "Key");
                if (!string.IsNullOrEmpty(key))
                    result[key!] = argEl;
            }

            return result;
        }

        private static IEnumerable<XElement> EnumerateArgElements(XElement argsContainer)
        {
            foreach (var child in argsContainer.Elements())
            {
                if (IsArgumentElement(child))
                {
                    yield return child;
                    continue;
                }

                // Newer layouts wrap arguments in scg:Dictionary
                if (child.Name.LocalName.Equals("Dictionary", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var nested in child.Elements().Where(IsArgumentElement))
                        yield return nested;
                }
            }
        }

        private static bool IsArgumentElement(XElement el)
        {
            string ln = el.Name.LocalName;
            return ln.Equals("InArgument", StringComparison.OrdinalIgnoreCase)
                || ln.Equals("OutArgument", StringComparison.OrdinalIgnoreCase)
                || ln.Equals("InOutArgument", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsEmptyArg(XElement argEl)
        {
            if (argEl.HasElements) return false;

            string text = argEl.Value;
            if (string.IsNullOrWhiteSpace(text)) return true;
            if (text.Equals("{x:Null}", StringComparison.OrdinalIgnoreCase)) return true;
            if (text.Equals("[]", StringComparison.Ordinal)) return true;
            return false;
        }

        private static string DirectionFromElement(XElement argEl)
        {
            string ln = argEl.Name.LocalName;
            if (ln.StartsWith("InOut", StringComparison.OrdinalIgnoreCase)) return "InOut";
            if (ln.StartsWith("Out", StringComparison.OrdinalIgnoreCase)) return "Out";
            return "In";
        }

        // ── Target workflow x:Members ─────────────────────────────────────────

        /// <summary>Null = unreadable / no Members section (do not treat as empty arg list).</summary>
        private static Dictionary<string, ArgInfo>? ReadTargetArgs(string path)
        {
            string xml;
            try { xml = File.ReadAllText(path); }
            catch { return null; }

            if (!TryLoad(xml, out var doc) || doc == null)
                return null;

            var members = doc.Descendants()
                .FirstOrDefault(e => e.Name.LocalName.Equals("Members", StringComparison.OrdinalIgnoreCase));
            if (members == null)
                return null;

            var result = new Dictionary<string, ArgInfo>(StringComparer.Ordinal);
            foreach (var prop in members.Elements()
                         .Where(e => e.Name.LocalName.Equals("Property", StringComparison.OrdinalIgnoreCase)))
            {
                string? name = GetAttr(prop, "Name");
                string? type = GetAttr(prop, "Type");
                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(type)) continue;

                result[name!] = ArgInfo.FromPropertyType(type!);
            }

            return result;
        }

        // ── Path resolution ───────────────────────────────────────────────────

        private static string? ResolveTarget(string fileDir, string relPath)
        {
            try
            {
                string norm = relPath.Replace('/', Path.DirectorySeparatorChar)
                                     .Replace('\\', Path.DirectorySeparatorChar);

                string? projectRoot = FindProjectRoot(fileDir);
                string baseDir = projectRoot ?? fileDir;

                string resolved = Path.GetFullPath(Path.Combine(baseDir, norm));
                if (File.Exists(resolved)) return resolved;

                if (projectRoot != null)
                {
                    string fallback = Path.GetFullPath(Path.Combine(fileDir, norm));
                    if (File.Exists(fallback)) return fallback;
                }

                return resolved;
            }
            catch { return null; }
        }

        private static string? FindProjectRoot(string startDir)
        {
            string? dir = startDir;
            while (!string.IsNullOrEmpty(dir))
            {
                if (File.Exists(Path.Combine(dir, "project.json")))
                    return dir;
                string? parent = Path.GetDirectoryName(dir);
                if (parent == dir) break;
                dir = parent;
            }
            return null;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private RuleCheckResult MakeResult(string filePath, RuleLevel level, string message, string recommendation) =>
            new RuleCheckResult
            {
                RuleId = RuleId,
                RuleName = RuleName,
                Level = level,
                Message = message,
                FilePath = filePath,
                Recommendation = recommendation,
                RequiresUserInteraction = RequiresUserInteraction
            };

        private static string? GetAttr(XElement el, string localName) =>
            el.Attributes().FirstOrDefault(a => a.Name.LocalName == localName)?.Value;

        private static void RemoveClean(XElement el)
        {
            if (el.PreviousNode is XText t && string.IsNullOrWhiteSpace(t.Value))
                t.Remove();
            el.Remove();
        }

        private static bool TryLoad(string content, out XDocument? doc)
        {
            try
            {
                doc = XDocument.Parse(content, LoadOptions.PreserveWhitespace);
                return true;
            }
            catch
            {
                doc = null;
                return false;
            }
        }

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
            if (original.TrimStart().StartsWith("<?xml", StringComparison.Ordinal))
            {
                int end = original.IndexOf("?>", StringComparison.Ordinal) + 2;
                if (end >= 2)
                    body = original.Substring(0, end) + (crlf ? "\r\n" : "\n") + body;
            }
            return body;
        }

        private sealed class ArgInfo
        {
            public string Direction { get; init; } = "In";
            public string ElementName { get; init; } = "InArgument";
            public string XType { get; init; } = "x:Object";

            public static ArgInfo FromPropertyType(string type)
            {
                string direction = "In";
                string element = "InArgument";
                if (type.StartsWith("InOutArgument", StringComparison.OrdinalIgnoreCase))
                {
                    direction = "InOut";
                    element = "InOutArgument";
                }
                else if (type.StartsWith("OutArgument", StringComparison.OrdinalIgnoreCase))
                {
                    direction = "Out";
                    element = "OutArgument";
                }

                string xType = "x:Object";
                int paren = type.IndexOf('(');
                if (paren >= 0)
                {
                    int close = type.LastIndexOf(')');
                    if (close > paren)
                        xType = type.Substring(paren + 1, close - paren - 1);
                }

                return new ArgInfo { Direction = direction, ElementName = element, XType = xType };
            }
        }
    }
}
