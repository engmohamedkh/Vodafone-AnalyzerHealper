using UiPath.Studio.Analyzer.Models;

namespace VodafoneWorkFlowsCustomRules
{
    /// <summary>
    /// Centralized workflow role detection for analyzer rules.
    /// Aligned with AnalyzerHelper desktop app role list (reference).
    /// </summary>
    public static class WorkflowRoleHelper
    {
        public enum WorkflowRole
        {
            Unknown,
            Loader,
            Worker,
            LoaderWorker,
            Process,
            Subprocess,
            Logic,
            Automation,
            Framework,
            Integration
        }

        public static WorkflowRole GetRole(IWorkflowModel workflow)
        {
            if (workflow?.RelativePath == null) return WorkflowRole.Unknown;
            var path = (workflow.Project?.Directory + "\\" + workflow.RelativePath).ToLowerInvariant();
            var name = workflow.DisplayName?.ToLowerInvariant() ?? "";
            var fileName = System.IO.Path.GetFileName(workflow.RelativePath)?.ToLowerInvariant() ?? "";

            if (name.Contains("_loaderworker") || path.Contains("_loaderworker")) return WorkflowRole.LoaderWorker;
            if (name.Contains("_loader") || path.Contains("_loader") || fileName == "loader.xaml") return WorkflowRole.Loader;
            if (name.Contains("_worker") || path.Contains("_worker") || fileName == "worker.xaml") return WorkflowRole.Worker;
            if (path.Contains("subprocess")) return WorkflowRole.Subprocess;
            if (path.Contains("logic")) return WorkflowRole.Logic;
            if (path.Contains("automation")) return WorkflowRole.Automation;
            if (path.Contains("processname_") || name.Contains("processname_")) return WorkflowRole.Process;
            if (path.Contains("iap_") || name.Contains("iap_")) return WorkflowRole.Framework;
            if (path.Contains("framework")) return WorkflowRole.Framework;
            if (path.Contains("integration")) return WorkflowRole.Integration;

            return WorkflowRole.Unknown;
        }

        public static bool IsExcludedFromStandardRules(IWorkflowModel workflow)
        {
            var role = GetRole(workflow);
            return role == WorkflowRole.Loader || role == WorkflowRole.Worker || role == WorkflowRole.LoaderWorker
                   || role == WorkflowRole.Process || role == WorkflowRole.Subprocess || role == WorkflowRole.Framework;
        }

        public static bool IsAutomation(IWorkflowModel workflow) => GetRole(workflow) == WorkflowRole.Automation;

        public static bool IsLogicOrComponent(IWorkflowModel workflow)
        {
            if (workflow?.RelativePath == null) return false;
            var path = (workflow.Project?.Directory + "\\" + workflow.RelativePath).ToLowerInvariant();
            var role = GetRole(workflow);
            return role == WorkflowRole.Logic || path.Contains("\\navigate\\") || path.Contains("\\read\\") || path.Contains("\\write\\");
        }
    }
}
