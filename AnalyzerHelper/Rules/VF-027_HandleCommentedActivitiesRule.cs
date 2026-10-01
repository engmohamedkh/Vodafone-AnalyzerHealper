using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Xml;
using System.Xml.Linq;
using AnalyzerHelper.Interfaces;
using AnalyzerHelper.Models;
using AnalyzerHelper.View;

namespace AnalyzerHelper.Rules
{
    /// <summary>
    /// Need-interaction rule: reports Error when commented-out (ui:CommentOut) activities are found.
    /// Fix shows a dialog to choose per activity: Delete or Uncomment (or Keep).
    /// Logic from OldAnalyzerHelper.RemoveCommentedActivitiesAction.
    /// </summary>
    public sealed class HandleCommentedActivitiesRule : IBatchAnalyzerRuleWithFix
    {
        // Studio CommentOutActivity is VF-027; VF-015 is BusinessSystemException (validate-only).
        public string RuleId => "VF-027";
        public string RuleName => "HandleCommentedActivities";
        public string DefaultRecommendation =>
            "Commented-out activities found. Use the Fix tab and choose for each: Delete (remove) or Uncomment (restore).";
        public bool RequiresUserInteraction => true;

        private static readonly XNamespace UiNs = "http://schemas.uipath.com/workflow/activities";
        private static readonly XNamespace Sap10 = "http://schemas.microsoft.com/netfx/2010/xaml/activities/presentation";
        private static readonly XNamespace XNs = "http://schemas.microsoft.com/winfx/2006/xaml";
        private static readonly XName CoName = UiNs + "CommentOut";
        private static readonly XName BodyN = UiNs + "CommentOut.Body";

        public IReadOnlyList<RuleCheckResult> Check(string filePath, string content)
        {
            var results = new List<RuleCheckResult>();
            if (string.IsNullOrWhiteSpace(content)) return results;

            XDocument doc;
            try { doc = XDocument.Parse(content, LoadOptions.PreserveWhitespace); }
            catch { return results; }

            var commentOuts = doc.Descendants(CoName).ToList();
            if (commentOuts.Count == 0) return results;

            results.Add(new RuleCheckResult
            {
                RuleId = RuleId,
                RuleName = RuleName,
                Level = RuleLevel.Error,
                Message = $"Commented-out activities found ({commentOuts.Count}). Use Fix to Delete or Uncomment each.",
                FilePath = filePath,
                Recommendation = DefaultRecommendation,
                RequiresUserInteraction = RequiresUserInteraction
            });
            return results;
        }

        private Dictionary<string, List<CommentItem>> _batchItems = null;
        private bool _batchCancelled = false;

        /// <summary>Finds a root folder common to all paths for relative display.</summary>
        private static string FindCommonRoot(IReadOnlyList<string> paths)
        {
            if (paths == null || paths.Count == 0) return "";
            var first = Path.GetDirectoryName(paths[0]) ?? "";
            foreach (var p in paths)
            {
                var dir = Path.GetDirectoryName(p) ?? "";
                while (!string.IsNullOrEmpty(first) && !dir.StartsWith(first, StringComparison.OrdinalIgnoreCase))
                    first = Path.GetDirectoryName(first) ?? "";
            }
            return first;
        }

        public void PrepareBatch(IReadOnlyList<string> filePaths)
        {
            _batchItems = new Dictionary<string, List<CommentItem>>();
            _batchCancelled = false;
            var allItems = new List<CommentItem>();
            string rootFolder = FindCommonRoot(filePaths);

            foreach (var path in filePaths)
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) continue;
                try
                {
                    var content = File.ReadAllText(path);
                    var doc = XDocument.Parse(content, LoadOptions.PreserveWhitespace);
                    var items = ScanContent(doc, path, rootFolder);
                    if (items.Count > 0)
                        allItems.AddRange(items);
                }
                catch { }
            }

