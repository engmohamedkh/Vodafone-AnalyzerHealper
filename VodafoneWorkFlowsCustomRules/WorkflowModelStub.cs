using System;
using System.Collections.Generic;
using UiPath.Studio.Analyzer.Models;

namespace VodafoneWorkFlowsCustomRules
{
    /// <summary>
    /// Minimal IWorkflowModel for running rules outside Studio. Used by RuleRunner to get validation results (errors/warnings).
    /// </summary>
    public sealed class WorkflowModelStub : IWorkflowModel
    {
        public IActivityModel Root => null;
        public IReadOnlyCollection<IArgumentModel> Arguments => Array.Empty<IArgumentModel>();
        public IReadOnlyCollection<string> ImportedNamespaces => Array.Empty<string>();
        public IProjectSummary Project { get; }
        public IReadOnlyCollection<string> Assemblies => Array.Empty<string>();
        public string RelativePath { get; }
        public string DisplayName { get; }

        public WorkflowModelStub(string projectDirectory, string relativePath, string displayName)
        {
            Project = new ProjectSummaryStub(projectDirectory ?? "");
            RelativePath = relativePath ?? "";
            DisplayName = displayName ?? System.IO.Path.GetFileNameWithoutExtension(relativePath ?? "");
        }

        private sealed class ProjectSummaryStub : IProjectSummary
        {
            public string Directory { get; }
            public IReadOnlyCollection<string> FileNames => Array.Empty<string>();
            public IReadOnlyCollection<IDependency> Dependencies => Array.Empty<IDependency>();
            public string ProjectOutputType => "";
            public string ProjectProfileType => "";
            public string ExpressionLanguage => "";
            public bool RequiresUserInteraction => false;
            public bool SupportsPersistence => false;
            public bool HasModernBehavior => false;
            public string EntryPointName => "";
            public string ProjectFilePath => "";
            public string ExceptionHandlerWorkflowName => "";
            public IReadOnlyCollection<string> EntryPoints => Array.Empty<string>();
            public IReadOnlyCollection<string> IgnoredFiles => Array.Empty<string>();
            public IReadOnlyCollection<string> TestCases => Array.Empty<string>();
            public IReadOnlyCollection<string> FileTemplates => Array.Empty<string>();
           // public IReadOnlyCollection<ITemplateModel> Templates => Array.Empty<ITemplateModel>();
            public IObjectBrowserSummary ObjectBrowserSummary => null;
            public string DisplayName => "";

            public ProjectSummaryStub(string directory) => Directory = directory;
        }
    }
}
