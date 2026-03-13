using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfColor = System.Windows.Media.Color;

namespace AnalyzerHelper.View
{
    /// <summary>
    /// Dialog that shows branch details (description and source file) and lets the user enter
    /// a log message for each branch. Used by BranchesLogsRule fix.
    /// </summary>
    public partial class BranchLogConfigWindow : Window
    {
        private readonly List<System.Windows.Controls.TextBox> _messageBoxes = new();

        public BranchLogConfigWindow(IReadOnlyList<(string Description, string Source)> branchInfos)
        {
            InitializeComponent();
            Owner = System.Windows.Application.Current?.MainWindow;

            var stack = new StackPanel();
            int index = 1;
            foreach (var (description, source) in branchInfos)
            {
                var border = new Border
                {
                    Background = new SolidColorBrush(WpfColor.FromRgb(250, 250, 250)),
                    BorderBrush = new SolidColorBrush(WpfColor.FromRgb(224, 224, 224)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(12),
                    Margin = new Thickness(0, 0, 0, 10)
                };

                var grid = new Grid();
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var branchLabel = new TextBlock
                {
                    Text = $"Branch {index}: {description}",
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(WpfColor.FromRgb(51, 51, 51)),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 4)
                };
                Grid.SetRow(branchLabel, 0);
                grid.Children.Add(branchLabel);

                var sourceLabel = new TextBlock
                {
                    Text = "Source: " + (source.Length > 80 ? "..." + source.Substring(source.Length - 77) : source),
                    FontSize = 12,
                    Foreground = new SolidColorBrush(WpfColor.FromRgb(100, 100, 100)),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 6)
                };
                Grid.SetRow(sourceLabel, 1);
                grid.Children.Add(sourceLabel);

                var messageBox = new System.Windows.Controls.TextBox
                {
                    Height = 32,
                    Padding = new Thickness(6, 4, 6, 4),
                    Background = new SolidColorBrush(Colors.White),
                    BorderBrush = new SolidColorBrush(WpfColor.FromRgb(224, 224, 224)),
                    Foreground = new SolidColorBrush(WpfColor.FromRgb(51, 51, 51)),
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Tag = index
                };
                messageBox.ToolTip = "Enter log message for this branch (will be added as Info_Log).";
                Grid.SetRow(messageBox, 2);
                grid.Children.Add(messageBox);
                _messageBoxes.Add(messageBox);

                border.Child = grid;
                stack.Children.Add(border);
                index++;
            }

            BranchItemsHost.Content = stack;
        }

        /// <summary>Returns the list of log messages in the same order as the branches, or null if cancelled.</summary>
        public IReadOnlyList<string>? GetMessages()
        {
            return _messageBoxes.Select(t => t.Text?.Trim() ?? "").ToList();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Activate();
            Dispatcher.BeginInvoke(new Action(() =>
            {
                Activate();
                if (_messageBoxes.Count > 0)
                {
                    _messageBoxes[0].Focus();
                    Keyboard.Focus(_messageBoxes[0]);
                }
            }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
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
