using System.Collections.Generic;

namespace AnalyzerHelper
{
    /// <summary>
    /// Implement this interface to add a new action to the UI.
    /// Register in ActionRegistry.cs.
    ///
    /// Example implementation showing all optional members:
    ///
    ///   public class FixConditionChecksAction : IFixerAction
    ///   {
    ///       public string Name => "Fix Condition Checks";
    ///
    ///       // Multiple lines — each becomes its own visual line in the expanded panel.
    ///       // Use null/empty string for a blank spacer line.
    ///       public IReadOnlyList<string> DescriptionLines => new[]
    ///       {
    ///           "Detects Pre/Post Condition sequences where the UiElementExists check",
    ///           "has been commented out or removed, leaving boolPreConditionMet always False.",
    ///           "",
    ///           "Restore options: Uncomment existing check, or clone from sibling sequence."
    ///       };
    ///
    ///       // Rule codes shown as small blue badges next to the action name.
    ///       // Omit the property (or return null) if this action has no rule codes.
    ///       public IReadOnlyList<string> RuleCodes => new[] { "VF-012", "ST-USG-004" };
    ///
    ///       public bool Run(string filePath) { ... }
    ///   }
    /// </summary>
    public interface IFixerAction
    {
        string Name { get; }

        /// <summary>
        /// Lines of description shown when the user expands the row.
        /// Each string is one visual line. Use "" for a blank spacer.
        /// Return null or empty list if no description (chevron hidden).
        /// </summary>
        IReadOnlyList<string> DescriptionLines => null;

        /// <summary>Backwards-compat shim — prefer DescriptionLines.</summary>
        string Description => null;

        bool Run(string filePath);

        // ── Rule codes (optional) ─────────────────────────────────────────────
        /// <summary>
        /// Rule codes this action fixes, e.g. new[] { "VF-012", "ST-USG-009" }.
        /// Shown as small inline badges next to the name. Null = no badges.
        /// </summary>
        IReadOnlyList<string> RuleCodes => null;

        // ── Batch / config mode (optional) ───────────────────────────────────
        bool NeedsConfig => false;
        bool ShowConfigDialog() => true;
        int RunBatch(string rootFolder) => 0;

        // ── Report mode (optional) ────────────────────────────────────────────
        IReadOnlyList<string> RunReport(IReadOnlyList<string> filePaths) => null;
        IReadOnlyList<string> ReportHeaders => null;
    }
}