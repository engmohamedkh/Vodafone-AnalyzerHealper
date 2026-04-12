using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;

namespace AnalyzerHelper
{
    /// <summary>
    /// Finds all ui:CommentOut elements across XAML files, shows a review dialog,
    /// and applies per-item choices:
    ///
    ///   Sequence context  → Delete | Uncomment | Keep
    ///   Flowchart context → Delete | Uncomment | Keep
    ///
    /// Delete in Flowchart performs a proper linked-list splice:
    ///   predecessor → [CommentOut FlowStep] → successor
    ///   becomes:
    ///   predecessor → successor
    ///
    /// Uncomment in Flowchart replaces the CommentOut activity inside the FlowStep
    /// with the unwrapped inner activity — the FlowStep itself stays in the chain.
    ///
    /// NOTE (fix):
    ///   - When uncommenting in Sequence, we "lift" activities to the OUTER sequence,
    ///     without creating a new sequence.
    ///   - We must not lift sap2010 ViewState property-elements (or any property-elements),
    ///     otherwise UiPath Studio throws:
    ///       'ViewState' property has already been set on 'Sequence'
    ///   - We also post-clean sequences to remove duplicate ViewState property-elements,
    ///     in case the file already contains duplicates (or from previous bad transforms).
    /// </summary>
    public class RemoveCommentedActivitiesAction : IFixerAction
    {
        public string Name => "Review Commented Activities";
        public string Description =>
            "Scans all XAML files for ui:CommentOut blocks, shows a review dialog " +
            "per item (Delete / Uncomment / Keep), then applies chosen actions.";

        public bool NeedsConfig => true;
        public bool Run(string filePath) => false;

        // ── Namespaces ────────────────────────────────────────────────────────
        private static readonly XNamespace UiNs = "http://schemas.uipath.com/workflow/activities";
        private static readonly XNamespace Sap10 = "http://schemas.microsoft.com/netfx/2010/xaml/activities/presentation";
        private static readonly XNamespace XNs = "http://schemas.microsoft.com/winfx/2006/xaml";
        private static readonly XName CoName = UiNs + "CommentOut";
        private static readonly XName BodyN = UiNs + "CommentOut.Body";

        // =====================================================================
        //  RunBatch
        // =====================================================================
        public int RunBatch(string rootFolder)
        {
            var allXaml = Directory.GetFiles(rootFolder, "*.xaml", SearchOption.AllDirectories);

            var items = new List<CommentItem>();
            foreach (string path in allXaml)
                items.AddRange(ScanFile(path, rootFolder));

            if (items.Count == 0)
            {
                MessageBox.Show("No commented-out activities found in any XAML file.",
                    "Nothing to do", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 0;
            }

            using var dlg = new CommentReviewDialog(items);
            if (dlg.ShowDialog() != DialogResult.OK) return 0;

            int modified = 0;
            foreach (var grp in items.GroupBy(i => i.FilePath))
            {
                if (grp.All(i => i.ChosenAction == CommentAction.Keep)) continue;

                string orig;
                try { orig = File.ReadAllText(grp.Key); }
                catch { continue; }

                XDocument doc;
                try { doc = XDocument.Parse(orig, LoadOptions.PreserveWhitespace); }
                catch { continue; }

                bool changed = false;
                foreach (var item in grp)
                {
                    if (item.ChosenAction == CommentAction.Keep) continue;

                    // Re-find the CommentOut by its stable IdRef
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
                            UncommentInSequence(coEl);   // <-- Lifts to outer sequence, no new Sequence
                        changed = true;
                    }
                }

                if (!changed) continue;

                // IMPORTANT FIX: remove duplicate ViewState property-elements on Sequence nodes
                // to prevent UiPath Studio load exception:
                //   'ViewState' property has already been set on 'Sequence'
                DeduplicateSequenceViewState(doc);

                try
                {
                    File.WriteAllText(grp.Key, Serialise(doc, orig));
                    modified++;
                }
                catch { /* skip unwritable */ }
            }

            return modified;
        }

