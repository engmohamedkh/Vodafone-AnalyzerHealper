using System.Collections.Generic;
using System.Linq;
using AnalyzerHelper.Interfaces;

namespace AnalyzerHelper.Rules
{
    /// <summary>All standard rules. Used by Report and by No/Need/Validate-only tabs.</summary>
    public static class StandardRules
    {
        private static readonly IAnalyzerRule[] AllRules =
        {
            // Auto-fix
            new TryCatchRule(),
            new RemoveDefaultsVarArgRule(),
            new AddRetryScopeCheckTrueRule(),
            new RemoveUnusedVarArgRule(),
            new SyncInvokeArgumentsRule(),
            new FixComponentNameRule(),
            new FlowchartOrphanNodesRule(),
            new CetTimeZoneRule(),

            // Need interaction
            new HandleCommentedActivitiesRule(),
            new WorkflowFileNamingRule(),
            new AnnotationRule(),
            new BranchesLogsRule(),
            new UnusedWorkflowFilesRule(),

            // Validate only (Check, no DefineAndFix)
            new ConfigConstantsUsageRule(),
            new ProhibitedActivitiesRule(),
            new SelectorAttributeRule(),
            new ImageBasedActivitiesRule(),
            new UndocumentedDelayRule(),
            new BusinessSystemExceptionRule(),
            new ArchtypesRule(),
            new WorkqueueEncryptionRule(),
            new HardCodedPasswordsRule(),
            new MicrosoftOfficeActivitiesRule(),
            new HardCodedDelaysRule(),
            new NestedIfsRule(),
        };

        public static IReadOnlyList<IAnalyzerRule> GetAll() => AllRules;

        public static bool HasFix(IAnalyzerRule rule) =>
            rule is IAnalyzerRuleWithFix || rule is IBatchAnalyzerRuleWithFix;

        /// <summary>Autofix rules: implement fix and do not require user interaction.</summary>
        public static IReadOnlyList<IAnalyzerRule> GetAutoFixRules() =>
            AllRules.Where(r => HasFix(r) && !r.RequiresUserInteraction).ToArray();

        /// <summary>Interactive fix rules.</summary>
        public static IReadOnlyList<IAnalyzerRule> GetNeedInteractionRules() =>
            AllRules.Where(r => r.RequiresUserInteraction).ToArray();

        /// <summary>Validate/report only — no fix path.</summary>
        public static IReadOnlyList<IAnalyzerRule> GetValidateOnlyRules() =>
            AllRules.Where(r => !HasFix(r) && !r.RequiresUserInteraction).ToArray();
    }
}
