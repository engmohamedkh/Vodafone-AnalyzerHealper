using System.Collections.Generic;

namespace VodafoneActivitiesCustomRules
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
    /// Exposes rule metadata for AnalyzerHelper report (activity rules run inside Studio).
    /// </summary>
    public static class RuleRegistry
    {
        public const string SourceName = "Vodafone Activity Rules";

        /// <summary>Returns metadata for all activity rules registered in this project.</summary>
        public static IReadOnlyList<RuleReportMetadata> GetReportMetadata()
        {
            return new List<RuleReportMetadata>
            {
                new() { RuleId = "VF-016", RuleName = "ProhibitedActivities", Category = "Activity", Description = "Prohibited activities." },
                new() { RuleId = "VF-060", RuleName = "SelectorAttribute", Category = "Activity", Description = "Selector attribute." },
                new() { RuleId = "VF-061", RuleName = "SelectorAttributeValue", Category = "Activity", Description = "Selector attribute value." },
                new() { RuleId = "VF-002", RuleName = "ImageBasedActivities", Category = "Activity", Description = "Image-based activities." },
                new() { RuleId = "VF-003", RuleName = "UndocumentedDelay", Category = "Activity", Description = "Undocumented delay." },
                new() { RuleId = "VF-010", RuleName = "SimulateAndSendWindowMessage", Category = "Activity", Description = "Simulate and send window message." },
                new() { RuleId = "VF-015", RuleName = "BusinessSystemException", Category = "Activity", Description = "Business/system exception." },
                new() { RuleId = "VF-025", RuleName = "Archtypes", Category = "Activity", Description = "Archetype logs." },
                new() { RuleId = "VF-014", RuleName = "MissingInOutArgument", Category = "Activity", Description = "Missing In/Out argument." },
                new() { RuleId = "VF-024", RuleName = "WorkqueueEncryption", Category = "Activity", Description = "Workqueue encryption." },
                new() { RuleId = "VF-017", RuleName = "HardCodedPasswords", Category = "Activity", Description = "Hard-coded passwords." },
                new() { RuleId = "VF-008", RuleName = "Log Browser URL", Category = "Activity", Description = "Log browser URL." },
                new() { RuleId = "VF-027", RuleName = "CommentOutActivity", Category = "Activity", Description = "Comment out activity." },
                new() { RuleId = "VF-029", RuleName = "IfElseCheck", Category = "Activity", Description = "If/Else check." },
                new() { RuleId = "VF-035", RuleName = "VariablesNaming", Category = "Activity", Description = "Variables naming." },
                new() { RuleId = "VF-038", RuleName = "Branches Logging", Category = "Activity", Description = "Branches logging." },
            };
        }
    }
}
