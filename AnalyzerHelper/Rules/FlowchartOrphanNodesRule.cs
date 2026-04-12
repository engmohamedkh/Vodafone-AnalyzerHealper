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
    /// In a Flowchart, FlowStep / FlowDecision / FlowSwitch nodes must be connected to the flow
    /// (reachable from Flowchart.StartNode via Next / True / False / Default / cases).
    /// Orphan nodes (no path from start) are reported; fix removes them automatically.
    /// </summary>
    public sealed class FlowchartOrphanNodesRule : IAnalyzerRuleWithFix
    {
        private static readonly XNamespace XNs = "http://schemas.microsoft.com/winfx/2006/xaml";

        public string RuleId => "VF-040";
        public string RuleName => "Flowchart orphan nodes";
        public string DefaultRecommendation =>
            "Remove disconnected Flowchart nodes or connect them with flow connectors. Auto-fix deletes orphan nodes.";
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
                        RequiresUserInteraction = false
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

            var toRemove = new List<XElement>();
            foreach (var flowchart in doc.Descendants().Where(e => e.Name.LocalName == "Flowchart").ToList())
                toRemove.AddRange(FindOrphanFlowNodes(flowchart));

            if (toRemove.Count == 0) return false;

            foreach (var el in toRemove.OrderByDescending(e => GetElementDepth(e)))
                try { el.Remove(); }
                catch { /* skip */ }

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
                return new List<XElement>();

            var reachable = new HashSet<XElement>();
            var q = new Queue<XElement>();
            q.Enqueue(start);
            reachable.Add(start);

            while (q.Count > 0)
            {
                var n = q.Dequeue();
                foreach (var next in GetOutgoingFlowNodes(n))
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

            if (first.Name.LocalName == "Reference")
            {
                string refId = (first.Value ?? "").Trim();
                if (string.IsNullOrEmpty(refId)) return null;
                return flowchart.Descendants()
                    .Where(IsFlowNodeType)
                    .FirstOrDefault(e => GetXName(e) == refId);
            }

            if (IsFlowNodeType(first)) return first;
            return null;
        }

        /// <summary>Immediate FlowNode targets linked from this node (Next / branches).</summary>
        private static IEnumerable<XElement> GetOutgoingFlowNodes(XElement node)
        {
            string ln = node.Name.LocalName;

            if (ln == "FlowStep")
            {
                foreach (var next in node.Elements().Where(e => e.Name.LocalName == "FlowStep.Next"))
                foreach (var c in next.Elements())
                    if (IsFlowNodeType(c)) yield return c;
                yield break;
            }

            if (ln == "FlowDecision")
            {
                foreach (var branchName in new[] { "FlowDecision.True", "FlowDecision.False" })
                {
                    var branch = node.Elements().FirstOrDefault(e => e.Name.LocalName == branchName);
                    if (branch == null) continue;
                    foreach (var c in branch.Elements())
                        if (IsFlowNodeType(c)) yield return c;
                }
                yield break;
            }

            if (ln == "FlowSwitch")
            {
                var def = node.Elements().FirstOrDefault(e => e.Name.LocalName == "FlowSwitch.Default");
                if (def != null)
                    foreach (var c in def.Elements())
                        if (IsFlowNodeType(c)) yield return c;

                foreach (var c in node.Elements())
                {
                    if (c.Name.LocalName != "FlowStep") continue;
                    if (c.Attribute(XNs + "Key") != null || c.Attribute("Key") != null)
                    {
                        if (IsFlowNodeType(c)) yield return c;
                    }
                }
            }
        }

    }
}
