namespace AnalyzerHelper.Interfaces
{
    /// <summary>
    /// Fix tab: rule defines the error and applies fix. No dependency on report.
    /// Report uses Check(); Fix tab uses DefineAndFix().
    /// </summary>
    public interface IAnalyzerRuleWithFix : IAnalyzerRule
    {
        bool DefineAndFix(string filePath, string content, out string newContent);
    }

    public interface IBatchAnalyzerRuleWithFix : IAnalyzerRuleWithFix
    {
        void PrepareBatch(System.Collections.Generic.IReadOnlyList<string> filePaths);
    }
}
