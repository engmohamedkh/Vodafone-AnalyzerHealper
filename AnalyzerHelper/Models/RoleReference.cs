namespace AnalyzerHelper.Models
{
    /// <summary>Reference list of workflow role names (for alignment with analyzer roles).</summary>
    public static class RoleReference
    {
        public static string[] WorkflowRoleNames { get; } =
        {
            "Unknown", "Loader", "Worker", "LoaderWorker", "Process",
            "Subprocess", "Logic", "Automation", "Framework", "Integration"
        };
    }
}
