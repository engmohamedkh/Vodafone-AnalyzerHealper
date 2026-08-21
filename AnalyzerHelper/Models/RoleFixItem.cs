using System.ComponentModel;

namespace AnalyzerHelper.Models
{
    /// <summary>A single rule item for the UI (No interaction / Need interaction / Validate only tabs).</summary>
    public class RoleFixItem : INotifyPropertyChanged
    {
        private string _ruleId = "";
        private string _displayName = "";
        private string _description = "";
        private FixCategory _category;
        private bool _isSelected;

        public string RuleId { get => _ruleId; set { _ruleId = value; OnPropertyChanged(nameof(RuleId)); } }
        public string DisplayName { get => _displayName; set { _displayName = value; OnPropertyChanged(nameof(DisplayName)); } }
        public string Description { get => _description; set { _description = value; OnPropertyChanged(nameof(Description)); } }

        public FixCategory Category
        {
            get => _category;
            set
            {
                _category = value;
                OnPropertyChanged(nameof(Category));
                OnPropertyChanged(nameof(CanAutofix));
                OnPropertyChanged(nameof(IsValidateOnly));
                OnPropertyChanged(nameof(CategoryLabel));
            }
        }

        public bool CanAutofix => Category == FixCategory.AutoFix;
        public bool IsValidateOnly => Category == FixCategory.ValidateOnly;

        public string CategoryLabel => Category switch
        {
            FixCategory.AutoFix => "Auto fix",
            FixCategory.RequiresUserInteraction => "Requires your input",
            FixCategory.ValidateOnly => "Validate only",
            _ => "Unknown"
        };

        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
