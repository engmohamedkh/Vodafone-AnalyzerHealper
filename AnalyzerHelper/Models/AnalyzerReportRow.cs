using System.ComponentModel;

namespace AnalyzerHelper.Models
{
    /// <summary>One row in the Analyzer Report table. ResultMessage and Level are filled when rules are run.</summary>
    public class AnalyzerReportRow : INotifyPropertyChanged
    {
        private bool _isSelectedForFix;
        private bool _isFixed;

        private int _rowNumber;
        public int RowNumber 
        { 
            get => _rowNumber; 
            set { _rowNumber = value; OnPropertyChanged(nameof(RowNumber)); }
        }
        public string RuleId { get; set; } = "";
        public string RuleName { get; set; } = "";
        public string Category { get; set; } = "";
        public string Description { get; set; } = "";
        public string Source { get; set; } = "";
        public string ResultMessage { get; set; } = "";
        public bool HasErrors { get; set; }
        public string FilePath { get; set; } = "";
        public string OriginalFilePath { get; set; } = "";
        public string Recommendation { get; set; } = "";
        public string Level { get; set; } = "";

        /// <summary>Whether the user has checked this row to include in a fix batch.</summary>
        public bool IsSelectedForFix
        {
            get => _isSelectedForFix;
            set { _isSelectedForFix = value; OnPropertyChanged(nameof(IsSelectedForFix)); }
        }

        /// <summary>Whether this row has already been fixed.</summary>
        public bool IsFixed
        {
            get => _isFixed;
            set { _isFixed = value; OnPropertyChanged(nameof(IsFixed)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