            if (allItems.Count > 0)
            {
                var dialog = new CommentReviewWindow(allItems, "Batch Review");
                bool? result = dialog.ShowDialog();

                // If user cancelled or closed the dialog, don't apply anything
                if (result != true || !dialog.Applied)
                {
                    _batchCancelled = true;
                    _batchItems = null;
                    throw new OperationCanceledException($"User cancelled the {RuleName} batch fix dialog.");
                }

                foreach (var item in allItems)
                {
                    if (!_batchItems.ContainsKey(item.FilePath))
                        _batchItems[item.FilePath] = new List<CommentItem>();
                    _batchItems[item.FilePath].Add(item);
                }
            }
        }

        public bool DefineAndFix(string filePath, string content, out string newContent)
        {
            newContent = content;
            if (_batchCancelled) return false;
            if (string.IsNullOrWhiteSpace(content)) return false;

            XDocument doc;
            try { doc = XDocument.Parse(content, LoadOptions.PreserveWhitespace); }
            catch { return false; }

            List<CommentItem> items;
            if (_batchItems != null)
            {
                if (!_batchItems.TryGetValue(filePath, out items))
                    return false;
            }
            else
            {
                items = ScanContent(doc, filePath, Path.GetDirectoryName(filePath) ?? "");
                if (items.Count == 0) return false;

                var dialog = new CommentReviewWindow(items, filePath);
                if (dialog.ShowDialog() != true) throw new OperationCanceledException($"User cancelled the {RuleName} fix dialog.");
            }

            bool changed = false;
            // Sort deepest-first so nested CommentOut elements are handled before their parents
            var sorted = items.OrderByDescending(i => i.Depth).ToList();
            foreach (var item in sorted)
            {
                if (item.ChosenAction == CommentAction.Keep) continue;
                var coEl = FindCommentOutByIdRef(doc, item.IdRef);
                if (coEl == null) continue;

                if (item.ChosenAction == CommentAction.Delete)
                {
                    if (item.IsInFlowchart)
                        DeleteFromFlowchart(doc, coEl);
                    else
                        DeleteFromSequence(coEl);
                    changed = true;
                }
                else if (item.ChosenAction == CommentAction.Uncomment)
                {
                    if (item.IsInFlowchart)
                        UncommentInFlowchart(coEl);
                    else
                        UncommentInSequence(coEl);
                    changed = true;
                }
            }

            if (!changed) return false;
            DeduplicateSequenceViewState(doc);
            newContent = Serialise(doc, content);
            return true;
        }

        // ── Scan (build items from document) ──────────────────────────────────

        private static List<CommentItem> ScanContent(XDocument doc, string filePath, string rootFolder)
        {
            string rel = !string.IsNullOrEmpty(rootFolder) && filePath.StartsWith(rootFolder, StringComparison.OrdinalIgnoreCase)
                ? filePath.Substring(rootFolder.Length).TrimStart(Path.DirectorySeparatorChar, '/', '\\')
                : Path.GetFileName(filePath);

            var result = new List<CommentItem>();
            foreach (var el in doc.Descendants(CoName))
            {
                string idRef = (string?)el.Attribute(Sap10 + "WorkflowViewState.IdRef")
                    ?? Guid.NewGuid().ToString();
                bool inFlow = el.Ancestors().Any(a =>
                    a.Name.LocalName == "Flowchart" || a.Name.LocalName == "FlowStep");
                int depth = el.Ancestors().Count(a => a.Name.LocalName == "CommentOut");
                var inner = ExtractInnerActivities(el);
                result.Add(new CommentItem
                {
                    FilePath = filePath,
                    RelativePath = rel,
                    IdRef = idRef,
                    DisplayName = (string?)el.Attribute("DisplayName") ?? "(no name)",
                    InnerSummary = BuildSummary(inner),
                    InvokeTitle = FindAttr(el, "InvokeWorkflowFile", "DisplayName"),
                    WorkflowFile = FindAttr(el, "InvokeWorkflowFile", "WorkflowFileName"),
                    IsInFlowchart = inFlow,
                    Depth = depth,
                    ChosenAction = CommentAction.Delete
                });
            }
            return result;
        }

        private static string FindAttr(XElement root, string localName, string attr)
        {
            var el = root.Descendants().FirstOrDefault(e => e.Name.LocalName == localName);
            return el == null ? string.Empty
                : (string?)el.Attributes().FirstOrDefault(a => a.Name.LocalName == attr) ?? string.Empty;
        }

        private static bool IsDesignerMetadata(XElement e) => e?.Name.Namespace == Sap10;
        private static bool IsPropertyElement(XElement e) => e != null && e.Name.LocalName.Contains(".");
        private static bool IsActivityElement(XElement e)
        {
            if (e == null) return false;
            if (e.Name.Namespace == Sap10) return false;
            if (e.Name.Namespace == XNs) return false;
            if (IsPropertyElement(e)) return false;
            return true;
        }

        private static IEnumerable<XElement> ExtractActivitiesFromSequence(XElement seq)
        {
            if (seq == null) yield break;
            var actsWrapper = seq.Elements().FirstOrDefault(x => x.Name.LocalName.EndsWith(".Activities"));
            var source = actsWrapper != null ? actsWrapper.Elements() : seq.Elements();
            foreach (var el in source.Where(IsActivityElement))
                yield return el;
        }

        private static List<XElement> ExtractInnerActivities(XElement coEl)
        {
            var bodyEl = coEl.Element(BodyN);
            var candidates = bodyEl != null
                ? bodyEl.Elements().ToList()
                : coEl.Elements().Where(e => e.Name != BodyN).ToList();
            if (candidates.Count == 1 && candidates[0].Name.LocalName == "Sequence")
            {
                string dn = (string?)candidates[0].Attribute("DisplayName") ?? "";
                if (dn == "Ignored Activities" || dn == "")
                    return ExtractActivitiesFromSequence(candidates[0]).ToList();
            }
            return candidates.Where(IsActivityElement).ToList();
        }

        private static string BuildSummary(List<XElement> kids)
        {
            if (kids.Count == 0) return "(empty)";
            var names = kids.Select(c =>
            {
                string? dn = (string?)c.Attribute("DisplayName");
                return string.IsNullOrWhiteSpace(dn) ? c.Name.LocalName : dn;
            }).ToList();
            if (names.Count == 1) return names[0];
            if (names.Count <= 3) return string.Join(", ", names);
            return string.Join(", ", names.Take(2)) + $" (+{names.Count - 2} more)";
        }

        // ── Delete / Uncomment (same logic as OldAnalyzerHelper) ───────────────

        private static void DeleteFromSequence(XElement coEl) => RemoveClean(coEl);

        private static void DeleteFromFlowchart(XDocument doc, XElement coEl)
        {
            var victim = OwningFlowStep(coEl);
            if (victim == null) { RemoveClean(coEl); return; }
            string? victimName = (string?)victim.Attribute(XNs + "Name");
            var nextWrapperEl = victim.Elements().FirstOrDefault(e => e.Name.LocalName == "FlowStep.Next");
            XElement? successor = nextWrapperEl?.Elements().FirstOrDefault();
            if (successor != null) successor.Remove();
            var parent = victim.Parent;
            if (successor != null)
                victim.ReplaceWith(successor);
            else
                RemoveClean(victim);
            if (successor == null && parent != null && !parent.Elements().Any())
                RemoveClean(parent);
            if (!string.IsNullOrEmpty(victimName))
                RemoveFlatReference(doc, victimName);
        }

        private static void UncommentInSequence(XElement coEl)
        {
            var inner = ExtractInnerActivities(coEl);
            if (inner.Count == 0) { RemoveClean(coEl); return; }
            var replacement = new List<XElement>(inner.Count);
            foreach (var act in inner) { act.Remove(); replacement.Add(act); }
            coEl.ReplaceWith(replacement);
        }

        private static void UncommentInFlowchart(XElement coEl)
        {
            var inner = ExtractInnerActivities(coEl);
            if (inner.Count == 0) { RemoveClean(coEl); return; }
            if (inner.Count == 1)
            {
                var activity = inner[0];
                activity.Remove();
                coEl.ReplaceWith(activity);
            }
            else
            {
                var seq = new XElement(
                    XName.Get("Sequence", "http://schemas.microsoft.com/netfx/2009/xaml/activities"),
                    new XAttribute("DisplayName", "Uncommented Activities"),
                    new XAttribute(Sap10 + "WorkflowViewState.IdRef",
                        "Sequence_uncomment_" + Guid.NewGuid().ToString("N").Substring(0, 8)));
                foreach (var act in inner) { act.Remove(); seq.Add(act); }
                coEl.ReplaceWith(seq);
            }
        }

        private static void DeduplicateSequenceViewState(XDocument doc)
        {
            foreach (var seq in doc.Descendants().Where(d => d.Name.LocalName == "Sequence"))
            {
                var vsProps = seq.Elements().Where(IsViewStatePropertyElement).ToList();
                if (vsProps.Count <= 1) continue;
                foreach (var extra in vsProps.Skip(1))
                    RemoveClean(extra);
            }
        }

        private static bool IsViewStatePropertyElement(XElement e)
        {
            if (e == null || e.Name.Namespace != Sap10) return false;
            var ln = e.Name.LocalName;
            return ln.Contains("ViewState", StringComparison.OrdinalIgnoreCase) && ln.Contains(".");
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static XElement? FindCommentOutByIdRef(XDocument doc, string idRef)
            => doc.Descendants(CoName).FirstOrDefault(e =>
                (string?)e.Attribute(Sap10 + "WorkflowViewState.IdRef") == idRef);

        private static XElement? OwningFlowStep(XElement coEl)
        {
            var p = coEl.Parent;
            return p?.Name.LocalName == "FlowStep" ? p : null;
        }

        private static void RemoveFlatReference(XDocument doc, string name)
        {
            var refEl = doc.Descendants(XNs + "Reference").FirstOrDefault(e => e.Value.Trim() == name);
            if (refEl != null) RemoveClean(refEl);
        }

        private static void RemoveClean(XElement el)
        {
            if (el == null) return;
            var prev = el.PreviousNode as XText;
            el.Remove();
            if (prev != null && string.IsNullOrWhiteSpace(prev.Value))
                prev.Remove();
        }

        private static string Serialise(XDocument doc, string original)
        {
            bool crlf = original.Contains("\r\n");
            var sb = new StringBuilder();
            var s = new XmlWriterSettings
            {
                OmitXmlDeclaration = true,
                Indent = true,
                IndentChars = "  ",
                NewLineChars = crlf ? "\r\n" : "\n",
                NewLineHandling = NewLineHandling.Replace,
            };
            using (var sw = new StringWriter(sb))
            using (var xw = XmlWriter.Create(sw, s))
                doc.Save(xw);
            string body = sb.ToString();
            if (original.TrimStart().StartsWith("<?xml"))
            {
                int end = original.IndexOf("?>", StringComparison.Ordinal);
                if (end >= 0) end += 2;
                if (end > 1)
                    body = original.Substring(0, end) + (crlf ? "\r\n" : "\n") + body;
            }
            return body;
        }
    }

    /// <summary>Per commented-activity item for the review dialog.</summary>
    public class CommentItem
    {
        public string FilePath { get; set; } = "";
        public string RelativePath { get; set; } = "";
        public string IdRef { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string InnerSummary { get; set; } = "";
        public string InvokeTitle { get; set; } = "";
        public string WorkflowFile { get; set; } = "";
        public bool IsInFlowchart { get; set; }
        /// <summary>Nesting depth — 0 = top level, 1 = inside another CommentOut, etc.</summary>
        public int Depth { get; set; }
        public CommentAction ChosenAction { get; set; }
    }

    /// <summary>User choice for each commented activity.</summary>
    public enum CommentAction { Delete, Uncomment, Keep }
}
