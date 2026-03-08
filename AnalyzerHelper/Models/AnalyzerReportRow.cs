namespace AnalyzerHelper.Models
{
    /// <summary>One row in the Analyzer Report table. ResultMessage and Level are filled when rules are run.</summary>
    public class AnalyzerReportRow
    {
        public string RuleId { get; set; } = "";
        public string RuleName { get; set; } = "";
        public string Category { get; set; } = "";
        public string Description { get; set; } = "";
        public string Source { get; set; } = "";
        public string ResultMessage { get; set; } = "";
        public bool HasErrors { get; set; }
        public string FilePath { get; set; } = "";
        public string Recommendation { get; set; } = "";
        public string Level { get; set; } = "";
    }
}