        // =====================================================================
        //  Scan one file
        // =====================================================================
        private static List<CommentItem> ScanFile(string filePath, string rootFolder)
        {
            var result = new List<CommentItem>();

            string xml;
            try { xml = File.ReadAllText(filePath); }
            catch { return result; }

            XDocument doc;
            try { doc = XDocument.Parse(xml, LoadOptions.PreserveWhitespace); }
            catch { return result; }

            string rel = filePath.StartsWith(rootFolder)
                ? filePath.Substring(rootFolder.Length).TrimStart(Path.DirectorySeparatorChar)
                : filePath;

            foreach (var el in doc.Descendants(CoName))
            {
                string idRef = (string)el.Attribute(Sap10 + "WorkflowViewState.IdRef")
                               ?? Guid.NewGuid().ToString();

                bool inFlow = el.Ancestors().Any(a =>
                    a.Name.LocalName == "Flowchart" || a.Name.LocalName == "FlowStep");

                // ── Extract inner activities ─────────────────────────────────
                var inner = ExtractInnerActivities(el);

                result.Add(new CommentItem
                {
                    FilePath = filePath,
                    RelativePath = rel,
                    IdRef = idRef,
                    DisplayName = (string)el.Attribute("DisplayName") ?? "(no name)",
                    InnerSummary = BuildSummary(inner),
                    InvokeTitle = FindAttr(el, "InvokeWorkflowFile", "DisplayName"),
                    WorkflowFile = FindAttr(el, "InvokeWorkflowFile", "WorkflowFileName"),
                    IsInFlowchart = inFlow,
                    ChosenAction = CommentAction.Delete,
                });
            }

            return result;
        }

        // =====================================================================
        //  Inner activity extraction (FIXED)
        //  - Filters out sap2010 designer metadata (ViewState, etc.)
        //  - Filters out XAML property-elements (Sequence.Activities, Sequence.Variables, If.Then, ...)
        //  - When unwrapping "Ignored Activities"/blank Sequence: returns ONLY activities
        // =====================================================================

        private static bool IsDesignerMetadata(XElement e)
            => e?.Name.Namespace == Sap10;

        private static bool IsPropertyElement(XElement e)
            => e != null && e.Name.LocalName.Contains(".");

        private static bool IsActivityElement(XElement e)
        {
            if (e == null) return false;
            if (e.Name.Namespace == Sap10) return false; // viewstate/presentation stuff
            if (e.Name.Namespace == XNs) return false;   // x:* references etc.
            if (IsPropertyElement(e)) return false;      // e.g. Sequence.Activities
            return true;
        }

        private static IEnumerable<XElement> ExtractActivitiesFromSequence(XElement seq)
        {
            if (seq == null) yield break;

            // UiPath often uses <Sequence.Activities> wrapper; if present, use it.
            var actsWrapper = seq.Elements()
                .FirstOrDefault(x => x.Name.LocalName.EndsWith(".Activities"));

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

            // Unwrap single "Ignored Activities" / unnamed Sequence (BUT ONLY ITS ACTIVITIES)
            if (candidates.Count == 1 && candidates[0].Name.LocalName == "Sequence")
            {
                string dn = (string)candidates[0].Attribute("DisplayName") ?? "";
                if (dn == "Ignored Activities" || dn == "")
                {
                    return ExtractActivitiesFromSequence(candidates[0]).ToList();
                }
            }

            // Default: filter out any metadata/property elements
            return candidates.Where(IsActivityElement).ToList();
        }

        private static string BuildSummary(List<XElement> kids)
        {
            if (kids.Count == 0) return "(empty)";
            var names = kids.Select(c =>
            {
                string dn = (string)c.Attribute("DisplayName");
                return string.IsNullOrWhiteSpace(dn) ? c.Name.LocalName : dn;
            }).ToList();
            if (names.Count == 1) return names[0];
            if (names.Count <= 3) return string.Join(", ", names);
            return string.Join(", ", names.Take(2)) + $" (+{names.Count - 2} more)";
        }

        private static string FindAttr(XElement root, string localName, string attr)
        {
            var el = root.Descendants().FirstOrDefault(e => e.Name.LocalName == localName);
            return el == null ? string.Empty
                : (string)el.Attributes().FirstOrDefault(a => a.Name.LocalName == attr) ?? string.Empty;
        }

        // =====================================================================
        //  DELETE — Sequence: just remove the CommentOut element
        // =====================================================================
        private static void DeleteFromSequence(XElement coEl)
        {
            RemoveClean(coEl);
        }

