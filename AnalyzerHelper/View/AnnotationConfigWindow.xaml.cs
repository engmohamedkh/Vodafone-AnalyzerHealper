using System.Windows;
using System.Windows.Input;

namespace AnalyzerHelper.View
{
    public partial class AnnotationConfigWindow : Window
    {
        public string? ComponentName => ComponentNameBox.Text;
        public string? Description => DescriptionBox.Text;
        public string? PreCondition => PreConditionBox.Text;
        public string? PostCondition => PostConditionBox.Text;
        public string? PddSection => PddSectionBox.Text;

        public AnnotationConfigWindow(
            string componentName,
            string description,
            string preCondition,
            string postCondition,
            string pddSection)
        {
            InitializeComponent();
            Owner = System.Windows.Application.Current?.MainWindow;
            ComponentNameBox.Text = componentName ?? "";
            DescriptionBox.Text = description ?? "";
            PreConditionBox.Text = preCondition ?? "";
            PostConditionBox.Text = postCondition ?? "";
            PddSectionBox.Text = pddSection ?? "";
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Activate();
            Dispatcher.BeginInvoke(new Action(() =>
            {
                Activate();
                ComponentNameBox.Focus();
                Keyboard.Focus(ComponentNameBox);
            }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ComponentNameBox.Text))
            {
                System.Windows.MessageBox.Show("Component Name cannot be empty.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                ComponentNameBox.Focus();
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
