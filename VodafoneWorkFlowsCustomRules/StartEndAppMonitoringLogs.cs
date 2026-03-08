using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UiPath.Studio.Activities.Api;
using UiPath.Studio.Activities.Api.Analyzer;
using UiPath.Studio.Activities.Api.Analyzer.Rules;
using UiPath.Studio.Analyzer.Models;


namespace StartEndAppMonitoringLogsSpace
{
    public class StartEndAppMonitoringLogs : IRegisterAnalyzerConfiguration
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
        string RuleID = "VF-026";
        string RuleName = "StartEndAppMonitoringLogs";
        string WFAnalyzerVersion = "WorkflowAnalyzerV4";
        string ExcludedWorkFlows = "";

        // init the rule to the studio
        public void Initialize(IAnalyzerConfigurationService workflowAnalyzerConfigService)
        {
            // checking the wf version
            if (!workflowAnalyzerConfigService.HasFeature(WFAnalyzerVersion))
                return;
           
            var newRule = new Rule<IWorkflowModel>(RuleName, RuleID, StartEndAppMonitoringLogsCheck)
            {
                /// Off and Verbose are not supported.
                ErrorLevel = System.Diagnostics.TraceLevel.Info,
                RecommendationMessage = "Checking IAP loging App Monitoring (Start/End)."
            };

            workflowAnalyzerConfigService.AddRule<IWorkflowModel>(newRule);
        }

        // Implemetation
        private InspectionResult StartEndAppMonitoringLogsCheck(IWorkflowModel CurrentWorkflow, Rule theNewRule)
        {
            var messageList = new List<string>();
            bool AppMonitoringStart = false, AppMonitoringEnd = false;

            // ExceptedWorkFlows always success
            if (ExcludedWorkFlows.Contains(CurrentWorkflow.DisplayName))
            {
                // No Error message existed return Success
                return new InspectionResult()
                {
                    HasErrors = false
                };
            }

            // no sequence or workflow added
            if (CurrentWorkflow.Root == null)
            {
                // No Error message existed return Success
                
            }
            // For any WF type (Sequence or Flowchart)
            else if (CurrentWorkflow.Root.ToolboxName.Contains("Sequence") || CurrentWorkflow.Root.ToolboxName.Contains("Flowchart"))
            {
                // no activities yet
                if (CurrentWorkflow.Root.Children.ToList().Count == 0)
                {
                    // No Error message existed return Success
                    return new InspectionResult()
                    {
                        HasErrors = false
                    };
                }

                var childs = CurrentWorkflow.Root.Children.ToList();

                // Start Log message
                if (childs[0].ToolboxName.ToLower().Contains("app_monitoring") && childs[0].ToolboxName.ToLower().Contains("start"))
                {
                    AppMonitoringStart = true;
                }

                // End Log message
                if (childs[childs.Count - 1].ToolboxName.ToLower().Contains("app_monitoring") && childs[childs.Count - 1].ToolboxName.ToLower().Contains("end"))
                {
                    AppMonitoringEnd = true;
                }

                if (AppMonitoringEnd && AppMonitoringStart)
                {
                    messageList.Add(string.Format("The following workflow: {0} has start/end app monitoring logging activity.", CurrentWorkflow.DisplayName));
                }
                else if (AppMonitoringEnd)
                {

                    messageList.Add(string.Format("The following workflow: {0} has end app monitoring logging activity.", CurrentWorkflow.DisplayName));
                }
                else if (AppMonitoringStart)
                {

                    messageList.Add(string.Format("The following workflow: {0} has start app monitoring logging activity.", CurrentWorkflow.DisplayName));
                }
                else { }
            }

            // errors existed
            if (!IsEmpty(messageList))
            {
                return new InspectionResult()
                {
                    HasErrors = true,
                    Messages = messageList,
                    RecommendationMessage = "Please note that app monitoring start/end logs use to evaluate the application performance.",
                    ErrorLevel = theNewRule.DefaultErrorLevel
                };
            }

            // No Error message existed return Success
            return new InspectionResult()
            {
                HasErrors = false
            };

        }
    }
}