        // =====================================================================
        //  DELETE — Flowchart: linked-list splice
        // =====================================================================
        private static void DeleteFromFlowchart(XDocument doc, XElement coEl)
        {
            // The victim FlowStep contains the CommentOut
            var victim = OwningFlowStep(coEl);
            if (victim == null)
            {
                // CommentOut not directly inside a FlowStep — treat like sequence
                RemoveClean(coEl);
                return;
            }

            string victimName = (string)victim.Attribute(XNs + "Name");

            // ── Get successor ────────────────────────────────────────────────
            XElement nextWrapperEl = victim.Elements()
                .FirstOrDefault(e => e.Name.LocalName == "FlowStep.Next");
            XElement successor = nextWrapperEl?.Elements().FirstOrDefault();

            if (successor != null)
                successor.Remove(); // detach from FlowStep.Next before we splice

            // ── Splice: replace victim with successor in its parent slot ─────
            var parent = victim.Parent; // e.g. FlowDecision.True or FlowStep.Next

            if (successor != null)
                victim.ReplaceWith(successor);
            else
                RemoveClean(victim);

            // If parent is now an empty wrapper element (FlowStep.Next, etc.)
            // and no successor was inserted, also clean up the parent wrapper
            if (successor == null && parent != null && !parent.Elements().Any())
                RemoveClean(parent);

            // ── Remove flat x:Reference for victim ──────────────────────────
            if (!string.IsNullOrEmpty(victimName))
                RemoveFlatReference(doc, victimName);
        }

        // =====================================================================
        //  UNCOMMENT — Sequence (FIXED):
        //  - Lift children into OUTER parent list (no new Sequence)
        //  - Replace CommentOut in-place with extracted activities
        // =====================================================================
        private static void UncommentInSequence(XElement coEl)
        {
            var inner = ExtractInnerActivities(coEl);

            if (inner.Count == 0)
            {
                RemoveClean(coEl);
                return;
            }

            // Detach extracted activities
            var replacement = new List<XElement>(inner.Count);
            foreach (var act in inner)
            {
                act.Remove();
                replacement.Add(act);
            }

            // Replace CommentOut with its activities in-place (push to outer sequence)
            coEl.ReplaceWith(replacement);
        }

        // =====================================================================
        //  UNCOMMENT — Flowchart: replace wrapper with inner activity within FlowStep
        // =====================================================================
        private static void UncommentInFlowchart(XElement coEl)
        {
            var inner = ExtractInnerActivities(coEl);

            if (inner.Count == 0)
            {
                // Nothing inside — just delete the CommentOut wrapper entirely
                RemoveClean(coEl);
                return;
            }

            if (inner.Count == 1)
            {
                // Replace CommentOut with the single inner activity
                var activity = inner[0];
                activity.Remove();
                coEl.ReplaceWith(activity);
            }
            else
            {
                // Multiple inner activities — FlowStep holds one activity.
                // In flowchart we still need a container (existing behavior).
                // If you also want "no new sequence" in flowchart, tell me and we’ll handle differently.
                var seq = new XElement(
                    XName.Get("Sequence", "http://schemas.microsoft.com/netfx/2009/xaml/activities"),
                    new XAttribute("DisplayName", "Uncommented Activities"),
                    new XAttribute(Sap10 + "WorkflowViewState.IdRef",
                        "Sequence_uncomment_" + Guid.NewGuid().ToString("N").Substring(0, 8))
                );
                foreach (var act in inner)
                {
                    act.Remove();
                    seq.Add(act);
                }
                coEl.ReplaceWith(seq);
            }
        }

        // =====================================================================
        //  ViewState de-duplication (CRITICAL FIX)
        //  Prevents:
        //    System.Xaml.XamlDuplicateMemberException:
        //      'ViewState' property has already been set on 'Sequence'
        // =====================================================================
        private static bool IsViewStatePropertyElement(XElement e)
        {
            if (e == null) return false;
            if (e.Name.Namespace != Sap10) return false;

            // Typically local name is "WorkflowViewState.ViewState"
            // We remove duplicates of any sap2010 property element containing "ViewState"
            // (keeps the first one).
            var ln = e.Name.LocalName;
            return ln.Contains("ViewState", StringComparison.OrdinalIgnoreCase) && ln.Contains(".");
        }

