namespace AnalyzerHelper.Models
{
    /// <summary>One finding from a rule when checking a file. Shown in the Report tab.</summary>
    public class RuleCheckResult
    {
        public string RuleId { get; set; } = "";
        public string RuleName { get; set; } = "";
        public RuleLevel Level { get; set; }
        public string Message { get; set; } = "";
        public string FilePath { get; set; } = "";
        public string Recommendation { get; set; } = "";
        public bool RequiresUserInteraction { get; set; }
    }
}
