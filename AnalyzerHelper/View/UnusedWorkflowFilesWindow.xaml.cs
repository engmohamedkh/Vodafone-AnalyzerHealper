using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfColor = System.Windows.Media.Color;

namespace AnalyzerHelper.View
{
    public partial class UnusedWorkflowFilesWindow : Window
    {
        private readonly List<(string Path, System.Windows.Controls.CheckBox DeleteBox)> _rows = new();

        public UnusedWorkflowFilesWindow(string projectRoot, IReadOnlyList<string> unusedFullPaths)
        {
            InitializeComponent();
            Owner = System.Windows.Application.Current?.MainWindow;
            RootPathText.Text = "Project: " + projectRoot;

            var stack = new StackPanel();
            foreach (string fullPath in unusedFullPaths.OrderBy(x => x, System.StringComparer.OrdinalIgnoreCase))
            {
                string rel = Path.GetRelativePath(projectRoot, fullPath);
                var border = new Border
                {
                    Background = new SolidColorBrush(WpfColor.FromRgb(250, 250, 250)),
                    BorderBrush = new SolidColorBrush(WpfColor.FromRgb(224, 224, 224)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(10),
                    Margin = new Thickness(0, 0, 0, 8)
                };

                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var cb = new System.Windows.Controls.CheckBox
                {
                    Content = "Delete",
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(0, 2, 12, 0),
                    ToolTip = "Check to delete this file when you click OK."
                };
                Grid.SetColumn(cb, 0);

                var tb = new TextBlock
                {
                    Text = rel,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = new SolidColorBrush(WpfColor.FromRgb(51, 51, 51)),
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(tb, 1);

                grid.Children.Add(cb);
                grid.Children.Add(tb);
                border.Child = grid;
                stack.Children.Add(border);
                _rows.Add((fullPath, cb));
            }

            RowsHost.Content = stack;
        }

        /// <summary>Full paths of files the user marked for deletion.</summary>
        public IReadOnlyList<string> GetFilesMarkedForDeletion()
        {
            return _rows.Where(r => r.DeleteBox.IsChecked == true).Select(r => r.Path).ToList();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Activate();
            Dispatcher.BeginInvoke(new System.Action(() =>
            {
                Activate();
                Keyboard.Focus(OkButton);
            }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        private void SelectAllDelete_Click(object sender, RoutedEventArgs e)
        {
            foreach (var (_, cb) in _rows)
                cb.IsChecked = true;
        }

        private void ClearDelete_Click(object sender, RoutedEventArgs e)
        {
            foreach (var (_, cb) in _rows)
                cb.IsChecked = false;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
