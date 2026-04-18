using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AnalyzerHelper.View
{
    public partial class RenameConfigWindow : Window
    {
        private readonly bool _requireStage;

        public string ShortName => ShortNameBox.Text?.Trim() ?? "";
        public string ProcessStage => (ProcessStageCombo.SelectedItem as ComboBoxItem)?.Content?.ToString()?.Trim() ?? "";

        public RenameConfigWindow(bool requireStage = true)
        {
            _requireStage = requireStage;
            InitializeComponent();
            Owner = System.Windows.Application.Current?.MainWindow;

            if (_requireStage)
            {
                ProcessStageLabel.Visibility = Visibility.Visible;
                ProcessStageCombo.Visibility = Visibility.Visible;
                ProcessStageCombo.SelectedIndex = 0; // Default to Worker
                this.Height = 240; 
            }
            else
            {
                ProcessStageLabel.Visibility = Visibility.Collapsed;
                ProcessStageCombo.Visibility = Visibility.Collapsed;
                this.Height = 180;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Activate();
            // Defer focus so the text box gets it after layout and modal is fully active (avoids not being able to type)
            Dispatcher.BeginInvoke(new System.Action(() =>
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
                System.Windows.MessageBox.Show("Process Stage is required for Subprocess workflows.\nPlease select: Worker, Loader, or LoaderWorker.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                ProcessStageCombo.Focus();
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
