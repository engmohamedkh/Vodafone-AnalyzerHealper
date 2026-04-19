using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace AnalyzerHelper.View
{
    public partial class BranchLogBatchWindow : Window
    {
        private readonly List<BranchLogRow> _rows;

        /// <summary>True when user clicked Apply.</summary>
        public bool Applied { get; private set; }

        public BranchLogBatchWindow(List<BranchLogRow> rows)
        {
            _rows = rows;
            InitializeComponent();
            Owner = System.Windows.Application.Current?.MainWindow;

            int fileCount = rows.Select(r => r.FilePath).Distinct(StringComparer.OrdinalIgnoreCase).Count();
            HeaderText.Text = $"{rows.Count} branch(es) across {fileCount} file(s) missing logging.";

            foreach (var row in _rows)
            {
                row.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(BranchLogRow.AddLog)) UpdateBottomStatus(); };
            }

            Grid.ItemsSource = _rows;
            UpdateBottomStatus();
        }

        public IReadOnlyList<BranchLogRow> Rows => _rows;

        private void Grid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateBottomStatus();
        }

        private void UpdateBottomStatus()
        {
            int selectedRows = Grid.SelectedItems.Count;
            int total = _rows.Count;
            int checkedCount = _rows.Count(r => r.AddLog);

            CheckedCountText.Text = $"{checkedCount} of {total} checked";
            ApplyCountLabel.Text = $"{checkedCount} branch(es) to be fixed";
            
            ApplyButton.IsEnabled = checkedCount > 0;
            ApplyButton.Content = checkedCount == total ? "✔ Apply All" : "✔ Apply Selected";

            BottomStatusText.Text = selectedRows == 0
                ? "Tick the checkboxes for branches you want to fix, then click Apply."
                : $"{selectedRows} rows highlighted. (Use checkboxes to filter apply)";
        }

        private void SelectAllButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var row in _rows) row.AddLog = true;
            UpdateBottomStatus();
        }

        private void UnselectAllButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var row in _rows) row.AddLog = false;
            UpdateBottomStatus();
        }

        private void Grid_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.OriginalSource is Visual v)
            {
                var parentRow = FindParent<DataGridRow>(v);
                if (parentRow == null)
                {
                    Grid.UnselectAll();
                }
            }
        }

        private T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            var parentObject = System.Windows.Media.VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            if (parentObject is T parent) return parent;
            return FindParent<T>(parentObject);
        }
        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            Grid.CommitEdit();
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
    }

    /// <summary>Row model for branch log batch editing.</summary>
    public class BranchLogRow : INotifyPropertyChanged
    {
        private string _message = "Branch entered";
        private bool _addLog = true;

        public bool AddLog
        {
            get => _addLog;
            set { if (_addLog == value) return; _addLog = value; OnPropertyChanged(nameof(AddLog)); }
        }

        public string FilePath { get; set; } = "";
        public string File { get; set; } = "";
        public string Type { get; set; } = ""; // If, FlowDecision, FlowSwitch
        public string BranchDescription { get; set; } = "";
        public int InsertIndex { get; set; }

        private string _logType = "Info_Log";
        public string LogType
        {
            get => _logType;
            set { if (_logType == value) return; _logType = value; OnPropertyChanged(nameof(LogType)); }
        }

        public static List<string> AvailableLogTypes { get; } = new List<string> { "Info_Log", "Error_Log" };

        public string Message
        {
            get => _message;
            set { if (_message == value) return; _message = value ?? ""; OnPropertyChanged(nameof(Message)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
