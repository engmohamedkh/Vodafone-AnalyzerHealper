using System.ComponentModel;

namespace AnalyzerHelper.Models
{
    /// <summary>A single rule fix item for the UI (No interaction / Need interaction tabs).</summary>
    public class RoleFixItem : INotifyPropertyChanged
    {
        private string _ruleId = "";
        private string _displayName = "";
        private string _description = "";
        private FixCategory _category;
        private bool _canAutofix;
        private bool _isSelected;

        public string RuleId { get => _ruleId; set { _ruleId = value; OnPropertyChanged(nameof(RuleId)); } }
        public string DisplayName { get => _displayName; set { _displayName = value; OnPropertyChanged(nameof(DisplayName)); } }
        public string Description { get => _description; set { _description = value; OnPropertyChanged(nameof(Description)); } }

        public FixCategory Category
        {
            get => _category;
            set { _category = value; _canAutofix = value == FixCategory.AutoFix; OnPropertyChanged(nameof(Category)); OnPropertyChanged(nameof(CanAutofix)); OnPropertyChanged(nameof(CategoryLabel)); }
        }

        public bool CanAutofix
        {
            get => _canAutofix;
            set { _canAutofix = value; _category = value ? FixCategory.AutoFix : FixCategory.RequiresUserInteraction; OnPropertyChanged(nameof(CanAutofix)); OnPropertyChanged(nameof(Category)); OnPropertyChanged(nameof(CategoryLabel)); }
        }

        public string CategoryLabel => Category == FixCategory.AutoFix ? "Auto fix" : "Requires your input";

        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
