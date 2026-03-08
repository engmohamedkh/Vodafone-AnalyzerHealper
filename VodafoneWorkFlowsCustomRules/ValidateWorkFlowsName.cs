using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UiPath.Studio.Activities.Api;
using UiPath.Studio.Activities.Api.Analyzer;
using UiPath.Studio.Activities.Api.Analyzer.Rules;
using UiPath.Studio.Analyzer.Models;

namespace ValidateWorkFlowsName
{
    public class ValidateWorkFlowName : IRegisterAnalyzerConfiguration
    {
        public static bool IsEmpty<T>(List<T> list)
        {
            if (list == null)
            {
                return true;
            }

            return !list.Any();
        }

        // Configs
        string RuleID = "VF-021";
        string RuleName = "ValidateWorkFlowName";
        string WFAnalyzerVersion = "WorkflowAnalyzerV4";
        string ExcludedWorkFlows = "_loader,_worker,_LoaderWorker";
      
        string ComponentNameVariableName = "strComponentName,strWorkflowName,strStateName";
        string RecommendationName = "";
        char[] SpecialChars = { ',', '\'', '.', '"', '?', '>', '<', ';', ':', '/', '\\', ';', '|', ']', '[', '}', '{', '=', '+', '-', '(', ')', '*', '&', '^', '%', '$', '#', '@', '!', '`', '~', ' ' };
        // init the rule to the studio
        public void Initialize(IAnalyzerConfigurationService workflowAnalyzerConfigService)
        {
            // checking the wf version
            if (!workflowAnalyzerConfigService.HasFeature(WFAnalyzerVersion))
                return;

            var newRule = new Rule<IWorkflowModel>(RuleName, RuleID, ValidateWorkFlowNameRule)
            {
                /// Off and Verbose are not supported.
                ErrorLevel = System.Diagnostics.TraceLevel.Warning,
                RecommendationMessage = "Checking the workflow name if contains the special chars."
            };

            workflowAnalyzerConfigService.AddRule<IWorkflowModel>(newRule);
        }

        // Implemetation
        private InspectionResult ValidateWorkFlowNameRule(IWorkflowModel CurrentWorkflow, Rule theNewRule)
        {
            var messageList = new List<string>();



            // ExceptedWorkFlows always success
            if (ExcludedWorkFlows.Contains(CurrentWorkflow.DisplayName) || CurrentWorkflow.DisplayName.ToLower().Contains("_loader") || CurrentWorkflow.DisplayName.ToLower().Contains("_worker") || CurrentWorkflow.DisplayName.ToLower().Contains("_loaderworker"))
            {
                // No Error message existed return Success
                return new InspectionResult()
                {
                    HasErrors = false
                };
            }

            List<char> UsedChars = new List<char> { };

            // check the used chars
            foreach (char cha in SpecialChars)
            {
                if (CurrentWorkflow.DisplayName.Contains(cha))
                {
                    UsedChars.Add(cha);
                };
            }

            // if any special char existed
            if (UsedChars.Count != 0)
            {
                // add error message
                messageList.Add(string.Format("The following workflow: {0} has '{1}' in its naming. Please replace them by '_'.", CurrentWorkflow.DisplayName, string.Join("/", UsedChars)));

                string newName = CurrentWorkflow.DisplayName;
                // replace any special char to _
                foreach (char cha in UsedChars)
                {
                    newName = newName.Replace(cha, '_');
                }
                RecommendationName = newName;
            }
            else
            {
                RecommendationName = CurrentWorkflow.DisplayName;
            }

            /*Component variable value*/
            // no sequence or workflow added
            if (CurrentWorkflow.Root == null)
            {
                // No Error message existed return Success
            }
            // For any WF type (Sequence or Flowchart)
            else
            {
                int Counter = 0;
                foreach (var activ in ComponentNameVariableName.Split(',').ToList())
                {

                  
                    /*Component variable value*/
                    var componentNameVariable = CurrentWorkflow.Root.Variables.FirstOrDefault(x => x.DisplayName.ToLower().Equals(activ.ToLower()));

                    if (componentNameVariable != null)
                    {

                        if (String.IsNullOrEmpty(componentNameVariable.DefaultValue))
                        {
                            messageList.Add(string.Format("The following workflow: {0} has '{1}' but its default value is empty.", CurrentWorkflow.DisplayName, activ));
                        }
                        else if (!componentNameVariable.DefaultValue.Replace("\"", "").ToLower().Equals(CurrentWorkflow.DisplayName.ToLower()))
                        {
                            messageList.Add(string.Format("The following workflow: {0} has '{1}' but its default value doesn't match the workflow name.", CurrentWorkflow.DisplayName, activ));
                        }
                       
                    }
                    else
                    {
                        Counter++;
                      

                    }
                }
                if (Counter >=3)
                {
                    messageList.Add(string.Format("The following workflow: {0} doesn't have one of these '{1}' variables.", CurrentWorkflow.DisplayName, ComponentNameVariableName.Replace(",", "/")));

                }
            }
            // errors existed
            if (!IsEmpty(messageList))
            {
                return new InspectionResult()
                {
                    HasErrors = true,
                    Messages = messageList,
                    RecommendationMessage = "Use the following naming instead: " + RecommendationName + ". and please make sure that the workflow name match " + ComponentNameVariableName.Replace(",","/") + " variable value.",
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