        private static void DeduplicateSequenceViewState(XDocument doc)
        {
            foreach (var seq in doc.Descendants().Where(d => d.Name.LocalName == "Sequence"))
            {
                var vsProps = seq.Elements().Where(IsViewStatePropertyElement).ToList();
                if (vsProps.Count <= 1) continue;

                // Keep first, remove the rest
                foreach (var extra in vsProps.Skip(1))
                    RemoveClean(extra);
            }
        }

        // =====================================================================
        //  Helpers
        // =====================================================================
        private static XElement FindCommentOutByIdRef(XDocument doc, string idRef)
            => doc.Descendants(CoName)
                  .FirstOrDefault(e =>
                      (string)e.Attribute(Sap10 + "WorkflowViewState.IdRef") == idRef);

        /// <summary>Returns the FlowStep that directly contains this CommentOut.</summary>
        private static XElement OwningFlowStep(XElement coEl)
        {
            var p = coEl.Parent;
            if (p?.Name.LocalName == "FlowStep") return p;
            return null;
        }

        /// <summary>
        /// Removes the flat <x:Reference>name</x:Reference> element
        /// from the Flowchart body (the de-duplication list at the bottom).
        /// </summary>
        private static void RemoveFlatReference(XDocument doc, string name)
        {
            var refEl = doc.Descendants(XNs + "Reference")
                .FirstOrDefault(e => e.Value.Trim() == name);
            if (refEl != null)
                RemoveClean(refEl);
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

        // =====================================================================
        //  Data model
        // =====================================================================
        internal class CommentItem
        {
            public string FilePath { get; set; }
            public string RelativePath { get; set; }
            public string IdRef { get; set; }
            public string DisplayName { get; set; }
            public string InnerSummary { get; set; }
            public string InvokeTitle { get; set; }
            public string WorkflowFile { get; set; }
            public bool IsInFlowchart { get; set; }
            public CommentAction ChosenAction { get; set; }
        }

        internal enum CommentAction { Delete, Uncomment, Keep }
    }

    // =========================================================================
    //  CommentReviewDialog
    // =========================================================================
    internal class CommentReviewDialog : Form
    {
        private static readonly Color PalBg = Color.White;
        private static readonly Color PalAlt = Color.FromArgb(248, 248, 252);
        private static readonly Color PalHeader = Color.FromArgb(243, 243, 243);
        private static readonly Color PalBorder = Color.FromArgb(220, 220, 220);
        private static readonly Color PalAccent = Color.FromArgb(0, 120, 212);
        private static readonly Color PalRed = Color.FromArgb(180, 0, 0);
        private static readonly Color PalGreen = Color.FromArgb(0, 130, 0);
        private static readonly Color PalGrey = Color.FromArgb(110, 110, 110);
        private static readonly Color PalFlowBadge = Color.FromArgb(210, 105, 0);
        private static readonly Color PalSeqBadge = Color.FromArgb(0, 100, 180);

        private readonly List<RemoveCommentedActivitiesAction.CommentItem> _items;
        private DataGridView _grid;

        public CommentReviewDialog(List<RemoveCommentedActivitiesAction.CommentItem> items)
        {
            _items = items;
            BuildUI();
            PopulateGrid();
        }

        private void BuildUI()
        {
            Text = "Review Commented-Out Activities";
            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1080, 650);
            MinimumSize = new Size(720, 430);
            BackColor = PalBg;
            Font = new Font("Segoe UI", 9.5f);

            // ── Header ───────────────────────────────────────────────────────
            var header = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = PalHeader };
            header.Paint += (s, e) =>
            {
                using var p = new Pen(PalBorder);
                e.Graphics.DrawLine(p, 0, header.Height - 1, header.Width, header.Height - 1);
            };

            int total = _items.Count;
            int inFlow = _items.Count(i => i.IsInFlowchart);
            int inSeq = total - inFlow;

            var hLbl = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(14, 0, 0, 0),
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = Color.FromArgb(40, 40, 40),
                BackColor = Color.Transparent,
                Text = $"{total} commented block(s) found   ·   " +
                       $"{inSeq} in Sequence   ·   {inFlow} in Flowchart",
            };
            header.Controls.Add(hLbl);

