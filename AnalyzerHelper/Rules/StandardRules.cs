using System.Collections.Generic;
using System.Linq;
using AnalyzerHelper.Interfaces;

namespace AnalyzerHelper.Rules
{
    /// <summary>All standard rules. Used by Report and by No/Need interaction tabs.</summary>
    public static class StandardRules
    {
        private static readonly IAnalyzerRule[] AllRules =
        {
            new TryCatchRule(),
            new RemoveDefaultsVarArgRule(),
            new HandleCommentedActivitiesRule(),
            new WorkflowFileNamingRule(),
            new AnnotationRule(),
            new BranchesLogsRule(),
        };

        public static IReadOnlyList<IAnalyzerRule> GetAll() => AllRules;
        public static IReadOnlyList<IAnalyzerRule> GetAutoFixRules() =>
            AllRules.Where(r => !r.RequiresUserInteraction).ToArray();
        public static IReadOnlyList<IAnalyzerRule> GetNeedInteractionRules() =>
            AllRules.Where(r => r.RequiresUserInteraction).ToArray();
    }
}
