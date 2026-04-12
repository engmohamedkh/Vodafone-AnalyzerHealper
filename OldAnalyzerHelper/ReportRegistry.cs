using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace AnalyzerHelper
{
    /// <summary>
    /// Central registry for all IReportAction implementations.
    /// Add new reports here — they automatically appear in the Reports tab.
    /// </summary>
    public static class ReportRegistry
    {
        private static readonly List<IReportAction> _all = new List<IReportAction>
        {
            // ── Register report implementations here ──────────────────────
            // new CommentedActivitiesReportAction(),
            // new UnusedVariablesReportAction(),
            // new AnnotationCoverageReportAction(),
            // ─────────────────────────────────────────────────────────────
        };

        public static IReadOnlyList<IReportAction> All =>
            new ReadOnlyCollection<IReportAction>(_all);
    }
}