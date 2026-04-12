using System.Collections.Generic;

namespace AnalyzerHelper
{
    /// <summary>
    /// Read-only analysis reports. Implement and register in ReportRegistry.cs.
    ///
    /// Example:
    ///   public class CommentedActivitiesReport : IReportAction
    ///   {
    ///       public string Name => "Commented Activities";
    ///       public string Category => "Structure";
    ///       public IReadOnlyList<string> DescriptionLines => new[]
    ///       {
    ///           "Lists all ui:CommentOut blocks found across selected files.",
    ///           "Output: CSV report saved to the root folder."
    ///       };
    ///       public string RunReport(string rootFolder, IEnumerable<string> files) { ... }
    ///   }
    /// </summary>
    public interface IReportAction
    {
        string Name { get; }
        string Category { get; }   // e.g. "Structure", "Variables", "Annotations"

        /// <summary>
        /// Lines of description shown in expanded panel. Same rules as IFixerAction.DescriptionLines.
        /// </summary>
        IReadOnlyList<string> DescriptionLines => null;

        string RunReport(string rootFolder, IEnumerable<string> selectedFiles);
    }
}