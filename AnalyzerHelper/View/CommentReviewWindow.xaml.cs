using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AnalyzerHelper.Rules;

namespace AnalyzerHelper.View
{
    public partial class CommentReviewWindow : Window
    {
        private readonly List<CommentItem> _items;
        private readonly List<CommentRow> _rows;

        /// <summary>True when user clicked Apply, false when dialog was cancelled.</summary>
        public bool Applied { get; private set; }

        public CommentReviewWindow(List<CommentItem> items, string filePath)
        {
            _items = items;
            _rows = items.Select(i => new CommentRow(i)).ToList();
            InitializeComponent();
            Owner = System.Windows.Application.Current?.MainWindow;

            int total = items.Count;
            int inFlow = items.Count(i => i.IsInFlowchart);
            int inSeq = total - inFlow;
            int fileCount = items.Select(i => i.FilePath).Distinct(StringComparer.OrdinalIgnoreCase).Count();

            HeaderText.Text = $"{total} commented block(s) across {fileCount} file(s)  ·  " +
                              $"{inSeq} in Sequence  ·  {inFlow} in Flowchart  —  " +
                              $"Choose Action: Delete (Remove), Uncomment (Restore), or Keep.";

            Grid.ItemsSource = _rows;
            ActionColumn.ItemsSource = new[] { "Delete", "Uncomment", "Keep" };
            foreach (var row in _rows)
            {
                row.ActionChoice = row.Item.ChosenAction.ToString();
                row.PropertyChanged += OnRowPropertyChanged;
            }

            UpdateCounters();
            UpdateBottomStatus();
        }

        // ── Bulk action handlers ──────────────────────────────────────────

        private void AllDelete_Click(object sender, RoutedEventArgs e) => BulkSet(false, "Delete");
        private void AllUncomment_Click(object sender, RoutedEventArgs e) => BulkSet(false, "Uncomment");
        private void AllKeep_Click(object sender, RoutedEventArgs e) => BulkSet(false, "Keep");
        private void SelDelete_Click(object sender, RoutedEventArgs e) => BulkSet(true, "Delete");
        private void SelKeep_Click(object sender, RoutedEventArgs e) => BulkSet(true, "Keep");

        private void BulkSet(bool selectedOnly, string action)
        {
            var rows = selectedOnly
                ? Grid.SelectedItems.Cast<CommentRow>()
                : _rows.AsEnumerable();

            foreach (var row in rows)
                row.ActionChoice = action;

            Grid.Items.Refresh();
            UpdateCounters();
        }

        // ── Counters ──────────────────────────────────────────────────────

        private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CommentRow.ActionChoice))
                UpdateCounters();
        }

        private void UpdateCounters()
        {
            int del = _rows.Count(r => r.ActionChoice == "Delete");
            int unc = _rows.Count(r => r.ActionChoice == "Uncomment");
            int kp = _rows.Count(r => r.ActionChoice == "Keep");

            DeleteCount.Text = $"{del} Delete";
            UncommentCount.Text = $"{unc} Uncomment";
            KeepCount.Text = $"{kp} Keep";
        }

        // ── Selection changed — update bottom status bar ──────────────────

        private void Grid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateBottomStatus();
        }

        private void UpdateBottomStatus()
        {
            int selected = Grid.SelectedItems.Count;
            int total = _rows.Count;

            if (selected == 0)
            {
                BottomStatusText.Text = $"No rows selected. Bulk actions on 'All' will apply to all {total} row(s).";
            }
            else
            {
                int selDel = Grid.SelectedItems.Cast<CommentRow>().Count(r => r.ActionChoice == "Delete");
                int selUnc = Grid.SelectedItems.Cast<CommentRow>().Count(r => r.ActionChoice == "Uncomment");
                int selKeep = Grid.SelectedItems.Cast<CommentRow>().Count(r => r.ActionChoice == "Keep");
                BottomStatusText.Text = $"{selected} of {total} selected  ·  {selDel} Delete  ·  {selUnc} Uncomment  ·  {selKeep} Keep";
            }
        }

        // ── Apply / Cancel ────────────────────────────────────────────────

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            Grid.CommitEdit();
            foreach (var row in _rows)
            {
                if (string.IsNullOrEmpty(row.ActionChoice)) continue;
                if (row.ActionChoice == "Uncomment")
                    row.Item.ChosenAction = CommentAction.Uncomment;
                else if (row.ActionChoice == "Keep")
                    row.Item.ChosenAction = CommentAction.Keep;
                else
                    row.Item.ChosenAction = CommentAction.Delete;
            }

            Applied = true;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Applied = false;
            DialogResult = false;
            Close();
        }

        // ── Row model ─────────────────────────────────────────────────────

        internal sealed class CommentRow : INotifyPropertyChanged
        {
            private string _actionChoice = "Delete";

            public CommentRow(CommentItem item) => Item = item;
            public CommentItem Item { get; }
            public string File => Item.RelativePath;
            public string Context => Item.IsInFlowchart ? "Flowchart" : "Sequence";
            public string DisplayName => Item.DisplayName;
            public string Contents => Item.InnerSummary;

            /// <summary>Merged column: "InvokeTitle → WorkflowFile" or just one if the other is empty.</summary>
            public string InvokeInfo
            {
                get
                {
                    bool hasTitle = !string.IsNullOrEmpty(Item.InvokeTitle);
                    bool hasFile = !string.IsNullOrEmpty(Item.WorkflowFile);
                    if (hasTitle && hasFile) return $"{Item.InvokeTitle} → {Item.WorkflowFile}";
                    if (hasTitle) return Item.InvokeTitle;
                    if (hasFile) return Item.WorkflowFile;
                    return "—";
                }
            }

            public string ActionChoice
            {
                get => _actionChoice;
                set
                {
                    if (_actionChoice == value) return;
                    _actionChoice = value ?? "Delete";
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ActionChoice)));
                    if (_actionChoice == "Uncomment") Item.ChosenAction = CommentAction.Uncomment;
                    else if (_actionChoice == "Keep") Item.ChosenAction = CommentAction.Keep;
                    else Item.ChosenAction = CommentAction.Delete;
                }
            }

            public event PropertyChangedEventHandler? PropertyChanged;
        }
        private void Grid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is Visual v)
            {
                // If we didn't click on a DataGridRow, clear selection
                var parentRow = FindParent<DataGridRow>(v);
                if (parentRow == null)
                {
                    Grid.UnselectAll();
                }
            }
        }

        private T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            if (parentObject is T parent) return parent;
            return FindParent<T>(parentObject);
        }
    }
}
