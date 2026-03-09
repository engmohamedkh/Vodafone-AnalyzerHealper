using System.Windows;
using System.Windows.Input;

namespace AnalyzerHelper.View
{
    public partial class RenameConfigWindow : Window
    {
        private readonly bool _requireStage;

        public string ShortName => ShortNameBox.Text?.Trim() ?? "";
        public string? ProcessStage => ProcessStageBox.Text?.Trim();

        public RenameConfigWindow(bool requireStage = true)
        {
            _requireStage = requireStage;
            InitializeComponent();
            Owner = System.Windows.Application.Current?.MainWindow;
            if (!_requireStage)
            {
                ProcessStageLabel.Visibility = Visibility.Collapsed;
                ProcessStageBox.Visibility = Visibility.Collapsed;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Activate();
            // Defer focus so the text box gets it after layout and modal is fully active (avoids not being able to type)
            Dispatcher.BeginInvoke(new Action(() =>
            {
                Activate();
                ShortNameBox.Focus();
                Keyboard.Focus(ShortNameBox);
                ShortNameBox.SelectAll();
            }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ShortName))
            {
                System.Windows.MessageBox.Show("Project Short Name cannot be empty.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                ShortNameBox.Focus();
                return;
            }
            if (_requireStage && string.IsNullOrWhiteSpace(ProcessStage))
            {
                System.Windows.MessageBox.Show("Process Stage is required for Subprocess workflows.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                ProcessStageBox.Focus();
                return;
            }
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
