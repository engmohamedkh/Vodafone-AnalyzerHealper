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

namespace HardcodedArgumentsSpace
{
    public class HardcodedArguments : IRegisterAnalyzerConfiguration
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
        string RuleID = "VF-028";
        string RuleName = "HardcodedArguments";
        string WFAnalyzerVersion = "WorkflowAnalyzerV4";
        string ExcludedWorkFlows = "";
        string ComponentNameVariableName = "strComponentName,strWorkflowName,strStateName";


        // init the rule to the studio
        public void Initialize(IAnalyzerConfigurationService workflowAnalyzerConfigService)
        {
            // checking the wf version
            if (!workflowAnalyzerConfigService.HasFeature(WFAnalyzerVersion))
                return;

            var newRule = new Rule<IWorkflowModel>(RuleName, RuleID, HardcodedArgumentsRule)
            {
                /// Off and Verbose are not supported.
                ErrorLevel = System.Diagnostics.TraceLevel.Warning,
                RecommendationMessage = "Checking workflow arguments value if it's hardcoded or not."
            };

            workflowAnalyzerConfigService.AddRule<IWorkflowModel>(newRule);
        }

        // Rule implementation
        private InspectionResult HardcodedArgumentsRule(IWorkflowModel CurrentWorkflow, Rule theNewRule)
        {

            var messageList = new List<string>();
            string Arg = "<Literal";

            // ExceptedWorkFlows always success
            if (ExcludedWorkFlows.Contains(CurrentWorkflow.DisplayName))
            {
                // No Error message existed return Success
                return new InspectionResult()
                {
                    HasErrors = false
                };
            }

            // check for every argument existed on the workflow
            foreach (var Argument in CurrentWorkflow.Arguments)
            {
                if (ComponentNameVariableName.Contains(Argument.DisplayName))
                    continue;

                if (!string.IsNullOrEmpty(Argument.DefaultValue))
                {
                    messageList.Add(string.Format("The following workflow: {0} has argument with default value. argument name: {1}.", CurrentWorkflow.DisplayName, Argument.DisplayName));
                }
            }

            string[] WorkflowLines = File.ReadAllLines(CurrentWorkflow.Project.Directory.ToString() + "\\" + CurrentWorkflow.RelativePath);
            List <string> AllLines = WorkflowLines.ToList();

            List<int> ArrStart=new List<int>();
            List<int> ArrEnd= new List<int>();
            for (int i = 0; i <= WorkflowLines.Length-1; i++)
            {
                if (WorkflowLines[i].Contains("<ui:InvokeWorkflowFile.Arguments>"))
                {
                    ArrStart.Add(i);
                }
                if (WorkflowLines[i].Contains("</ui:InvokeWorkflowFile.Arguments>"))
                {
                    ArrEnd.Add(i);
                }
            }

            List<string> SpecificRange=new List<string>();

            for (int i = 0; i <= ArrStart.Count - 1; i++)
            {
                SpecificRange = AllLines.GetRange(ArrStart[i], ArrEnd[i] - ArrStart[i]);
                foreach (string item in SpecificRange)
                {
                    if (item.Contains(Arg))
                    {
                        int CharIndex = item.IndexOf(Arg);
                        messageList.Add(string.Format("Invoked Workflows {0} Contains invoked sequances has a static arguments.", CurrentWorkflow.DisplayName));
                    }

                }
                SpecificRange = new List<string>();
            }
            // errors existed
            if (!IsEmpty(messageList))
            {
                return new InspectionResult()
                {
                    HasErrors = true,
                    Messages = messageList,
                    RecommendationMessage = "Please remove any default value existed in the workflow or its invokes.",
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
