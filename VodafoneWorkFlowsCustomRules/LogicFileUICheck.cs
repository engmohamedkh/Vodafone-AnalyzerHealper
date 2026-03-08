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

namespace LogicFileUICheckSpace
{

    public class LogicFileUICheck : IRegisterAnalyzerConfiguration
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
        string RuleID = "VF-032";
        string RuleName = "LogicFileUICheck";
        string WFAnalyzerVersion = "WorkflowAnalyzerV4";
        // Excluded WF from the rule
        string DefaultDisplayedNAames = "Click,GetValue,TypeInto,Activate,UiElementExists,BrowserScope,WindowScope";
        string DefaultNames = "Click,Get Text,Type Into,Activate,Element Exists,Attach Browser,Attach Window";


        //Rule init
        public void Initialize(IAnalyzerConfigurationService workflowAnalyzerConfigService)
        {
            if (!workflowAnalyzerConfigService.HasFeature(WFAnalyzerVersion))
                return;

            var newRule = new Rule<IWorkflowModel>(RuleName, RuleID, LogicFileUICheckImplemntation)
            {
                /// Off and Verbose are not supported.
                ErrorLevel = System.Diagnostics.TraceLevel.Error,
                RecommendationMessage = "Checking the logic/subprocess files has any UI activitiy or not"
            };

            workflowAnalyzerConfigService.AddRule<IWorkflowModel>(newRule);
        }


        // Rule implementation
        private InspectionResult LogicFileUICheckImplemntation(IWorkflowModel CurrentWorkflow, Rule theNewRule)
        {
            var messageList = new List<string>();
            try
            {
              

                // no sequence or workflow added

                if (CurrentWorkflow.Root == null)
                {
                    // No Error message existed return Success
                    messageList.Add(string.Format("The following workflow: {0} is empty .", CurrentWorkflow.DisplayName));
                }

                else if ((CurrentWorkflow.Project.Directory.ToString() + "\\" + CurrentWorkflow.RelativePath).ToLower().Contains("logic") || (CurrentWorkflow.Project.Directory.ToString() + "\\" + CurrentWorkflow.RelativePath).ToLower().Contains("subprocess"))
                {

                    //Read current work flow as a text file

                    string[] XamlFileLines = File.ReadAllLines(CurrentWorkflow.Project.Directory.ToString() + "\\" + CurrentWorkflow.RelativePath);
                 
                    string activityLine = "";
                    int counter = 0;
                    foreach (var UiElement in DefaultDisplayedNAames.Split(',').ToList())
                    {

                       

                        activityLine = XamlFileLines.FirstOrDefault(b => b != null && b.Contains("<ui:" + UiElement));
                        
                        if (!(string.IsNullOrEmpty(activityLine)))
                        {
                           
                            if (activityLine.Contains("DisplayName="))
                            {
                                var SplitedLine = activityLine.Split('"').ToList();
                                var DisplayedNameIndex = SplitedLine.FindIndex(x => x.Contains("DisplayName=")) + 1;
                              
                                messageList.Add(string.Format("The following logic workflow: {0} has a UI activity {1}.", CurrentWorkflow.DisplayName, SplitedLine[DisplayedNameIndex]));


                            }
                            else
                            {
                                messageList.Add(string.Format("The following logic workflow: {0} has a UI activity {1}.", CurrentWorkflow.DisplayName, DefaultNames.Split(',').ToList()[counter]));
                            }
                        }

                        counter++;
                    }

                }

                // No Error message existed return Success
                if (!IsEmpty(messageList))
                {
                    // Error message existed return the error
                    return new InspectionResult()
                    {
                        HasErrors = true,
                        Messages = messageList,
                        RecommendationMessage = "Remove UI Activities" + DefaultDisplayedNAames,
                        ErrorLevel = theNewRule.DefaultErrorLevel
                    };
                }

                return new InspectionResult()
                {
                    HasErrors = false
                };
            }
            catch (Exception ex)
            {
                if (!IsEmpty(messageList))
                {
                    // Error message existed return the error
                    return new InspectionResult()
                    {
                        HasErrors = true,
                        Messages = messageList,
                        RecommendationMessage = "Remove UI Activities" + DefaultDisplayedNAames,
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
}
