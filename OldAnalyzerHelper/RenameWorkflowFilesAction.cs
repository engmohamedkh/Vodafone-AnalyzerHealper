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
    /// Renames .xaml files in two specific folders and updates their internals.
    ///
    /// ── Subprocess folder ──────────────────────────────────────────────────
    /// Target pattern : {ShortName}_{ProcessStage}_{WorkflowName}.xaml
    /// WorkflowName   : everything after the 2nd underscore in the current name.
    ///                  If the file has fewer than 2 underscores the entire
    ///                  current stem is kept as the WorkflowName.
    /// Only renames if the first two segments don't already match.
    ///
    /// ── Logic folder (non-IAP files) ───────────────────────────────────────
    /// Target pattern : {ShortName}_{WorkflowName}.xaml
    /// WorkflowName   : everything after the 1st underscore.
    ///                  Files whose name starts with "IAP" are skipped entirely.
    /// Only renames if the first segment doesn't already match.
    ///
    /// ── What the rename does ───────────────────────────────────────────────
    ///   1. Renames the .xaml file on disk.
    ///   2. Updates x:Class attribute on the root Activity element.
    ///   3. Updates the root Sequence/Flowchart DisplayName attribute.
    ///
    /// ── Config dialog ──────────────────────────────────────────────────────
    /// Appears when Start is clicked while this action is checked.
    /// Two fields: Project Short Name  |  Process Stage
    /// </summary>
    public class RenameWorkflowFilesAction : IFixerAction
    {
        // ── State set by dialog ───────────────────────────────────────────────
        private string _shortName = string.Empty;
        private string _processStage = string.Empty;

        // ── IFixerAction identity ─────────────────────────────────────────────
        public string Name => "Rename Workflow Files";

        public string Description =>
            "Renames files in the Subprocess folder to {ShortName}_{Stage}_{WorkflowName} " +
            "and Logic folder (non-IAP) files to {ShortName}_{WorkflowName}. " +
            "\nAlso updates x:Class and DisplayName inside each renamed file. " +
            "A dialog collects Short Name and Process Stage before running.";

        // ── This action uses batch mode ───────────────────────────────────────
        public bool NeedsConfig => true;
        public bool Run(string filePath) => false;   // not used in batch mode

        // =====================================================================
        //  Config dialog — shown by OnStart before RunBatch
        // =====================================================================
        public bool ShowConfigDialog()
        {
            using var dlg = new RenameConfigDialog(_shortName, _processStage);
            if (dlg.ShowDialog() != DialogResult.OK) return false;

            _shortName = dlg.ShortName.Trim();
            _processStage = dlg.ProcessStage.Trim();

            if (string.IsNullOrEmpty(_shortName))
            {
                MessageBox.Show("Project Short Name cannot be empty.",
                                "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            return true;
        }

        // =====================================================================
        //  RunBatch — scans Subprocess + Logic, renames, updates internals
        // =====================================================================
        public int RunBatch(string rootFolder)
        {
            int count = 0;

            // ── Subprocess folder ─────────────────────────────────────────────
            string subprocessDir = Path.Combine(rootFolder, "Subprocess");
            if (Directory.Exists(subprocessDir))
                count += ProcessFolder(subprocessDir, isSubprocess: true);

            // ── Logic folder ──────────────────────────────────────────────────
            string logicDir = Path.Combine(rootFolder, "Logic");
            if (Directory.Exists(logicDir))
                count += ProcessFolder(logicDir, isSubprocess: false);

            return count;
        }

        // =====================================================================
        //  Process all .xaml files in one folder (non-recursive)
        // =====================================================================
        private int ProcessFolder(string folder, bool isSubprocess)
        {
            int count = 0;

            foreach (string filePath in Directory.GetFiles(folder, "*.xaml",
                                                           SearchOption.TopDirectoryOnly))
            {
                string oldStem = Path.GetFileNameWithoutExtension(filePath);

                // Logic: skip files starting with IAP
                if (!isSubprocess &&
                    oldStem.StartsWith("IAP", StringComparison.OrdinalIgnoreCase))
                    continue;

                // ── Compute workflow name (the part to keep) ──────────────
                string workflowName = ExtractWorkflowName(oldStem, isSubprocess);

                // ── Compute target stem ───────────────────────────────────
                string newStem = isSubprocess
                    ? $"{_shortName}_{_processStage}_{workflowName}"
                    : $"{_shortName}_{workflowName}";

                // ── Skip if already correct ───────────────────────────────
                if (oldStem == newStem) continue;

                string dir = Path.GetDirectoryName(filePath)!;
                string newPath = Path.Combine(dir, newStem + ".xaml");

                // ── Update internals first (while old path is still valid) ─
                bool internalChanged = UpdateInternals(filePath, newStem);

                // ── Rename file on disk ───────────────────────────────────
                try
                {
                    if (File.Exists(newPath) && !string.Equals(filePath, newPath,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        // Destination exists — skip to avoid overwrite
                        continue;
                    }
                    File.Move(filePath, newPath);
                    count++;
                }
                catch
                {
                    // If rename fails, leave the internal changes (they're
                    // harmless) but don't count as success
                }
            }

            return count;
        }

        // =====================================================================
        //  Extract the workflow name (the part to keep after renaming)
        //
        //  Subprocess: ShortName_Stage_WorkflowName  → keep after 2nd underscore
        //  Logic:      ShortName_WorkflowName        → keep after 1st underscore
        //
        //  If the file doesn't have enough underscores, treat the whole stem
        //  as the workflow name so nothing is lost.
        // =====================================================================
        private static string ExtractWorkflowName(string stem, bool isSubprocess)
        {
            int underscoresNeeded = isSubprocess ? 2 : 1;
            int pos = 0;
            int found = 0;

            while (found < underscoresNeeded && pos < stem.Length)
            {
                int next = stem.IndexOf('_', pos);
                if (next < 0) break;
                pos = next + 1;
                found++;
            }

            // If we found enough underscores, return everything from that point
            if (found == underscoresNeeded && pos <= stem.Length)
                return stem.Substring(pos);

            // Not enough underscores — keep the whole stem as workflow name
            return stem;
        }

        // =====================================================================
        //  Update x:Class and root DisplayName inside the XAML file
        // =====================================================================
        private static bool UpdateInternals(string filePath, string newStem)
        {
            string original;
            try { original = File.ReadAllText(filePath); }
            catch { return false; }

            XDocument doc;
            try { doc = XDocument.Parse(original, LoadOptions.PreserveWhitespace); }
            catch { return false; }

            bool changed = false;

            XNamespace xNs = "http://schemas.microsoft.com/winfx/2006/xaml";

            // ── x:Class on root Activity element ─────────────────────────────
            var classAttr = doc.Root?.Attribute(xNs + "Class");
            if (classAttr != null && classAttr.Value != newStem)
            {
                classAttr.Value = newStem;
                changed = true;
            }

            // ── DisplayName on root Sequence or Flowchart ─────────────────────
            var body = doc.Root?.Elements()
                .FirstOrDefault(e => e.Name.LocalName == "Sequence" ||
                                     e.Name.LocalName == "Flowchart");

            if (body != null)
            {
                var dnAttr = body.Attribute("DisplayName");
                if (dnAttr != null && dnAttr.Value != newStem)
                {
                    dnAttr.Value = newStem;
                    changed = true;
                }
            }

            if (!changed) return false;

            // ── Serialise back ────────────────────────────────────────────────
            try
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

                string body2 = sb.ToString();
                if (original.TrimStart().StartsWith("<?xml"))
                {
                    int end = original.IndexOf("?>") + 2;
                    body2 = original.Substring(0, end) +
                            (crlf ? "\r\n" : "\n") + body2;
                }

                File.WriteAllText(filePath, body2);
                return true;
            }
            catch { return false; }
        }
    }

    // =========================================================================
    //  RenameConfigDialog — modal dialog for Short Name + Process Stage input
    // =========================================================================
    internal class RenameConfigDialog : Form
    {
        public string ShortName => _shortNameBox.Text;
        public string ProcessStage => _stageBox.Text;

        private readonly TextBox _shortNameBox;
        private readonly TextBox _stageBox;

        public RenameConfigDialog(string currentShortName, string currentStage)
        {
            Text = "Rename Workflow Files — Configuration";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(420, 210);
            BackColor = Color.FromArgb(255, 255, 255);
            Font = new Font("Segoe UI", 9.5f);

            // ── Layout ────────────────────────────────────────────────────────
            var table = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 4,
                Padding = new Padding(16, 16, 16, 8),
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));  // Short Name
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));  // Process Stage
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));  // spacer
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // buttons

            var lblShort = Label("Project Short Name");
            var lblStage = Label("Process Stage");

            _shortNameBox = StyledBox(currentShortName);
            _stageBox = StyledBox(currentStage);

            table.Controls.Add(lblShort, 0, 0);
            table.Controls.Add(_shortNameBox, 1, 0);
            table.Controls.Add(lblStage, 0, 1);
            table.Controls.Add(_stageBox, 1, 1);

            // ── Button row ────────────────────────────────────────────────────
            var btnPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0),
            };

            var okBtn = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Width = 80,
                Height = 28,
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
            };
            okBtn.FlatAppearance.BorderSize = 0;

            var cancelBtn = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Width = 80,
                Height = 28,
                BackColor = Color.FromArgb(240, 240, 240),
                FlatStyle = FlatStyle.Flat,
            };

            btnPanel.Controls.Add(okBtn);
            btnPanel.Controls.Add(cancelBtn);

            table.Controls.Add(btnPanel, 0, 3);
            table.SetColumnSpan(btnPanel, 2);

            Controls.Add(table);
            AcceptButton = okBtn;
            CancelButton = cancelBtn;

            _shortNameBox.Focus();
        }

        private static Label Label(string text) => new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(51, 51, 51),
        };

        private static TextBox StyledBox(string value) => new TextBox
        {
            Text = value ?? string.Empty,
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(250, 250, 250),
            BorderStyle = BorderStyle.FixedSingle,
        };
    }
}