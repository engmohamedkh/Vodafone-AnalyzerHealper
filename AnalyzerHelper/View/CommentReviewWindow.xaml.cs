using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using AnalyzerHelper.Rules;

namespace AnalyzerHelper.View
{
    public partial class CommentReviewWindow : Window
    {
        private readonly List<CommentItem> _items;
        private readonly List<CommentRow> _rows;

        public CommentReviewWindow(List<CommentItem> items, string filePath)
        {
            _items = items;
            _rows = items.Select(i => new CommentRow(i)).ToList();
            InitializeComponent();
            Owner = System.Windows.Application.Current?.MainWindow;
            HeaderText.Text = $"{items.Count} block(s) found. Choose Action for each: Delete (remove), Uncomment (restore), or Keep.";
            Grid.ItemsSource = _rows;
            ActionColumn.ItemsSource = new[] { "Delete", "Uncomment", "Keep" };
            foreach (var row in _rows)
                row.ActionChoice = row.Item.ChosenAction.ToString();
        }

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
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private sealed class CommentRow : INotifyPropertyChanged
        {
            private string _actionChoice = "Delete";

            public CommentRow(CommentItem item) => Item = item;
            public CommentItem Item { get; }
            public string Context => Item.IsInFlowchart ? "Flowchart" : "Sequence";
            public string DisplayName => Item.DisplayName;
            public string Contents => Item.InnerSummary;

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
    }
}
