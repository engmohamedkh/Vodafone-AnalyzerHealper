using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Text;
using System.Threading.Tasks;
using UiPath.Studio.Activities.Api;
using UiPath.Studio.Activities.Api.Analyzer;
using UiPath.Studio.Activities.Api.Analyzer.Rules;
using UiPath.Studio.Analyzer.Models;


namespace AddLogFieldsSpace
{
    public class CETTimeZone : IRegisterAnalyzerConfiguration
    {

        // Configs
        string RuleID = "VF-053";
        string RuleName = "CETTimeZone";
        string WFAnalyzerVersion = "WorkflowAnalyzerV4";
        int LogActivitiesCounter = 0;
        int FieldsCounter = 0;
        bool StartCetTimeExists = false;
        bool EndCetTimeExists = false;
        List<string> TempList = new List<string>();

        // init the rule to the studio
        public void Initialize(IAnalyzerConfigurationService workflowAnalyzerConfigService)
        {
            // checking the wf version
            if (!workflowAnalyzerConfigService.HasFeature(WFAnalyzerVersion))
                return;
            var newRule = new Rule<IWorkflowModel>(RuleName, RuleID, CETTimeZoneRole)
            {
                /// Off and Verbose are not supported.
                ErrorLevel = System.Diagnostics.TraceLevel.Error,
                RecommendationMessage = "Checking CET start time activity at the beginng of the settransactionstatus."


            };

            workflowAnalyzerConfigService.AddRule<IWorkflowModel>(newRule);
        }

        //Recursive function
        private void CheckCETTimeZone(IActivityModel ActivityToSearchIn, IWorkflowModel currWorkflow)
        {
            if (ActivityToSearchIn.Children.Count > 0)
            {
                foreach (var chi in ActivityToSearchIn.Children.ToList())
                {
                    CheckCETTimeZone(chi, currWorkflow);
                }
            }

        }


        // Implemetation
        private InspectionResult CETTimeZoneRole(IWorkflowModel CurrentWorkflow, Rule theNewRule)
        {

            List<string> messageList = new List<string>();


            if (CurrentWorkflow.RelativePath.ToLower().Split('_').Last().ToLower().Equals("settransactionstatus.xaml"))
            {
                foreach (var child in CurrentWorkflow.Root.Children.ToList())
                {
                    CheckCETTimeZone(child, CurrentWorkflow);



                    if (child.DisplayName.Contains("Try catch"))
                    {
                        foreach (var ChildinTryCatch in child.Children)
                        {
                            if (ChildinTryCatch.DisplayName.Contains("Work Sequence"))
                            {
                                foreach (var ChildinSeq in ChildinTryCatch.Children)
                                {

                                    if (ChildinSeq.DisplayName.Contains("Set transaction Flowchart"))
                                    {
                                        foreach (var ChildinTransactionFlowchart in ChildinSeq.Children)
                                        {

                                            if (ChildinTransactionFlowchart.DisplayName.Contains("Set Start time"))
                                            {
                                                StartCetTimeExists = true;
                                                foreach (var argument in ChildinTransactionFlowchart.Arguments)
                                                {
                                                    if (argument.DisplayName == "Value")
                                                    {
                                                        if (!argument.DefinedExpression.Contains("TimeZoneInfo.ConvertTime(in_dtmTransactionStartTime,TimeZoneInfo.FindSystemTimeZoneById"))
                                                        {
                                                            messageList.Add(string.Format("The following workflow: {0} has '{1}' Activity that has empty/wrong mandatory argument '{2}'.", CurrentWorkflow.DisplayName, ChildinTransactionFlowchart.DisplayName, argument.DisplayName));

                                                        }
                                                    }
                                                }
                                            }

                                            else if (ChildinTransactionFlowchart.DisplayName.Contains("Set end time"))
                                            {
                                                EndCetTimeExists = true;
                                                foreach (var argument in ChildinTransactionFlowchart.Arguments)
                                                {
                                                    if (argument.DisplayName == "Value")
                                                    {
                                                        if (!argument.DefinedExpression.Contains("TimeZoneInfo.ConvertTime(System.DateTime.Now , TimeZoneInfo.FindSystemTimeZoneById"))
                                                        {
                                                            messageList.Add(string.Format("The following workflow: {0} has '{1}' Activity that has empty/wrong mandatory argument '{2}'.", CurrentWorkflow.DisplayName, ChildinTransactionFlowchart.DisplayName, argument.DisplayName));
                                                        }
                                                    }
                                                }
                                            }

                                        }
                                    }
                                }
                            }
                        }
                    }

                }
                if(StartCetTimeExists == false)
                {
                    messageList.Add(string.Format("The following workflow: Set transaction status don't contain the Set Start time to CET activity"));
                }
                else if (EndCetTimeExists == false)
                {
                    messageList.Add(string.Format("The following workflow: Set transaction status don't contain the Set End time to CET activity"));
                }


            }
            TempList.Clear();
            LogActivitiesCounter = 0;
            FieldsCounter = 0;
            // errors existed
            if (messageList.Count > 0)
            {
                return new InspectionResult()
                {
                    HasErrors = true,
                    Messages = messageList,
                    RecommendationMessage = "Please add the mandatory add log fields activity with the mandatory arguments",
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