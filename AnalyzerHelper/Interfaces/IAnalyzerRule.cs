using System.Collections.Generic;
using AnalyzerHelper.Models;

namespace AnalyzerHelper.Interfaces
{
    /// <summary>
    /// A standard rule that checks a workflow file. No SDK – just file path and content.
    /// Return one result per finding (error/warning), or empty if the file passes.
    /// </summary>
    public interface IAnalyzerRule
    {
        string RuleId { get; }
        string RuleName { get; }
        string DefaultRecommendation { get; }
        bool RequiresUserInteraction { get; }
        IReadOnlyList<RuleCheckResult> Check(string filePath, string content);
    }
}
