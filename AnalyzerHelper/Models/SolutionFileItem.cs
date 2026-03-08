using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AnalyzerHelper.Models
{
    /// <summary>
    /// A file from the loaded UiPath solution. Shown in the side menu with checkbox for "apply rules to this file".
    /// </summary>
    public class SolutionFileItem : INotifyPropertyChanged
    {
        private bool _isSelected = true;

        public string FullPath { get; set; } = "";
        /// <summary>Path relative to solution root for display.</summary>
        public string DisplayPath { get; set; } = "";

        /// <summary>When true, Run rules / Apply fix will include this file.</summary>
        public bool IsSelected
        {
            get => _isSelected;
            set { if (_isSelected == value) return; _isSelected = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
