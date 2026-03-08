using System;
using System.Collections.Generic;
using System.Linq;
using UiPath.Studio.Activities.Api;
using UiPath.Studio.Activities.Api.Analyzer;
using UiPath.Studio.Activities.Api.Analyzer.Rules;
using UiPath.Studio.Analyzer.Models;

namespace SetTransactionStatusOnlyInHandoffSpace
{
    /// <summary>
    /// Per framework: only the sequence invoked from the Handoff state (settransactionstatus.xaml)
    /// may contain Set Transaction Status activity. If Set Transaction Status exists in any other
    /// workflow, report an error.
    /// </summary>
    public class SetTransactionStatusOnlyInHandoff : IRegisterAnalyzerConfiguration
    {
        const string RuleID = "VF-062";
        const string RuleName = "SetTransactionStatusOnlyInHandoff";
        const string WFAnalyzerVersion = "WorkflowAnalyzerV4";

        /// <summary>Activity name as in toolbox / display (Orchestrator Set Transaction Status).</summary>
        const string SetTransactionStatusActivityName = "SetTransactionStatus";

        /// <summary>Allowed workflow: the one invoked from Handoff state in main state machine.</summary>
        private static bool IsSetTransactionStatusWorkflow(IWorkflowModel workflow)
        {
            if (workflow?.RelativePath == null)
                return false;
            return workflow.RelativePath.ToLower().Split('_').Last().ToLower().Equals("settransactionstatus.xaml");
        }

        /// <summary>Recursively checks if any activity in the tree is Set Transaction Status.</summary>
        private static bool ContainsSetTransactionStatusActivity(IActivityModel activity)
        {
            if (activity == null)
                return false;
            if (activity.ToolboxName != null &&
                activity.ToolboxName.IndexOf(SetTransactionStatusActivityName, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (activity.DisplayName != null &&
                activity.DisplayName.IndexOf("Set Transaction Status", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (activity.Children != null)
            {
                foreach (var child in activity.Children)
                {
                    if (ContainsSetTransactionStatusActivity(child))
                        return true;
                }
            }
            return false;
        }

        public void Initialize(IAnalyzerConfigurationService workflowAnalyzerConfigService)
        {
            if (!workflowAnalyzerConfigService.HasFeature(WFAnalyzerVersion))
                return;

            var newRule = new Rule<IWorkflowModel>(RuleName, RuleID, SetTransactionStatusOnlyInHandoffRole)
            {
                ErrorLevel = System.Diagnostics.TraceLevel.Error,
                RecommendationMessage = "Set Transaction Status activity is only allowed in the workflow invoked from Handoff state (settransactionstatus.xaml). Remove it from any other workflow."
            };

            workflowAnalyzerConfigService.AddRule<IWorkflowModel>(newRule);
        }

        private InspectionResult SetTransactionStatusOnlyInHandoffRole(IWorkflowModel currentWorkflow, Rule theNewRule)
        {
            // Only settransactionstatus.xaml is allowed to contain Set Transaction Status
            if (IsSetTransactionStatusWorkflow(currentWorkflow))
                return new InspectionResult() { HasErrors = false };

            if (currentWorkflow?.Root == null)
                return new InspectionResult() { HasErrors = false };

            bool found = ContainsSetTransactionStatusActivity(currentWorkflow.Root);
            if (!found)
                return new InspectionResult() { HasErrors = false };

            var messageList = new List<string>
            {
                string.Format("Workflow '{0}' contains Set Transaction Status activity. Per framework, this activity is only allowed in the sequence invoked from the Handoff state (settransactionstatus.xaml). Remove it from this workflow.",
                    currentWorkflow.DisplayName)
            };

            return new InspectionResult()
            {
                HasErrors = true,
                Messages = messageList,
                RecommendationMessage = "Set Transaction Status activity is only allowed in settransactionstatus.xaml (invoked from Handoff state). Remove it from any other workflow.",
                ErrorLevel = theNewRule.DefaultErrorLevel
            };
        }
    }
}
