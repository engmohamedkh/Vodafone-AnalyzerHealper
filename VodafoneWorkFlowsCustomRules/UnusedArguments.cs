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

namespace UnusedArgumentsSpace
{
    public class UnusedArgumentsRule : IRegisterAnalyzerConfiguration
    {

        // Configs
        string RuleID = "VF-037";
        string RuleName = "UnusedArguments";
        string WFAnalyzerVersion = "WorkflowAnalyzerV4";

        //Rule init
        public void Initialize(IAnalyzerConfigurationService workflowAnalyzerConfigService)
        {
            if (!workflowAnalyzerConfigService.HasFeature(WFAnalyzerVersion))
                return;

            var newRule = new Rule<IWorkflowModel>(RuleName, RuleID, UnusedArguments)
            {
                /// Off and Verbose are not supported.
                ErrorLevel = System.Diagnostics.TraceLevel.Warning,
                RecommendationMessage = "Checking if there is Any Unused Arguments in each workflow"
            };

            workflowAnalyzerConfigService.AddRule<IWorkflowModel>(newRule);
        }

        // Rule implementation
        private InspectionResult UnusedArguments(IWorkflowModel CurrentWorkflow, Rule theNewRule)
        {
            var messageList = new List<string>();
            

            // read the current workflow as a text of lines
            string[] XamlFileLines = File.ReadAllLines(CurrentWorkflow.Project.Directory.ToString() + "\\" + CurrentWorkflow.RelativePath);
            foreach (var arg in CurrentWorkflow.Arguments)
            {
                var count = Array.FindAll(XamlFileLines, s => s.Contains(arg.DisplayName.Trim())).Length;
                if (count == 1)
                {
                    messageList.Add(string.Format("The following workflow: '{0}' has an Unused Argument '{1}'.", CurrentWorkflow.DisplayName, arg.DisplayName));
                }
            }
            // No Error message existed return Success
            if (messageList.Count>0)
            {
                // Error message existed return the error
                return new InspectionResult()
                {
                    HasErrors = true,
                    Messages = messageList,
                    RecommendationMessage = "Remove any Unused Arguments.",
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