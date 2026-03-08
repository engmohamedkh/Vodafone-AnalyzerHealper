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



namespace BEInsideAutomationSpace
{
    public class BEInsideAutomation : IRegisterAnalyzerConfiguration
    {      // Configs
        string RuleID = "VF-041";
        string RuleName = "BEInsideAutomation";
        string WFAnalyzerVersion = "WorkflowAnalyzerV4";
        string IncludedWorkflowPath = "Automation";

        // check list size
        public static bool IsEmpty<T>(List<T> list)
        {
            if (list == null)
            {
                return true;
            }

            return !list.Any();
        }
        // init the rule to the studio
        public void Initialize(IAnalyzerConfigurationService workflowAnalyzerConfigService)
        {
            if (!workflowAnalyzerConfigService.HasFeature(WFAnalyzerVersion))
                return;

            var newRule = new Rule<IWorkflowModel>(RuleName, RuleID, BEInsideAutomationCheck)
            {
                /// Off and Verbose are not supported.
                ErrorLevel = System.Diagnostics.TraceLevel.Warning,
                RecommendationMessage = "Checking BE throw inside the automation Workflows."
            };

            workflowAnalyzerConfigService.AddRule<IWorkflowModel>(newRule);
        }

        // Rule implementation
        private InspectionResult BEInsideAutomationCheck(IWorkflowModel CurrentWorkflow, Rule theNewRule)
        {
            var messageList = new List<string>();
            //if the workflow is in automation folder and the workflow is not empty and only sequences and flowcharts check if it contains pre and post condtion
            if (((CurrentWorkflow.Project.Directory.ToString() + "\\" + CurrentWorkflow.RelativePath).Contains(IncludedWorkflowPath)) && !(CurrentWorkflow.Root == null) && (CurrentWorkflow.Root.Children.ToList().Count > 0) && (CurrentWorkflow.Root.ToolboxName.Contains("Sequence") || CurrentWorkflow.Root.ToolboxName.Contains("Flowchart")))
            {
                var childs = CurrentWorkflow.Root.Children.ToList();
                // For any WF type (Sequence or Flowchart) does it contain try and catch?
                if (childs.FirstOrDefault(x => x.ToolboxName.Contains("TryCatch")) != null)
                {
                    var container = childs.FirstOrDefault(x => x.ToolboxName.Contains("TryCatch")).Children.FirstOrDefault(y => y.ToolboxName.Contains("Sequence") || y.ToolboxName.Contains("Flowchart"));
                    var RetryScope = childs.FirstOrDefault(x => x.ToolboxName.Contains("TryCatch")).Children.FirstOrDefault(y => y.ToolboxName.Contains("RetryScope"));
                    // if no retry scope in try catch children check inside the container for retry scope
                    if (RetryScope == null)
                    {
                        RetryScope = container.Children.FirstOrDefault(y => y.ToolboxName.Contains("RetryScope"));
                    }

                    // Retry scope existed check for pre and post conditions inside it
                    if (RetryScope != null)
                    {//reset the global values
                        string[] XamlFileLines = File.ReadAllLines(CurrentWorkflow.Project.Directory.ToString() + "\\" + CurrentWorkflow.RelativePath);
                        // reverse it
                        Array.Reverse(XamlFileLines);
                        // get the main catch block start and end
                        var CatchStartLine = XamlFileLines.ToList().FindIndex(x => x.Contains("<TryCatch.Try>"));
                        var CatchEndLine = XamlFileLines.ToList().FindIndex(x => x.Contains("</TryCatch.Try>"));
                        // convert this block to string
                        string XAMLfile = "";
                        if (CatchStartLine > 0 && CatchEndLine > 0)
                            XAMLfile = string.Join(",", XamlFileLines.ToList().GetRange(CatchEndLine, CatchStartLine - CatchEndLine));
                        if (XAMLfile.ToLower().Contains("new businessruleexception"))
                            messageList.Add(string.Format("The following Automation Workflow: {0} has Business Exception inside it.", CurrentWorkflow.DisplayName));


                    }
                }
            }
            if (!IsEmpty(messageList))
            {
                // Error message existed return the error
                return new InspectionResult()
                {
                    HasErrors = true,
                    Messages = messageList,
                    RecommendationMessage = "Don't Throw BE inside Automation Workflows use A Flag and throw it in main Logic instead.",
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
