namespace AnalyzerHelper.Models
{
    /// <summary>Whether a rule can autofix, needs user input, or only validates/reports.</summary>
    public enum FixCategory
    {
        /// <summary>Can be applied by the app without user decisions.</summary>
        AutoFix,

        /// <summary>Requires user input or confirmation.</summary>
        RequiresUserInteraction,

        /// <summary>Report findings only — no autofix and no interactive fix.</summary>
        ValidateOnly
    }
}
