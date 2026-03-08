using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UiPath.Studio.Activities.Api;
using UiPath.Studio.Activities.Api.Analyzer;
using UiPath.Studio.Activities.Api.Analyzer.Rules;
using UiPath.Studio.Analyzer.Models;

namespace ArchetypeLogsSpace

{
    public class ArchetypeLogs : IRegisterAnalyzerConfiguration
    {
        // check list size
        public static bool IsEmpty<T>(List<T> list)
        {
            if (list == null)
            {
                return true;
            }

            return !list.Any();
        }

        // Configs
        string RuleID = "VF-025";
        string RuleName = "Archtypes";
        string WFAnalyzerVersion = "WorkflowAnalyzerV4";
        string AnalyticActivity = "analytics_log";

        // init the rule to the studio
        public void Initialize(IAnalyzerConfigurationService workflowAnalyzerConfigService)
        {
            // checking the wf version
            if (!workflowAnalyzerConfigService.HasFeature(WFAnalyzerVersion))
                return;

            var newRule = new Rule<IActivityModel>(RuleName, RuleID, ArchetypeLogsList)
            {
                /// Off and Verbose are not supported.
                ErrorLevel = System.Diagnostics.TraceLevel.Info,
                RecommendationMessage = "List all archtype logs which used in the project/workflow."
            };

            workflowAnalyzerConfigService.AddRule<IActivityModel>(newRule);
        }

        // Rule implementation
        private InspectionResult ArchetypeLogsList(IActivityModel activity, Rule theNewRule)
        {
            var messageList = new List<string>();


            // Throw activity
            if (activity.ToolboxName.ToLower().Contains(AnalyticActivity.ToLower()))
            {
                messageList.Add(string.Format("The following activity {0} has archtypes code: {1}", activity.DisplayName, activity.Arguments.FirstOrDefault(x => x.DisplayName.ToLower().Contains("archetype")).DefinedExpression));
                return new InspectionResult()
                {
                    HasErrors = true,
                    Messages = messageList,
                    RecommendationMessage = "List of all archtype logs which used in the project/workflow.",
                    ErrorLevel = theNewRule.DefaultErrorLevel,
                };
            }


            return new InspectionResult()
            {
                HasErrors = false
            };
        }
    }
}