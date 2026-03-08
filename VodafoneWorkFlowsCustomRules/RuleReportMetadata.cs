using System.Collections.Generic;

namespace VodafoneWorkFlowsCustomRules
{
    /// <summary>
    /// One rule's metadata for the Analyzer Report (used by AnalyzerHelper).
    /// </summary>
    public class RuleReportMetadata
    {
        public string RuleId { get; set; } = "";
        public string RuleName { get; set; } = "";
        public string Category { get; set; } = "";
        public string Description { get; set; } = "";
    }

    /// <summary>
    /// Result of running one workflow rule (used by AnalyzerHelper to fill the report).
    /// </summary>
    public class RuleRunResult
    {
        public string RuleId { get; set; } = "";
        public string RuleName { get; set; } = "";
        public bool HasErrors { get; set; }
        public string Message { get; set; } = "";
        public string FilePath { get; set; } = "";
    }

    /// <summary>
    /// Exposes rule metadata and run capability for AnalyzerHelper report.
    /// </summary>
    public static class RuleRegistry
    {
        public const string SourceName = "Vodafone Workflow Rules";

        /// <summary>Returns metadata for all workflow rules registered in this project.</summary>
        public static IReadOnlyList<RuleReportMetadata> GetReportMetadata()
        {
            return new List<RuleReportMetadata>
            {
                new() { RuleId = "VF-021", RuleName = "ValidateWorkFlowName", Category = "Workflow", Description = "Validate workflow naming." },
                new() { RuleId = "VF-012", RuleName = "TryCatch", Category = "Workflow", Description = "TryCatch and exception logging." },
                new() { RuleId = "VF-022", RuleName = "RetryScope", Category = "Workflow", Description = "Retry scope usage." },
                new() { RuleId = "VF-028", RuleName = "HardcodedArguments", Category = "Workflow", Description = "Hardcoded arguments." },
                new() { RuleId = "VF-030", RuleName = "AddLogFields", Category = "Workflow", Description = "Add log fields." },
                new() { RuleId = "VF-033", RuleName = "Annotations", Category = "Workflow", Description = "Annotations." },
                new() { RuleId = "VF-032", RuleName = "LogicFileUICheck", Category = "Workflow", Description = "Logic file UI check." },
                new() { RuleId = "VF-034", RuleName = "Naming Convention", Category = "Workflow", Description = "Naming convention." },
                new() { RuleId = "VF-037", RuleName = "UnusedArguments", Category = "Workflow", Description = "Unused arguments." },
                new() { RuleId = "VF-040", RuleName = "Activities Number", Category = "Workflow", Description = "Activities number." },
                new() { RuleId = "VF-042", RuleName = "Multiple Apps Sequence", Category = "Workflow", Description = "Multiple apps sequence." },
                new() { RuleId = "VF-049", RuleName = "InputValidation", Category = "Workflow", Description = "Input validation." },
                new() { RuleId = "VF-062", RuleName = "SetTransactionStatusOnlyInHandoff", Category = "Workflow", Description = "Set Transaction Status only in Handoff workflow." },
                new() { RuleId = "VF-063", RuleName = "SelectorAttributeFromXaml", Category = "Workflow", Description = "Selector attribute from XAML." },
                new() { RuleId = "VF-064", RuleName = "SelectorAttributeValueFromXaml", Category = "Workflow", Description = "Selector attribute value from XAML." },
            };
        }
    }
}
