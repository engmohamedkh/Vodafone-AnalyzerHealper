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
    /// FlowStep / FlowDecision / FlowSwitch must be reachable from StartNode (Next / True / False /
    /// Default / cases, including x:Reference links). Auto-fix removes each disconnected subtree once
    /// (root only) and deletes sibling x:Reference entries that pointed at removed nodes.
    /// </summary>
    public sealed class FlowchartOrphanNodesRule : IAnalyzerRuleWithFix
    {
        private static readonly XNamespace XNs = "http://schemas.microsoft.com/winfx/2006/xaml";

        public string RuleId => "VF-040";
        public string RuleName => "Unreachable Flowchart Nodes";
        public string DefaultRecommendation =>
            "Remove unreachable nodes from the Flowchart. Use Fix to auto-delete orphan nodes.";
        public bool RequiresUserInteraction => false;

        public IReadOnlyList<RuleCheckResult> Check(string filePath, string content)
        {
            var results = new List<RuleCheckResult>();
            if (string.IsNullOrWhiteSpace(content)) return results;

            XDocument doc;
            try { doc = XDocument.Parse(content, LoadOptions.PreserveWhitespace); }
            catch { return results; }

            foreach (var flowchart in doc.Descendants().Where(e => e.Name.LocalName == "Flowchart"))
            {
                foreach (var orphan in FindOrphanFlowNodes(flowchart))
                {
                    string dn = (string?)orphan.Attribute("DisplayName")
                        ?? GetXName(orphan)
                        ?? orphan.Name.LocalName;
                    results.Add(new RuleCheckResult
                    {
                        RuleId = RuleId,
                        RuleName = RuleName,
                        Level = RuleLevel.Warning,
                        Message = $"Flowchart contains a disconnected node (not linked from Start): {dn} ({orphan.Name.LocalName}).",
                        FilePath = filePath,
                        Recommendation = DefaultRecommendation,
                        RequiresUserInteraction = RequiresUserInteraction
                    });
                }
            }

            return results;
        }

        public bool DefineAndFix(string filePath, string content, out string newContent)
        {
            newContent = content;
            if (string.IsNullOrWhiteSpace(content)) return false;

            XDocument doc;
            try { doc = XDocument.Parse(content, LoadOptions.PreserveWhitespace); }
            catch { return false; }

            bool docChanged = false;
            foreach (var flowchart in doc.Descendants().Where(e => e.Name.LocalName == "Flowchart").ToList())
            {
                var orphans = FindOrphanFlowNodes(flowchart);
                if (orphans.Count == 0) continue;

                var orphanSet = orphans.ToHashSet();
                // Remove one root per disconnected subgraph; removing a root drops all nested flow
                // nodes, so we must not call Remove() on descendants (detached nodes / errors).
                var roots = orphans.Where(o => !HasOrphanAncestorInSet(o, orphanSet)).ToList();
                var removedIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (var root in roots.OrderByDescending(GetElementDepth))
                {
                    foreach (var fn in root.DescendantsAndSelf().Where(IsFlowNodeType))
                    {
                        string? id = GetXName(fn);
                        if (!string.IsNullOrEmpty(id))
                            removedIds.Add(id);
                    }

                    try
                    {
                        root.Remove();
                        docChanged = true;
                    }
                    catch { /* skip */ }
                }

                if (RemoveDanglingFlowchartReferences(flowchart, removedIds))
                    docChanged = true;
            }

            if (!docChanged) return false;

            try
            {
                newContent = SerializeDoc(doc, content);
                return true;
            }
            catch
            {
                newContent = content;
                return false;
            }
        }

        private static bool HasOrphanAncestorInSet(XElement node, HashSet<XElement> orphanSet)
        {
            for (var p = node.Parent; p != null; p = p.Parent)
            {
                if (p is XElement px && orphanSet.Contains(px))
                    return true;
            }
            return false;
        }

        /// <summary>Removes top-level &lt;x:Reference&gt;…&lt;/x:Reference&gt; under the flowchart whose target was removed.</summary>
        private static bool RemoveDanglingFlowchartReferences(XElement flowchart, HashSet<string> removedIds)
        {
            if (removedIds.Count == 0) return false;
            bool any = false;
            foreach (var child in flowchart.Elements().ToList())
            {
                if (child.Name.LocalName != "Reference") continue;
                string id = (child.Value ?? "").Trim();
                if (!removedIds.Contains(id)) continue;
                if (child.PreviousNode is XText t && string.IsNullOrWhiteSpace(t.Value))
                    t.Remove();
                child.Remove();
                any = true;
            }
            return any;
        }

        private static int GetElementDepth(XElement e)
        {
            int d = 0;
            for (var x = e; x.Parent != null; x = x.Parent) d++;
            return d;
        }

        private static string SerializeDoc(XDocument doc, string originalContent)
        {
            bool crlf = originalContent.Contains("\r\n");
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

            string result = sb.ToString();
            if (originalContent.TrimStart().StartsWith("<?xml", StringComparison.Ordinal))
            {
                int end = originalContent.IndexOf("?>", StringComparison.Ordinal) + 2;
                if (end >= 2)
                    result = originalContent.Substring(0, end) + (crlf ? "\r\n" : "\n") + result;
            }
            return result;
        }

        /// <summary>FlowNodes in this flowchart that are not reachable from StartNode.</summary>
        internal static List<XElement> FindOrphanFlowNodes(XElement flowchart)
        {
            var all = flowchart.Descendants()
                .Where(IsFlowNodeType)
                .ToList();

            if (all.Count == 0) return new List<XElement>();

            var start = GetStartFlowNode(flowchart);
            if (start == null)
            {
                // Start unset (<x:Null />): direct-child flow nodes are not on an entry path.
                return all.Where(n => n.Parent == flowchart).ToList();
            }

            var reachable = new HashSet<XElement>();
            var q = new Queue<XElement>();
            q.Enqueue(start);
            reachable.Add(start);

            while (q.Count > 0)
            {
                var n = q.Dequeue();
                foreach (var next in GetOutgoingFlowNodes(flowchart, n))
                {
                    if (next != null && all.Contains(next) && reachable.Add(next))
                        q.Enqueue(next);
                }
            }

            return all.Where(n => !reachable.Contains(n)).ToList();
        }

        private static bool IsFlowNodeType(XElement e)
        {
            string ln = e.Name.LocalName;
            return ln == "FlowStep" || ln == "FlowDecision" || ln == "FlowSwitch";
        }

        private static string? GetXName(XElement e) =>
            (string?)e.Attribute(XNs + "Name") ?? (string?)e.Attribute("Name");

        private static XElement? GetStartFlowNode(XElement flowchart)
        {
            var startWrapper = flowchart.Elements()
                .FirstOrDefault(e => e.Name.LocalName == "Flowchart.StartNode");
            if (startWrapper == null) return null;

            var first = startWrapper.Elements().FirstOrDefault();
            if (first == null) return null;

            if (first.Name.LocalName == "Null")
                return null;

            if (first.Name.LocalName == "Reference")
                return ResolveReferenceToFlowNode(flowchart, first);

            if (IsFlowNodeType(first)) return first;
            return null;
        }

        /// <summary>Immediate FlowNode targets (Next / True / False / Default / cases); follows x:Reference.</summary>
        private static IEnumerable<XElement> GetOutgoingFlowNodes(XElement flowchart, XElement node)
        {
            string ln = node.Name.LocalName;

            if (ln == "FlowStep")
            {
                foreach (var next in node.Elements().Where(e => e.Name.LocalName == "FlowStep.Next"))
                    foreach (var t in EnumerateBranchFlowNodes(flowchart, next))
                        yield return t;
                yield break;
            }

            if (ln == "FlowDecision")
            {
                foreach (var branchName in new[] { "FlowDecision.True", "FlowDecision.False" })
                {
                    var branch = node.Elements().FirstOrDefault(e => e.Name.LocalName == branchName);
                    if (branch == null) continue;
                    foreach (var t in EnumerateBranchFlowNodes(flowchart, branch))
                        yield return t;
                }
                yield break;
            }

            if (ln == "FlowSwitch")
            {
                var def = node.Elements().FirstOrDefault(e => e.Name.LocalName == "FlowSwitch.Default");
                if (def != null)
                    foreach (var t in EnumerateBranchFlowNodes(flowchart, def))
                        yield return t;

                foreach (var c in node.Elements())
                {
                    if (c.Name.LocalName != "FlowStep") continue;
                    if (c.Attribute(XNs + "Key") != null || c.Attribute("Key") != null)
                    {
                        if (IsFlowNodeType(c))
                            yield return c;
                    }
                }
            }
        }

        private static IEnumerable<XElement> EnumerateBranchFlowNodes(XElement flowchart, XElement branchContainer)
        {
            foreach (var c in branchContainer.Elements())
            {
                if (IsFlowNodeType(c))
                {
                    yield return c;
                    continue;
                }

                var resolved = ResolveReferenceToFlowNode(flowchart, c);
                if (resolved != null)
                    yield return resolved;
            }
        }

        private static XElement? ResolveReferenceToFlowNode(XElement flowchart, XElement el)
        {
            if (el.Name.LocalName != "Reference") return null;
            string refId = (el.Value ?? "").Trim();
            if (string.IsNullOrEmpty(refId)) return null;
            return flowchart.Descendants()
                .Where(IsFlowNodeType)
                .FirstOrDefault(e => GetXName(e) == refId);
        }

    }
}
