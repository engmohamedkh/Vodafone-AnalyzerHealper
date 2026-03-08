namespace AnalyzerHelper.Models
{
    /// <summary>Whether a rule fix can be applied automatically or needs user interaction.</summary>
    public enum FixCategory
    {
        /// <summary>Can be applied by the app without user decisions (no interaction).</summary>
        AutoFix,

        /// <summary>Requires user input or confirmation (need interaction).</summary>
        RequiresUserInteraction
    }
}
