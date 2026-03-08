using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UiPath.Studio.Activities.Api;
using UiPath.Studio.Activities.Api.Analyzer;
using UiPath.Studio.Activities.Api.Analyzer.Rules;
using UiPath.Studio.Analyzer.Models;

namespace WQDuplicatesSpace
{
    public class WQDuplicates : IRegisterAnalyzerConfiguration
    {

        // Configs
        string RuleID = "VF-050";
        string RuleName = "WQ Duplication Check";
        string WFAnalyzerVersion = "WorkflowAnalyzerV4";
        List<string> TempList = new List<string>();
        bool DuplicatecheckFound = false;
        // init the rule to the studio
        public void Initialize(IAnalyzerConfigurationService workflowAnalyzerConfigService)
        {
            // checking the wf version
            if (!workflowAnalyzerConfigService.HasFeature(WFAnalyzerVersion))
                return;
            var newRule = new Rule<IWorkflowModel>(RuleName, RuleID, WQDuplicatesRule)
            {

                /// Off and Verbose are not supported.
                ErrorLevel = System.Diagnostics.TraceLevel.Warning,
                RecommendationMessage = "Checking that WQ Duplication Check is used correctly"
            };

            workflowAnalyzerConfigService.AddRule<IWorkflowModel>(newRule);
        }
        //Recursive Function
        private void CheckInputValidation(IActivityModel ActivityToSearchIn)
        {
            if (ActivityToSearchIn.Children.Count > 0)
            {
                foreach (var chi in ActivityToSearchIn.Children.ToList())
                {
                    DuplicatecheckFound = false;
                    CheckInputValidation(chi);
                    if (DuplicatecheckFound)
                    {
                        break;
                    }
                }
            }
            else
            {
                if (ActivityToSearchIn.ToolboxName.ToString().ToLower().Trim().Contains("invokeworkflowfile"))
                {
                    //Needed File invoke is found
                    DuplicatecheckFound = ActivityToSearchIn.Arguments.Any(arg => arg.DisplayName.ToLower().Equals("workflowfilename") && arg.DefinedExpression.ToLower().Contains(("Logic\\IAP_CheckReferenceinQueueItems.xaml").ToLower()));
                    if (DuplicatecheckFound)
                    {
                        foreach (var arg in ActivityToSearchIn.Arguments)
                        {
                            //if ((string.IsNullOrEmpty(arg.DefinedExpression) || arg.DefinedExpression.Equals("{}")) && (!(arg.DisplayName.Equals("WorkflowFileName") || arg.DisplayName.Equals("ContinueOnError") || arg.DisplayName.Equals("Timeout") || arg.DisplayName.Equals("Log Entry") || arg.DisplayName.Equals("Log Exit") || arg.DisplayName.Equals("ArgumentsVariable") || arg.DisplayName.Equals("LogLevel"))))
                            if ((string.IsNullOrEmpty(arg.DefinedExpression) || arg.DefinedExpression.Equals("\"\"")) && !(arg.DisplayName.Replace(" ","").ToLower().Equals("workflowfilename") || arg.DisplayName.Replace(" ", "").ToLower().Equals("continueonerror") || arg.DisplayName.Replace(" ", "").ToLower().Equals("timeout") || arg.DisplayName.Replace(" ", "").ToLower().Equals("logentry") || arg.DisplayName.Replace(" ", "").ToLower().Equals("logexit") || arg.DisplayName.Replace(" ", "").ToLower().Equals("argumentsvariable") || arg.DisplayName.Replace(" ", "").ToLower().Equals("loglevel")))

                            {
                                //if the arguments are not empty or {}
                                TempList.Add(string.Format("The WQ Duplication invoke '{0}' has empty {1} argument: {2}.", ActivityToSearchIn.DisplayName, arg.Direction, arg.DisplayName));
                            }
                        }
                    }
                }
            }
        }
        // Rule implementation
        private InspectionResult WQDuplicatesRule(IWorkflowModel CurrentWorkflow, Rule theNewRule)
        {
            var messageList = new List<string>();
            if (CurrentWorkflow.RelativePath.ToLower().Split('_').Last().Equals("uploaditems.xaml"))
            {
                foreach (var child in CurrentWorkflow.Root.Children.ToList())
                {
                    DuplicatecheckFound = false;
                    CheckInputValidation(child);
                    if (DuplicatecheckFound)
                    {
                        break;
                    }
                }
                if (!DuplicatecheckFound)
                {
                    messageList.Add("The WQ Duplication Check Workflow is not used in the process.");
                }

                if (TempList.Count > 0)
                {
                    messageList.AddRange(TempList);
                }
                TempList.Clear();



            }
            //check if one of the needed activities
            if (messageList.Count > 0)
            {
                messageList = messageList.Distinct().ToList();
                return new InspectionResult()
                {
                    HasErrors = true,
                    Messages = messageList,
                    RecommendationMessage = "Please Use WQ Duplication Check file to validate Queue References",
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
