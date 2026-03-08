using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UiPath.Studio.Activities.Api;
using UiPath.Studio.Activities.Api.Analyzer;
using UiPath.Studio.Activities.Api.Analyzer.Rules;
using UiPath.Studio.Analyzer.Models;

namespace MultipleAppsSpace
{
    public class MultipleAppsRule : IRegisterAnalyzerConfiguration
    {

        // Configs
        string RuleID = "VF-042";
        string RuleName = "Multiple Apps Sequence";
        string WFAnalyzerVersion = "WorkflowAnalyzerV4";
        string[] TargetActivities = { "BrowserScope", "WindowScope", "ExcelApplicationScope", "ExchangeScope", "SharePoint", "S3","Range","PDF"};
        List<string> TargetApps = new List<string>();
        List<string> DistinctTargetApps = new List<string>();

        //Rule init
        public void Initialize(IAnalyzerConfigurationService workflowAnalyzerConfigService)
        {
            if (!workflowAnalyzerConfigService.HasFeature(WFAnalyzerVersion))
                return;

            var newRule = new Rule<IWorkflowModel>(RuleName, RuleID, MultipleAppsSequence)
            {

                ErrorLevel = System.Diagnostics.TraceLevel.Error,
                RecommendationMessage = "Checking if the workflow is Dealing with more than 1 target application"
            };

            workflowAnalyzerConfigService.AddRule<IWorkflowModel>(newRule);
        }

        private void CountActivities(IActivityModel ActivityToSearchIn)
        {
            if (ActivityToSearchIn.ToolboxName.ToLower().Contains("sequence") || ActivityToSearchIn.ToolboxName.ToLower().Contains("flowchart") || ActivityToSearchIn.ToolboxName.ToLower().Contains("trycatch") || ActivityToSearchIn.ToolboxName.ToLower().Contains("retryscope"))
            {
                foreach (var chi in ActivityToSearchIn.Children.ToList())
                {
                    CountActivities(chi);
                }
            }
            else if (TargetActivities.Any(x => ActivityToSearchIn.ToolboxName.ToString().ToLower().Trim().Contains(x.Trim().ToLower())))
            {
                TargetApps.Add(ActivityToSearchIn.ToolboxName);
            }



        }

        // Rule implementation
        private InspectionResult MultipleAppsSequence(IWorkflowModel CurrentWorkflow, Rule theNewRule)
        {
            var messageList = new List<string>();

            if (CurrentWorkflow.RelativePath.ToLower().Contains("automation"))
            {
                foreach (var child in CurrentWorkflow.Root.Children.ToList())
                {
                    CountActivities(child);
                }

                if (TargetApps.Count > 1)
                {
                    DistinctTargetApps = TargetApps.Distinct().ToList();
                    //exceptional cases
                    if (DistinctTargetApps.All(x => x.ToLower().Contains("excel") || (x.ToLower().Contains("range") && ! x.ToLower().Contains("pdf"))))
                    {
                        return new InspectionResult()
                        {
                            HasErrors = false
                        };
                    }
                    if (DistinctTargetApps.Count > 1)
                    {
                        //more than 1 application is being used
                        messageList.Add(string.Format("The following Workflow: '{0}' Deals with {1} Target Applications. below please consider splitting to 1 sequence for each of the follwoing '{2}'", CurrentWorkflow.DisplayName, TargetApps.Count.ToString(),String.Join("/",DistinctTargetApps)));
                    }
                    else
                    {
                        //more than 1 screen or file is being used on the same application
                        messageList.Add(string.Format("The following Workflow: '{0}' has '{1} {2} activities'. please consider splitting it", CurrentWorkflow.DisplayName, TargetApps.Count.ToString(), DistinctTargetApps[0]));
                    }

                }
            }

            TargetApps.Clear();
            DistinctTargetApps.Clear();
            if (messageList.Count > 0)
            {

                messageList = messageList.Distinct().ToList();
                return new InspectionResult()
                {
                    HasErrors = true,
                    Messages = messageList,
                    RecommendationMessage = "please consider splitting the sequence so that every sequnce deals with only 1 Application or 1 screen",
                    ErrorLevel = theNewRule.DefaultErrorLevel
                };
            }
            return new InspectionResult()
            {
                HasErrors = false
            };
        }
    }
}