            // ── Grid ─────────────────────────────────────────────────────────
            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None,
                RowTemplate = { Height = 26 },
                BackgroundColor = PalBg,
                BorderStyle = BorderStyle.None,
                GridColor = PalBorder,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                Font = new Font("Segoe UI", 9f),
            };

            _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(228, 228, 228);
            _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(30, 30, 30);
            _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            _grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(5, 0, 0, 0);
            _grid.EnableHeadersVisualStyles = false;
            _grid.ColumnHeadersHeight = 28;
            _grid.ColumnHeadersHeightSizeMode =
                DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

            TCol("colFile", "File", 210, false);
            TCol("colCtx", "Context", 75, false);
            TCol("colInvoke", "Invoke Title", 160, false);
            TCol("colWf", "Workflow File", 190, false);
            TCol("colContents", "Contents", 0, true);

            _grid.Columns.Add(new DataGridViewComboBoxColumn
            {
                Name = "colAction",
                HeaderText = "Action",
                Width = 115,
                FlatStyle = FlatStyle.Flat,
                DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton,
            });

            _grid.CellFormatting += OnCellFormatting;
            _grid.CellValueChanged += OnCellValueChanged;
            _grid.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (_grid.IsCurrentCellDirty)
                    _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };

            // ── Bottom bar ───────────────────────────────────────────────────
            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 48, BackColor = PalHeader };
            bottom.Paint += (s, e) =>
            {
                using var p = new Pen(PalBorder);
                e.Graphics.DrawLine(p, 0, 0, bottom.Width, 0);
            };

            var applyBtn = Btn("✔  Apply", 110, true);
            var cancelBtn = Btn("Cancel", 80, false);
            var allDelBtn = Btn("All: Delete", 100, false);
            var allUnBtn = Btn("All: Uncomment", 120, false);
            var allKeepBtn = Btn("All: Keep", 90, false);
            var selDelBtn = Btn("Sel: Delete", 95, false);
            var selKeepBtn = Btn("Sel: Keep", 85, false);

            applyBtn.Click += (s, e) => { DialogResult = DialogResult.OK; Close(); };
            cancelBtn.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            allDelBtn.Click += (s, e) => BulkSet(false, "Delete");
            allUnBtn.Click += (s, e) => BulkSet(false, "Uncomment");
            allKeepBtn.Click += (s, e) => BulkSet(false, "Keep");
            selDelBtn.Click += (s, e) => BulkSet(true, "Delete");
            selKeepBtn.Click += (s, e) => BulkSet(true, "Keep");

            bottom.Controls.AddRange(new Control[]
                { applyBtn, cancelBtn, allDelBtn, allUnBtn, allKeepBtn, selDelBtn, selKeepBtn });

            bottom.Resize += (s, e) =>
            {
                const int pad = 12, gap = 6;
                int y = (bottom.Height - applyBtn.Height) / 2;
                applyBtn.Location = new Point(bottom.Width - pad - applyBtn.Width, y);
                cancelBtn.Location = new Point(applyBtn.Left - gap - cancelBtn.Width, y);
                allDelBtn.Location = new Point(pad, y);
                allUnBtn.Location = new Point(allDelBtn.Right + gap, y);
                allKeepBtn.Location = new Point(allUnBtn.Right + gap, y);
                selDelBtn.Location = new Point(allKeepBtn.Right + gap * 3, y);
                selKeepBtn.Location = new Point(selDelBtn.Right + gap, y);
            };

            Controls.Add(_grid);
            Controls.Add(bottom);
            Controls.Add(header);
            AcceptButton = applyBtn;
            CancelButton = cancelBtn;
        }

        private void TCol(string name, string hdr, int w, bool fill)
        {
            var col = new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = hdr,
                ReadOnly = true,
            };
            col.DefaultCellStyle.Padding = new Padding(5, 0, 0, 0);
            if (fill) col.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            else col.Width = w;
            _grid.Columns.Add(col);
        }

        private void PopulateGrid()
        {
            _grid.Rows.Clear();
            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                int r = _grid.Rows.Add(
                    item.RelativePath,
                    item.IsInFlowchart ? "Flowchart" : "Sequence",
                    string.IsNullOrEmpty(item.InvokeTitle) ? "—" : item.InvokeTitle,
                    string.IsNullOrEmpty(item.WorkflowFile) ? "—" : item.WorkflowFile,
                    item.InnerSummary,
                    "Delete"
                );
                _grid.Rows[r].Tag = item;
                _grid.Rows[r].DefaultCellStyle.BackColor = i % 2 == 0 ? PalBg : PalAlt;
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            _grid.EditingControlShowing += (s, ev) =>
            {
                if (_grid.CurrentCell?.OwningColumn.Name != "colAction") return;
                if (ev.Control is not ComboBox cb) return;

                cb.Items.Clear();
                cb.Items.Add("Delete");
                cb.Items.Add("Uncomment");
                cb.Items.Add("Keep");
            };

            for (int r = 0; r < _grid.Rows.Count; r++)
            {
                var row = _grid.Rows[r];
                if (row.Cells["colAction"] is not DataGridViewComboBoxCell cell) continue;
                cell.Items.Clear();
                cell.Items.Add("Delete");
                cell.Items.Add("Uncomment");
                cell.Items.Add("Keep");
                cell.Value = "Delete";
            }
        }

        private void OnCellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var item = _grid.Rows[e.RowIndex].Tag as RemoveCommentedActivitiesAction.CommentItem;
            if (item == null) return;

            string col = _grid.Columns[e.ColumnIndex].Name;

            if (col == "colCtx")
            {
                e.CellStyle.ForeColor = item.IsInFlowchart ? PalFlowBadge : PalSeqBadge;
                e.CellStyle.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            }
            else if (col == "colAction")
            {
                string val = _grid.Rows[e.RowIndex].Cells["colAction"].Value?.ToString() ?? "Delete";
                e.CellStyle.ForeColor = val switch
                {
                    "Delete" => PalRed,
                    "Uncomment" => PalGreen,
                    _ => PalGrey,
                };
                e.CellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            }
        }

        private void OnCellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex != _grid.Columns["colAction"].Index || e.RowIndex < 0) return;
            var item = _grid.Rows[e.RowIndex].Tag as RemoveCommentedActivitiesAction.CommentItem;
            if (item == null) return;

            string val = _grid.Rows[e.RowIndex].Cells["colAction"].Value?.ToString() ?? "Delete";
            item.ChosenAction = val switch
            {
                "Uncomment" => RemoveCommentedActivitiesAction.CommentAction.Uncomment,
                "Keep" => RemoveCommentedActivitiesAction.CommentAction.Keep,
                _ => RemoveCommentedActivitiesAction.CommentAction.Delete,
            };
            _grid.InvalidateRow(e.RowIndex);
        }

        private void BulkSet(bool selectedOnly, string action)
        {
            var rows = selectedOnly
                ? _grid.SelectedRows.Cast<DataGridViewRow>()
                : _grid.Rows.Cast<DataGridViewRow>();

            foreach (var row in rows)
            {
                var item = row.Tag as RemoveCommentedActivitiesAction.CommentItem;
                if (item == null) continue;

                if (row.Cells["colAction"] is DataGridViewComboBoxCell cell &&
                    cell.Items.Contains(action))
                    cell.Value = action;

                item.ChosenAction = action switch
                {
                    "Uncomment" => RemoveCommentedActivitiesAction.CommentAction.Uncomment,
                    "Keep" => RemoveCommentedActivitiesAction.CommentAction.Keep,
                    _ => RemoveCommentedActivitiesAction.CommentAction.Delete,
                };
                _grid.InvalidateRow(row.Index);
            }
        }

        private static Button Btn(string text, int width, bool primary) => new Button
        {
            Text = text,
            Size = new Size(width, 28),
            FlatStyle = FlatStyle.Flat,
            BackColor = primary ? PalAccent : Color.FromArgb(235, 235, 235),
            ForeColor = primary ? Color.White : Color.FromArgb(30, 30, 30),
            Cursor = Cursors.Hand,
            FlatAppearance =
            {
                BorderSize  = 1,
                BorderColor = primary ? PalAccent : PalBorder,
            },
        };
    }
}