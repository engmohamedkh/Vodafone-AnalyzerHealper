//using AnalyzerHelper;
using System.Collections.Generic;

namespace AnalyzerHelper
{
    /// <summary>
    /// Central registry of every fixer action available in the tool.
    /// To add a new action: implement IFixerAction in a new file,
    /// then add one line here registering it.
    /// </summary>
    public static class ActionRegistry
    {
        public static IReadOnlyList<IFixerAction> All { get; } = new List<IFixerAction>
        {
            new RenameWorkflowFilesAction(),
            new FixAnnotationAction(),
            new FixComponentNameAction(),
            new RemoveVarArgDefaultsAction(),
            new RemoveUnusedVarsArgsAction(),
            new SyncInvokeArgumentsAction(),
            new EnsureCatchRethrowAction(),
            new AddRetryScopeCheckTrueAction(),
            new RemoveCommentedActivitiesAction(),

        };
    }
}