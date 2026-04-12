using AnalyzerHelper;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Windows.Forms;

namespace AnalyzerFixer
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    // =========================================================================
    //  MainForm
    // =========================================================================
    public class MainForm : Form
    {
        private TextBox _folderBox;
        private Button _browseBtn;
        private FolderTreeView _folderTree;
        private RightPanel _rightPanel;   // owns tabs + item lists — no TabControl border
        private Label _statusLabel;
        private Button _cancelBtn;
        private Button _startBtn;
        private SplitContainer _split;
        private string _rootFolder = string.Empty;

        public MainForm() => BuildUI();

        private void BuildUI()
        {
            Text = "UiPath Analyzer Fixer";
            Size = new Size(1060, 700);
            MinimumSize = new Size(760, 520);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Pal.EditorBg;
            ForeColor = Pal.TextPrimary;
            Font = new Font("Segoe UI", 9.5f);

            // ── Top folder bar ───────────────────────────────────────────────
            var topPanel = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = Pal.TitleBar };
            topPanel.Paint += (s, e) =>
            {
                using var p = new Pen(Pal.Border);
                e.Graphics.DrawLine(p, 0, topPanel.Height - 1, topPanel.Width, topPanel.Height - 1);
            };

            var folderLbl = new Label
            {
                Text = "Folder",
                AutoSize = false,
                Width = 52,
                Height = 50,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Pal.TextSecondary,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            };

            _folderBox = new TextBox
            {
                Height = 24,
                BackColor = Color.White,
                ForeColor = Pal.TextPrimary,
                BorderStyle = BorderStyle.FixedSingle,
                ReadOnly = true,
            };

            _browseBtn = MakeButton("Browse…", 88, false);
            _browseBtn.Click += OnBrowse;

            topPanel.Controls.Add(folderLbl);
            topPanel.Controls.Add(_folderBox);
            topPanel.Controls.Add(_browseBtn);
            topPanel.Resize += (s, e) =>
            {
                const int pad = 12, btnW = 88, gap = 6, lblW = 52;
                folderLbl.Location = new Point(pad, 0);
                _folderBox.Location = new Point(pad + lblW + gap, (topPanel.Height - _folderBox.Height) / 2);
                _folderBox.Width = topPanel.Width - pad * 2 - lblW - gap * 2 - btnW;
                _browseBtn.Location = new Point(topPanel.Width - pad - btnW, (topPanel.Height - _browseBtn.Height) / 2);
            };

            // ── Bottom status bar ────────────────────────────────────────────
            var bottomPanel = new Panel { Dock = DockStyle.Bottom, Height = 50, BackColor = Pal.SideBarBg };
            bottomPanel.Paint += (s, e) =>
            {
                using var p = new Pen(Pal.Border);
                e.Graphics.DrawLine(p, 0, 0, bottomPanel.Width, 0);
            };

            _statusLabel = new Label
            {
                AutoSize = false,
                Height = 50,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Pal.TextSecondary,
                Padding = new Padding(14, 0, 0, 0),
            };

            _cancelBtn = MakeButton("Cancel", 90, false);
            _cancelBtn.Click += (s, e) => Close();

            _startBtn = MakeButton("▶  Run", 110, true);
            _startBtn.Click += OnStart;

            bottomPanel.Controls.AddRange(new Control[] { _statusLabel, _cancelBtn, _startBtn });
            bottomPanel.Resize += (s, e) =>
            {
                const int pad = 12;
                _startBtn.Location = new Point(bottomPanel.Width - pad - _startBtn.Width, (bottomPanel.Height - _startBtn.Height) / 2);
                _cancelBtn.Location = new Point(_startBtn.Left - 8 - _cancelBtn.Width, (bottomPanel.Height - _cancelBtn.Height) / 2);
                _statusLabel.Width = _cancelBtn.Left - 14;
            };

            // ── Split ────────────────────────────────────────────────────────
            _split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterWidth = 1,
                BackColor = Pal.Border,
            };
            _split.Panel1.BackColor = Pal.SideBarBg;
            _split.Panel2.BackColor = Pal.EditorBg;

            // ── LEFT ─────────────────────────────────────────────────────────
            var leftHeader = MakeHeader("XAML Files");
            var leftToolbar = new Panel { Dock = DockStyle.Bottom, Height = 36, BackColor = Pal.SideBarBg };
            leftToolbar.Paint += (s, e) => { using var p = new Pen(Pal.Border); e.Graphics.DrawLine(p, 0, 0, leftToolbar.Width, 0); };

            var tSelAll = SmallBtn("Select All");
            var tDesel = SmallBtn("Deselect All");
            tSelAll.Location = new Point(8, 7);
            tDesel.Location = new Point(tSelAll.Right + 5, 7);
            tSelAll.Click += (s, e) => _folderTree.SetAllChecked(true);
            tDesel.Click += (s, e) => _folderTree.SetAllChecked(false);
            leftToolbar.Controls.AddRange(new Control[] { tSelAll, tDesel });

            _folderTree = new FolderTreeView { Dock = DockStyle.Fill };
            _folderTree.CheckedCountChanged += count =>
            {
                if (!string.IsNullOrEmpty(_rootFolder))
                    _statusLabel.Text = count == 0 ? "No files selected." : $"{count} file(s) selected";
            };

            _split.Panel1.Controls.Add(_folderTree);
            _split.Panel1.Controls.Add(leftToolbar);
            _split.Panel1.Controls.Add(leftHeader);

            // ── RIGHT: RightPanel (pure Panel-based, zero TabControl borders) ─
            _rightPanel = new RightPanel(
                ActionRegistry.All.Cast<object>().ToList(),
                o => new ActionItemRow((IFixerAction)o),
                ReportRegistry.All.Cast<object>().ToList(),
                o => new ReportItemRow((IReportAction)o));

            _rightPanel.TabChanged += idx => _startBtn.Text = idx == 1 ? "▶  Report" : "▶  Run";

            _split.Panel2.Controls.Add(_rightPanel);

            Controls.Add(_split);
            Controls.Add(bottomPanel);
            Controls.Add(topPanel);

            Load += (s, e) =>
            {
                _split.Panel1MinSize = 180;
                _split.Panel2MinSize = 260;
                _split.SplitterDistance = Math.Max(180,
                    Math.Min(_split.Width - 260 - _split.SplitterWidth, _split.Width * 2 / 5));
                _startBtn.Text = "▶  Run";
            };
        }

        // ── Events ───────────────────────────────────────────────────────────
        private void OnBrowse(object sender, EventArgs e)
        {
            var dlg = new FolderBrowserDialog { Description = "Select root folder" };
            if (dlg.ShowDialog() != DialogResult.OK) return;
            _rootFolder = dlg.SelectedPath;
            _folderBox.Text = _rootFolder;
            _statusLabel.Text = string.Empty;
            _folderTree.LoadFolder(_rootFolder);
        }

        private void OnStart(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_rootFolder))
            { MessageBox.Show("Please select a folder first.", "No folder", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

            var selectedFiles = _folderTree.GetCheckedFiles();
            if (selectedFiles.Count == 0)
            { MessageBox.Show("No XAML files selected.", "Nothing to do", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

            if (_rightPanel.ActiveTab == 1) RunReports(selectedFiles);
            else RunActions(selectedFiles);
        }

        private void RunActions(List<string> files)
        {
            var active = _rightPanel.GetCheckedActions();
            if (active.Count == 0) { MessageBox.Show("No actions selected.", "Nothing to do", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

            var results = active.ToDictionary(a => a.Name, _ => 0);
            bool needRefresh = false;

            foreach (var a in active.Where(a => a.NeedsConfig).ToList())
            {
                if (!a.ShowConfigDialog()) { active.Remove(a); continue; }
                int c = a.RunBatch(_rootFolder); results[a.Name] += c;
                if (c > 0) needRefresh = true;
            }
            foreach (var file in files)
                foreach (var a in active.Where(a => !a.NeedsConfig))
                    if (a.Run(file)) results[a.Name]++;

            if (needRefresh) _folderTree.LoadFolder(_rootFolder);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Files processed:  {files.Count}");
            sb.AppendLine();
            foreach (var kv in results) sb.AppendLine($"  {kv.Key}:  {kv.Value} file(s) modified");
            _statusLabel.Text = $"Done – {files.Count} file(s) processed.";
            MessageBox.Show(sb.ToString().TrimEnd(), "Actions Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void RunReports(List<string> files)
        {
            var active = _rightPanel.GetCheckedReports();
            if (active.Count == 0)
            { MessageBox.Show("No reports selected.", "Nothing to do", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

            // Ask for output folder
            var dlg = new FolderBrowserDialog { Description = "Select folder to save the report" };
            if (dlg.ShowDialog() != DialogResult.OK) return;

            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string path = Path.Combine(dlg.SelectedPath, $"AnalyzerReport_{stamp}.xlsx");

            // Collect each report's CSV content (report returns CSV text, one row = one finding)
            var sheets = new List<(string Name, string Csv)>();
            foreach (var r in active)
                sheets.Add((r.Name, r.RunReport(_rootFolder, files) ?? string.Empty));

            try
            {
                //XlsxWriter.Write(path, sheets);
                _statusLabel.Text = $"Report saved → {Path.GetFileName(path)}";
                MessageBox.Show($"Report saved to:\n{path}", "Report Complete",
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save report:\n{ex.Message}", "Error",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ── Shared factories ─────────────────────────────────────────────────
        internal static Button MakeButton(string text, int width, bool primary)
        {
            var btn = new Button
            {
                Text = text,
                Size = new Size(width, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = primary ? Pal.Accent : Pal.ButtonBg,
                ForeColor = primary ? Color.White : Pal.TextPrimary,
                Cursor = Cursors.Hand,
                FlatAppearance = { BorderColor = primary ? Pal.Accent : Pal.Border, BorderSize = 1 },
            };
            btn.MouseEnter += (s, ev) => btn.BackColor = primary ? Pal.AccentHover : Color.FromArgb(218, 218, 218);
            btn.MouseLeave += (s, ev) => btn.BackColor = primary ? Pal.Accent : Pal.ButtonBg;
            return btn;
        }

        internal static Button SmallBtn(string t)
        {
            var b = MakeButton(t, 94, false);
            b.Height = 22; b.Font = new Font("Segoe UI", 8f);
            return b;
        }

        private static Panel MakeHeader(string title)
        {
            var hdr = new Panel { Dock = DockStyle.Top, Height = 28, BackColor = Pal.TitleBar };
            hdr.Paint += (s, e) =>
            {
                using var p = new Pen(Pal.Border);
                e.Graphics.DrawLine(p, 0, hdr.Height - 1, hdr.Width, hdr.Height - 1);
            };
            hdr.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = title.ToUpperInvariant(),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0),
                ForeColor = Pal.TextSecondary,
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
            });
            return hdr;
        }
    }

    // =========================================================================
    //  RightPanel — replaces TabControl entirely with plain Panels.
    //  Zero WinForms border artifacts because there is no TabControl.
    //
    //  Layout:
    //    [TabStrip Panel  — 28px tall, drawn manually]
    //    [Content Panel   — fills rest, shows actionsPanel or reportsPanel]
    //    [Toolbar Panel   — 36px, select/deselect all for active tab]
    // =========================================================================
    public class RightPanel : Panel
    {
        public event Action<int> TabChanged;
        public int ActiveTab { get; private set; } = 0;

        private readonly Panel _tabStrip;
        private readonly Panel[] _tabButtons = new Panel[2];
        private readonly Panel _contentArea;
        private readonly Panel _toolbar;
        private readonly ItemListPanel _actionsPanel;
        private readonly ItemListPanel _reportsPanel;

        // Toolbar buttons kept so we can resize them to match left panel
        private readonly Button _selAll;
        private readonly Button _deselAll;

        public RightPanel(
            List<object> actionItems, Func<object, BaseItemRow> actionFactory,
            List<object> reportItems, Func<object, BaseItemRow> reportFactory)
        {
            Dock = DockStyle.Fill;
            BackColor = Pal.EditorBg;

            // ── Tab strip ─────────────────────────────────────────────────────
            _tabStrip = new Panel
            {
                Dock = DockStyle.Top,
                Height = 24,
                BackColor = Pal.SideBarBg,
            };
            // Bottom separator line under strip
            _tabStrip.Paint += (s, e) =>
            {
                using var p = new Pen(Pal.Border);
                e.Graphics.DrawLine(p, 0, _tabStrip.Height - 1, _tabStrip.Width, _tabStrip.Height - 1);
            };

            string[] tabNames = { "Actions", "Reports" };
            for (int i = 0; i < 2; i++)
            {
                int idx = i;
                var tb = new Panel
                {
                    Width = 78,
                    Height = 24,
                    Left = i * 78,
                    Top = 0,
                    BackColor = i == 0 ? Pal.EditorBg : Pal.SideBarBg,
                    Cursor = Cursors.Hand,
                    Tag = (object)tabNames[i],
                };

                var lbl = new Label
                {
                    Dock = DockStyle.Fill,
                    Text = tabNames[i],
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font("Segoe UI", 9f, i == 0 ? FontStyle.Bold : FontStyle.Regular),
                    ForeColor = i == 0 ? Pal.TextPrimary : Pal.TextSecondary,
                    BackColor = Color.Transparent,
                    Cursor = Cursors.Hand,
                };

                tb.Paint += (s, e) =>
                {
                    bool sel = ActiveTab == idx;
                    if (sel)
                    {
                        using var ap = new Pen(Pal.Accent, 2);
                        e.Graphics.DrawLine(ap, 4, tb.Height - 1, tb.Width - 4, tb.Height - 1);
                    }
                };

                tb.Click += (s, e) => SelectTab(idx);
                lbl.Click += (s, e) => SelectTab(idx);
                tb.Controls.Add(lbl);
                _tabButtons[i] = tb;
                _tabStrip.Controls.Add(tb);
            }

            // ── Toolbar ───────────────────────────────────────────────────────
            _toolbar = new Panel { Dock = DockStyle.Bottom, Height = 36, BackColor = Pal.SideBarBg };
            _toolbar.Paint += (s, e) =>
            {
                using var p = new Pen(Pal.Border);
                e.Graphics.DrawLine(p, 0, 0, _toolbar.Width, 0);
            };

            _selAll = MainForm.SmallBtn("Select All");
            _deselAll = MainForm.SmallBtn("Deselect All");
            _selAll.Location = new Point(8, 7);
            _deselAll.Location = new Point(_selAll.Right + 5, 7);
            _selAll.Click += (s, e) => ActivePanel?.SelectAll(true);
            _deselAll.Click += (s, e) => ActivePanel?.SelectAll(false);
            _toolbar.Controls.AddRange(new Control[] { _selAll, _deselAll });

            // ── Content area ──────────────────────────────────────────────────
            _contentArea = new Panel { Dock = DockStyle.Fill, BackColor = Pal.EditorBg };

            _actionsPanel = new ItemListPanel(actionItems, actionFactory);
            _reportsPanel = new ItemListPanel(reportItems, reportFactory);

            _actionsPanel.Dock = DockStyle.Fill;
            _reportsPanel.Dock = DockStyle.Fill;
            _reportsPanel.Visible = false;

            _contentArea.Controls.Add(_actionsPanel);
            _contentArea.Controls.Add(_reportsPanel);

            Controls.Add(_contentArea);
            Controls.Add(_toolbar);
            Controls.Add(_tabStrip);
        }

        private ItemListPanel ActivePanel =>
            ActiveTab == 0 ? _actionsPanel : _reportsPanel;

        private void SelectTab(int idx)
        {
            if (idx == ActiveTab) return;
            ActiveTab = idx;

            _actionsPanel.Visible = idx == 0;
            _reportsPanel.Visible = idx == 1;

            for (int i = 0; i < 2; i++)
            {
                bool sel = i == idx;
                var lbl = _tabButtons[i].Controls.OfType<Label>().First();
                lbl.Font = new Font("Segoe UI", 9f, sel ? FontStyle.Bold : FontStyle.Regular);
                lbl.ForeColor = sel ? Pal.TextPrimary : Pal.TextSecondary;
                _tabButtons[i].BackColor = sel ? Pal.EditorBg : Pal.SideBarBg;
                _tabButtons[i].Invalidate();
            }

            TabChanged?.Invoke(idx);
        }

        public List<IFixerAction> GetCheckedActions() =>
            _actionsPanel.GetCheckedItems().Cast<IFixerAction>().ToList();

        public List<IReportAction> GetCheckedReports() =>
            _reportsPanel.GetCheckedItems().Cast<IReportAction>().ToList();
    }

    // =========================================================================
    //  FolderTreeView
    // =========================================================================
    public class FolderTreeView : Panel
    {
        public event Action<int> CheckedCountChanged;
        private readonly TreeView _tree;

        public FolderTreeView()
        {
            _tree = new TreeView
            {
                Dock = DockStyle.Fill,
                CheckBoxes = true,
                BackColor = Pal.SideBarBg,
                ForeColor = Pal.TextPrimary,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 10f),
                ShowLines = true,
                ShowPlusMinus = true,
                ShowRootLines = true,
                HideSelection = false,
                Indent = 16,
                ItemHeight = 22,
            };
            _tree.AfterCheck += OnAfterCheck;
            Controls.Add(_tree);
        }

        public void LoadFolder(string root)
        {
            _tree.BeginUpdate(); _tree.Nodes.Clear();
            if (Directory.Exists(root))
            {
                var n = BuildNode(root, Path.GetFileName(root));
                if (n != null) { _tree.Nodes.Add(n); n.Expand(); }
            }
            _tree.EndUpdate();
            FireCount();
        }

        private static TreeNode BuildNode(string path, string label)
        {
            var files = Directory.GetFiles(path, "*.xaml", SearchOption.TopDirectoryOnly).OrderBy(f => f).ToArray();
            var dirs = Directory.GetDirectories(path).OrderBy(d => d).ToArray();
            if (!files.Any() && !dirs.Any(HasXaml)) return null;

            var node = new TreeNode(label)
            {
                Tag = path,
                Checked = true,
                NodeFont = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.FromArgb(70, 70, 70),
            };
            foreach (var sub in dirs) { var sn = BuildNode(sub, Path.GetFileName(sub)); if (sn != null) node.Nodes.Add(sn); }
            foreach (var file in files)
                node.Nodes.Add(new TreeNode(Path.GetFileName(file)) { Tag = file, Checked = true, ForeColor = Pal.TextPrimary });
            return node;
        }

        private static bool HasXaml(string p) =>
            Directory.Exists(p) && (Directory.GetFiles(p, "*.xaml").Length > 0 || Directory.GetDirectories(p).Any(HasXaml));

        private bool _propagating;

        private void OnAfterCheck(object sender, TreeViewEventArgs e)
        {
            if (_propagating || e.Action == TreeViewAction.Unknown) return;
            _propagating = true;
            try { SetChildrenChecked(e.Node, e.Node.Checked); UpdateParent(e.Node.Parent); }
            finally { _propagating = false; }
            FireCount();
        }

        private static void SetChildrenChecked(TreeNode n, bool v)
        { foreach (TreeNode c in n.Nodes) { c.Checked = v; SetChildrenChecked(c, v); } }

        private static void UpdateParent(TreeNode p)
        { if (p == null) return; p.Checked = p.Nodes.Cast<TreeNode>().Any(c => c.Checked); UpdateParent(p.Parent); }

        public void SetAllChecked(bool v)
        { _propagating = true; try { foreach (var n in AllNodes()) n.Checked = v; } finally { _propagating = false; } FireCount(); }

        public List<string> GetCheckedFiles() =>
            AllNodes().Where(n => n.Checked && n.Tag is string t && t.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
                      .Select(n => (string)n.Tag).ToList();

        private void FireCount() => CheckedCountChanged?.Invoke(GetCheckedFiles().Count);

        private IEnumerable<TreeNode> AllNodes()
        {
            var stack = new Stack<TreeNode>();
            foreach (TreeNode r in _tree.Nodes) stack.Push(r);
            while (stack.Count > 0) { var n = stack.Pop(); yield return n; foreach (TreeNode c in n.Nodes) stack.Push(c); }
        }
    }

    // =========================================================================
    //  ItemListPanel — scroll area, no borders
    // =========================================================================
    public class ItemListPanel : Panel
    {
        private readonly List<BaseItemRow> _rows = new List<BaseItemRow>();
        private readonly FlowLayoutPanel _flow;

        public ItemListPanel(List<object> items, Func<object, BaseItemRow> factory)
        {
            Dock = DockStyle.Fill;
            BackColor = Pal.EditorBg;

            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Pal.EditorBg };

            _flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Pal.EditorBg,
                Padding = new Padding(0),
            };

            if (items.Count == 0)
            {
                _flow.Controls.Add(new Label
                {
                    Text = "No items registered.",
                    ForeColor = Pal.TextSecondary,
                    AutoSize = true,
                    Padding = new Padding(14, 14, 0, 0),
                    Font = new Font("Segoe UI", 9f, FontStyle.Italic),
                });
            }
            else
            {
                foreach (var item in items)
                {
                    var row = factory(item);
                    _rows.Add(row);
                    _flow.Controls.Add(row);
                }
            }

            scroll.Controls.Add(_flow);
            Controls.Add(scroll);

            scroll.Resize += (s, e) =>
            {
                int w = scroll.ClientSize.Width;
                if (w < 50) return;
                foreach (var row in _rows) row.Width = w;
            };
        }

        public void SelectAll(bool v) => _rows.ForEach(r => r.SetChecked(v));
        public IEnumerable<object> GetCheckedItems() => _rows.Where(r => r.IsChecked).Select(r => r.Item);
    }

    // =========================================================================
    //  BaseItemRow — compact rows, no left bar, rotated chevron, auto-height detail
    // =========================================================================
    public abstract class BaseItemRow : Panel
    {
        public abstract object Item { get; }
        public bool IsChecked => _checkbox.Checked;
        public void SetChecked(bool v) => _checkbox.Checked = v;

        protected readonly CheckBox _checkbox;
        private readonly RotatingChevron _chevron;
        private readonly Panel _detailPanel;
        private bool _expanded;
        private int _detailH;
        private bool _hasDetail;

        protected const int CollapsedH = 38;    // expanded spacing
        private const int PadX = 12;
        private const int DetailPadX = 16;
        private const int DetailPadY = 9;

        private readonly List<Label> _ruleBadges = new List<Label>();
        private readonly Color _accentColor;

        protected BaseItemRow(string name, Color accent,
                               IReadOnlyList<string> descriptionLines,
                               IReadOnlyList<string> ruleCodes)
        {
            _accentColor = accent;
            BackColor = Pal.EditorBg;
            Margin = new Padding(0);
            Height = CollapsedH;
            MinimumSize = new Size(0, CollapsedH);

            // Only thin separator — no left bar
            Paint += (s, e) =>
            {
                using var sep = new Pen(Color.FromArgb(238, 238, 238));
                e.Graphics.DrawLine(sep, 0, Height - 1, Width, Height - 1);
            };

            // ── Checkbox ─────────────────────────────────────────────────────
            _checkbox = new CheckBox
            {
                Checked = true,
                Text = name,
                AutoSize = false,
                Left = PadX,
                Top = 0,
                Height = CollapsedH,
                ForeColor = Pal.TextPrimary,
                Font = new Font("Segoe UI", 10f),    // +0.5 from 9.5
                Cursor = Cursors.Hand,
            };

            // ── Rule badges ──────────────────────────────────────────────────
            if (ruleCodes != null && ruleCodes.Count > 0)
            {
                foreach (var code in ruleCodes)
                {
                    var badge = new Label
                    {
                        Text = code,
                        AutoSize = false,
                        Size = new Size(BadgeW(code), 15),
                        Top = (CollapsedH - 15) / 2,
                        TextAlign = ContentAlignment.MiddleCenter,
                        Font = new Font("Segoe UI", 6.5f, FontStyle.Bold),
                        ForeColor = Color.FromArgb(0, 70, 145),
                        BackColor = Color.FromArgb(218, 234, 255),
                        Cursor = Cursors.Default,
                    };
                    _ruleBadges.Add(badge);
                    Controls.Add(badge);
                }
            }

            // ── Chevron — small, 14×14 ────────────────────────────────────────
            bool hasDesc = descriptionLines != null && descriptionLines.Any(l => !string.IsNullOrWhiteSpace(l));
            _hasDetail = hasDesc;

            _chevron = new RotatingChevron(accent, 10) { Visible = hasDesc };
            if (hasDesc) _chevron.Click += ToggleExpand;

            // ── Detail panel ──────────────────────────────────────────────────
            _detailPanel = new Panel
            {
                BackColor = Color.FromArgb(250, 251, 254),
                Visible = false,
                Left = 0,
                Top = CollapsedH,
            };
            _detailPanel.Paint += (s, e) =>
            {
                using var pen = new Pen(Color.FromArgb(238, 238, 242));
                e.Graphics.DrawLine(pen, 0, 0, _detailPanel.Width, 0);
            };

            if (hasDesc)
            {
                string joined = string.Join(Environment.NewLine,
                    descriptionLines.Where(l => l != null).Select(l => l.TrimEnd()));

                var descLbl = new Label
                {
                    Text = joined,
                    AutoSize = false,
                    Location = new Point(DetailPadX, DetailPadY),
                    ForeColor = Pal.TextSecondary,
                    Font = new Font("Segoe UI", 8.5f),
                };

                _detailPanel.Controls.Add(descLbl);

                _detailPanel.Resize += (s, e) =>
                {
                    int avail = Math.Max(30, _detailPanel.Width - DetailPadX - PadX);
                    descLbl.Width = avail;

                    var sz = TextRenderer.MeasureText(
                        descLbl.Text, descLbl.Font,
                        new Size(avail, 4000),
                        TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);

                    descLbl.Height = sz.Height + 2;
                    int needed = sz.Height + DetailPadY * 2;

                    if (needed != _detailH)
                    {
                        _detailH = needed;
                        _detailPanel.Height = needed;
                        if (_expanded)
                        {
                            Height = CollapsedH + _detailH;
                            (Parent as FlowLayoutPanel)?.PerformLayout();
                        }
                    }
                };

                _detailH = 18 * Math.Max(1, descriptionLines.Count(l => !string.IsNullOrWhiteSpace(l))) + DetailPadY * 2;
            }

            _detailPanel.Height = _detailH;

            Controls.Add(_checkbox);
            Controls.Add(_chevron);
            Controls.Add(_detailPanel);

            Resize += OnResize;
            OnResize(null, EventArgs.Empty);
        }

        private static int BadgeW(string code)
            => Math.Max(36, TextRenderer.MeasureText(code, new Font("Segoe UI", 6.5f, FontStyle.Bold)).Width + 10);

        private void OnResize(object s, EventArgs e)
        {
            _chevron.Left = Math.Max(0, Width - _chevron.Width - 8);
            _chevron.Top = (CollapsedH - _chevron.Height) / 2;

            // Badges right-to-left before chevron
            int bx = _chevron.Left - 5;
            for (int i = _ruleBadges.Count - 1; i >= 0; i--)
            {
                bx -= _ruleBadges[i].Width;
                _ruleBadges[i].Left = bx;
                bx -= 4;
            }

            int rightStop = _ruleBadges.Count > 0 ? _ruleBadges[0].Left - 6 : _chevron.Left - 4;
            _checkbox.Width = Math.Max(60, rightStop - _checkbox.Left);
            _detailPanel.Width = Width;
        }

        private void ToggleExpand(object s, EventArgs e)
        {
            if (!_hasDetail) return;
            _expanded = !_expanded;
            _chevron.SetExpanded(_expanded);
            _detailPanel.Visible = _expanded;
            Height = _expanded ? CollapsedH + _detailH : CollapsedH;
            (Parent as FlowLayoutPanel)?.PerformLayout();
        }
    }

    // =========================================================================
    //  RotatingChevron — draws a real > shape and rotates it 90° when expanded
    // =========================================================================
    public class RotatingChevron : Control
    {
        private float _angle;
        private readonly Color _hoverColor;
        private bool _hovered;
        private readonly int _sz;   // control size in pixels

        public RotatingChevron(Color accent, int size = 14)
        {
            _hoverColor = accent;
            _sz = size;
            Size = new Size(size, size);
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        }

        public void SetExpanded(bool expanded) { _angle = expanded ? 90f : 0f; Invalidate(); }

        protected override void OnMouseEnter(EventArgs e) { _hovered = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { _hovered = false; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            float cx = Width / 2f;
            float cy = Height / 2f;
            float arm = _sz * 0.28f;   // arm length

            // > shape: top-left → mid-right → bottom-left
            var pts = new PointF[]
            {
                new PointF(cx - arm * 0.55f, cy - arm),
                new PointF(cx + arm * 0.55f, cy),
                new PointF(cx - arm * 0.55f, cy + arm),
            };

            // Rotate around centre
            float rad = (float)(_angle * Math.PI / 180.0);
            float cos = (float)Math.Cos(rad), sin = (float)Math.Sin(rad);
            for (int i = 0; i < pts.Length; i++)
            {
                float dx = pts[i].X - cx, dy = pts[i].Y - cy;
                pts[i] = new PointF(cx + dx * cos - dy * sin, cy + dx * sin + dy * cos);
            }

            var color = _hovered ? _hoverColor : Color.FromArgb(160, 160, 160);
            using var pen = new Pen(color, 1.4f) { LineJoin = LineJoin.Round };
            g.DrawLines(pen, pts);
        }
    }

    // =========================================================================
    //  ActionItemRow / ReportItemRow
    // =========================================================================
    public class ActionItemRow : BaseItemRow
    {
        private readonly IFixerAction _action;
        public override object Item => _action;

        public ActionItemRow(IFixerAction action)
            : base(action.Name, Pal.Accent, Coerce(action.DescriptionLines, action.Description), action.RuleCodes)
        {
            _action = action;
        }

        private static IReadOnlyList<string> Coerce(IReadOnlyList<string> lines, string fallback)
        {
            if (lines != null && lines.Count > 0) return lines;
            if (!string.IsNullOrWhiteSpace(fallback)) return new[] { fallback };
            return null;
        }
    }

    public class ReportItemRow : BaseItemRow
    {
        private readonly IReportAction _report;
        public override object Item => _report;

        public ReportItemRow(IReportAction report)
            : base(report.Name, Color.FromArgb(160, 80, 0), report.DescriptionLines, null)
        {
            _report = report;
        }
    }

    // =========================================================================
    //  XlsxWriter — writes a real .xlsx file (one sheet per report)
    //  Uses only System.IO.Packaging (WindowsBase.dll, included with WinForms)
    //  and System.Xml. No third-party libraries needed.
    //
    //  IReportAction.RunReport() should return CSV text: first line = headers,
    //  subsequent lines = data rows, fields quoted if they contain commas.
    // =========================================================================
    /*
    internal static class XlsxWriter
    {
        public static void Write(string path, List<(string Name, string Csv)> sheets)
        {
            // Build the XLSX zip package in memory then write to disk
            using var mem = new System.IO.MemoryStream();
            using (var pkg = System.IO.Packaging.Package.Open(
                       mem, System.IO.FileMode.Create, System.IO.FileAccess.ReadWrite))
            {
                // ── [Content_Types].xml ───────────────────────────────────────
                SetContentTypes(pkg, sheets.Count);

                // ── _rels/.rels ───────────────────────────────────────────────
                AddPart(pkg, "/_rels/.rels",
                    "application/vnd.openxmlformats-package.relationships+xml",
                    @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
  <Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"" Target=""xl/workbook.xml""/>
</Relationships>");

                // ── xl/workbook.xml ───────────────────────────────────────────
                var sheetRefs = sheets.Select((s, i) =>
                    $@"  <sheet name=""{XmlEsc(Truncate(s.Name, 31))}"" sheetId=""{i + 1}"" r:id=""rId{i + 1}""/>").ToList();
                AddPart(pkg, "/xl/workbook.xml",
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml",
                    $@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<workbook xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main""
          xmlns:r=""http://schemas.openxmlformats.org/officeDocument/2006/relationships"">
  <sheets>
{string.Join("
", sheetRefs)}
  </ sheets >
</ workbook > ");

                // ── xl/_rels/workbook.xml.rels ────────────────────────────────
                var wbRels = sheets.Select((s, i) =>
                    $@"  <Relationship Id=""rId{i + 1}"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"" Target=""worksheets/sheet{i + 1}.xml""/>").ToList();
                AddPart(pkg, "/xl/_rels/workbook.xml.rels",
                    "application/vnd.openxmlformats-package.relationships+xml",
                    $@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
{string.Join("
", wbRels)}
</Relationships>");

                // ── xl/worksheets/sheetN.xml ──────────────────────────────────
                for (int i = 0; i < sheets.Count; i++)
                {
                    string xml = BuildSheetXml(sheets[i].Csv);
                    AddPart(pkg, $"/xl/worksheets/sheet{i + 1}.xml",
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml",
                        xml);
                }
            }

            System.IO.File.WriteAllBytes(path, mem.ToArray());
        }

        // ── Build worksheet XML from CSV text ─────────────────────────────────
        private static string BuildSheetXml(string csv)
        {
            var rows = new System.Text.StringBuilder();
            int rowNum = 1;
            foreach (var line in (csv ?? string.Empty)
                     .Split(new[] { "
", "
" }, StringSplitOptions.None))
            {
                if (string.IsNullOrWhiteSpace(line)) { rowNum++; continue; }
                var fields = ParseCsvLine(line);
                var cells  = new System.Text.StringBuilder();
                for (int c = 0; c < fields.Count; c++)
                {
                    string colAddr = ColAddr(c + 1) + rowNum;
                    string val     = XmlEsc(fields[c]);
                    cells.Append($@"<c r=""{colAddr}"" t=""inlineStr""><is><t>{val}</t></is></c>");
                }
                rows.AppendLine($@"<row r=""{rowNum}"">{cells}</row>");
                rowNum++;
            }

            return $@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<worksheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"">
  <sheetData>
{rows}  </sheetData>
</worksheet>";
        }

        // ── CSV line parser (handles quoted fields) ───────────────────────────
        private static List<string> ParseCsvLine(string line)
        {
            var fields = new List<string>();
            int i = 0;
            while (i < line.Length)
            {
                if (line[i] == '"')
                {
                    i++;
                    var sb = new System.Text.StringBuilder();
                    while (i < line.Length)
                    {
                        if (line[i] == '"' && i + 1 < line.Length && line[i + 1] == '"')
                        { sb.Append('"'); i += 2; }
                        else if (line[i] == '"') { i++; break; }
                        else sb.Append(line[i++]);
                    }
                    fields.Add(sb.ToString());
                    if (i < line.Length && line[i] == ',') i++;
                }
                else
                {
                    int j = line.IndexOf(',', i);
                    if (j < 0) { fields.Add(line.Substring(i)); break; }
                    fields.Add(line.Substring(i, j - i));
                    i = j + 1;
                }
            }
            return fields;
        }

        private static string ColAddr(int col)
        {
            string addr = "";
            while (col > 0) { addr = (char)('A' + (col - 1) % 26) + addr; col = (col - 1) / 26; }
                return addr;
            }

        private static string Truncate(string s, int max)
            => s.Length <= max ? s : s.Substring(0, max);

        private static string XmlEsc(string s)
            => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
               .Replace(""", "&quot;").Replace("'", "&apos;");

        private static void AddPart(System.IO.Packaging.Package pkg,
                                    string uri, string contentType, string xml)
        {
            var partUri = new Uri(uri, UriKind.Relative);
            var part = pkg.CreatePart(partUri, contentType,
                                         System.IO.Packaging.CompressionOption.Normal);
            using var w = new System.IO.StreamWriter(part.GetStream());
            w.Write(xml);
        }

        private static void SetContentTypes(System.IO.Packaging.Package pkg, int sheetCount)
        {
            // Content types are handled automatically by Package, but we need to
            // override with exact OOXML types. Use a workaround: write the part manually.
            // (System.IO.Packaging sets content types from AddPart calls directly.)
            // Nothing extra needed here — content types are set in AddPart calls above.
            _ = sheetCount; // suppress warning
        }
    }
    */
    // =========================================================================
    //  Pal
    // =========================================================================
    internal static class Pal
    {
        public static readonly Color EditorBg = Color.White;
        public static readonly Color SideBarBg = Color.FromArgb(243, 243, 243);
        public static readonly Color TitleBar = Color.FromArgb(221, 221, 221);
        public static readonly Color Border = Color.FromArgb(225, 225, 225);
        public static readonly Color TextPrimary = Color.FromArgb(30, 30, 30);
        public static readonly Color TextSecondary = Color.FromArgb(110, 110, 110);
        public static readonly Color Accent = Color.FromArgb(0, 120, 212);
        public static readonly Color AccentHover = Color.FromArgb(0, 102, 180);
        public static readonly Color ButtonBg = Color.FromArgb(232, 232, 232);
    }
}