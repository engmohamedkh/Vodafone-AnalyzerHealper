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
    public partial class AnnotationBatchWindow : Window
    {
        private readonly List<AnnotationRow> _rows;

        /// <summary>True when user clicked Apply.</summary>
        public bool Applied { get; private set; }

        public AnnotationBatchWindow(List<AnnotationRow> rows)
        {
            _rows = rows;
            InitializeComponent();
            Owner = System.Windows.Application.Current?.MainWindow;

            int fileCount = rows.Select(r => r.FilePath).Distinct(StringComparer.OrdinalIgnoreCase).Count();
            HeaderText.Text = $"{rows.Count} file(s) to annotate. Fields are auto-populated — edit as needed.";

            foreach (var row in _rows)
            {
                row.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(AnnotationRow.IsSelectedForFix)) UpdateBottomStatus(); };
            }

            Grid.ItemsSource = _rows;
            UpdateBottomStatus();
        }

        public IReadOnlyList<AnnotationRow> Rows => _rows;

        private void Grid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateBottomStatus();
        }

        private void SelectAllButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var row in _rows) row.IsSelectedForFix = true;
            Grid.Items.Refresh();
            UpdateBottomStatus();
        }

        private void UnselectAllButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var row in _rows) row.IsSelectedForFix = false;
            Grid.Items.Refresh();
            UpdateBottomStatus();
        }

        private void UpdateBottomStatus()
        {
            int checkedCount = _rows.Count(r => r.IsSelectedForFix);
            int total = _rows.Count;
            
            CheckedCountText.Text = $"{checkedCount} of {total} checked";
            
            if (ApplyButton != null)
            {
                ApplyButton.IsEnabled = checkedCount > 0;
                ApplyButton.Content = checkedCount == total ? "✔ Apply All" : "✔ Apply Selected";
            }
            
            int selectedRows = Grid.SelectedItems.Count;
            BottomStatusText.Text = selectedRows == 0
                ? $"Click a row to edit. Checked items will be processed."
                : $"{selectedRows} row(s) highlighted.";
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
            var parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            if (parentObject is T parent) return parent;
            return FindParent<T>(parentObject);
        }
    }

    /// <summary>Row model for annotation batch editing.</summary>
    public class AnnotationRow : INotifyPropertyChanged
    {
        private string _componentName = "";
        private string _description = "";
        private string _preCondition = "";
        private string _postCondition = "";
        private string _pddSection = "";
        private bool _isSelectedForFix = true;

        public bool IsSelectedForFix
        {
            get => _isSelectedForFix;
            set { if (_isSelectedForFix == value) return; _isSelectedForFix = value; OnPropertyChanged(nameof(IsSelectedForFix)); }
        }

        public string FilePath { get; set; } = "";
        public string File { get; set; } = "";

        public string ComponentName
        {
            get => _componentName;
            set { if (_componentName == value) return; _componentName = value ?? ""; OnPropertyChanged(nameof(ComponentName)); }
        }

        public string Description
        {
            get => _description;
            set { if (_description == value) return; _description = value ?? ""; OnPropertyChanged(nameof(Description)); }
        }

        public string PreCondition
        {
            get => _preCondition;
            set { if (_preCondition == value) return; _preCondition = value ?? ""; OnPropertyChanged(nameof(PreCondition)); }
        }

        public string PostCondition
        {
            get => _postCondition;
            set { if (_postCondition == value) return; _postCondition = value ?? ""; OnPropertyChanged(nameof(PostCondition)); }
        }

        public string PddSection
        {
            get => _pddSection;
            set { if (_pddSection == value) return; _pddSection = value ?? ""; OnPropertyChanged(nameof(PddSection)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
