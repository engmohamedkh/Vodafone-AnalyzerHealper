using System.Windows;

namespace AnalyzerHelper.View
{
    public partial class RenameConfigWindow : Window
    {
        public string ShortName => ShortNameBox.Text?.Trim() ?? "";
        public string? ProcessStage => ProcessStageBox.Text?.Trim();

        public RenameConfigWindow()
        {
            InitializeComponent();
            Owner = System.Windows.Application.Current?.MainWindow;
            ShortNameBox.Focus();
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
