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


namespace PreAndPostConditionsSpace
{
    public class PreAndPostConditions : IRegisterAnalyzerConfiguration
    {      // Configs
        string RuleID = "VF-039";
        string RuleName = "PreAndPostConditions";
        string WFAnalyzerVersion = "WorkflowAnalyzerV4";
        string IncludedWorkflowPath = "Automation";
        string ReadWorkflowPath = "Read";
        List<string> DynamicWaitList = new List<string>() { "UiElementExists", "FindChildren", "WaitUiElementAppear", "FindRelative", "WaitImageAppear", "FindImageMatches", "ImageFound", "OCRTextExists", "TextExists", "ReadStatusbar", "GetValue", "PathExists", "GetFullText", "GetVisibleText", "ExtractData", "GetOCRText", "OnImageAppear", "OnImageVanish", "WaitImageVanish", "OnUiElementAppear", "OnUiElementVanish", "AnchorBase", "AnchorContextAware", "GetAncestor", "WaitUiElementVanish", "WaitAttribute", "GetAttribute" };
        int int_DynamicWaitCount = 0;
        int int_IfCount = 0;
        bool PreCondition = false;
        bool PostCondition = false;
        bool AlreadyInsideThePreorPostSeqbool=false;
        List<string> PreConditionSeqDisplayName = new List<string>() { "pre condition", "precondition" };
        List<string> PostConditionSeqDisplayName = new List<string>() { "post condition", "postcondition" };
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

            var newRule = new Rule<IWorkflowModel>(RuleName, RuleID, PreAndPostConditionsCheck)
            {
                /// Off and Verbose are not supported.
                ErrorLevel = System.Diagnostics.TraceLevel.Warning,
                RecommendationMessage = "Checking Pre and Post Conditions in the automation Workflows and pre condition only in read workflows ."
            };

            workflowAnalyzerConfigService.AddRule<IWorkflowModel>(newRule);
        }
        //Recursive function to get the children of any seq
        private void LookForPreAndPost(IActivityModel ActivityToSearchIn)
        {

            foreach (var child in ActivityToSearchIn.Children.ToList())
            {//if the child is sequence then look inside it for the pre and post conditions display name
                if (child.ToolboxName.Contains("Sequence") && !((child.Parent.DisplayName.ToLower().Contains(PreConditionSeqDisplayName[0]) || child.Parent.DisplayName.ToLower().Contains(PreConditionSeqDisplayName[1])) || (child.Parent.DisplayName.ToLower().Contains(PostConditionSeqDisplayName[0]) || child.Parent.DisplayName.ToLower().Contains(PostConditionSeqDisplayName[1]))))
                {
                    if (child.DisplayName.ToLower().Contains(PreConditionSeqDisplayName[0]) || child.DisplayName.ToLower().Contains(PreConditionSeqDisplayName[1]))
                    {
                        PreCondition = true;
                    }
                    if (child.DisplayName.ToLower().Contains(PostConditionSeqDisplayName[0]) || child.DisplayName.ToLower().Contains(PostConditionSeqDisplayName[1]))
                    {
                        PostCondition = true;
                    }
                    LookForPreAndPost(child);
                }
                //if this is children for pre or post condition
                else if (((child.Parent.DisplayName.ToLower().Contains(PreConditionSeqDisplayName[0]) || child.Parent.DisplayName.ToLower().Contains(PreConditionSeqDisplayName[1])) || (child.Parent.DisplayName.ToLower().Contains(PostConditionSeqDisplayName[0]) || child.Parent.DisplayName.ToLower().Contains(PostConditionSeqDisplayName[1]))) || AlreadyInsideThePreorPostSeqbool)
                {//count the total number of appearance of the waits and ifs
                    AlreadyInsideThePreorPostSeqbool=true;
                    //if there's a sequence inside 
                    if(child.ToolboxName.Contains("Sequence") || child.ToolboxName.Contains("DoWhile") || child.ToolboxName.Contains("If") || child.ToolboxName.Contains("Parallel"))
                    { 
                        LookForPreAndPost(child);
                    }
                    if (DynamicWaitList.Contains(child.ToolboxName))
                    {
                        int_DynamicWaitCount++;
                    }
                    if (child.ToolboxName.Contains("If"))
                    {
                        int_IfCount++;
                        //if precondition is met reset the flag to only enter again when post condition comes
                        AlreadyInsideThePreorPostSeqbool = false;

                    }


                    
                }
                //childDisplayName=child.DisplayName;
            }
        }
        // Rule implementation
        private InspectionResult PreAndPostConditionsCheck(IWorkflowModel CurrentWorkflow, Rule theNewRule)
        {
            var messageList = new List<string>();
            //if the workflow is in automation folder and the workflow is not empty and only sequences and flowcharts check if it contains pre and post condtion
            if ((CurrentWorkflow.RelativePath.Contains(IncludedWorkflowPath)) && !(CurrentWorkflow.Root == null) && (CurrentWorkflow.Root.Children.ToList().Count > 0) && (CurrentWorkflow.Root.ToolboxName.Contains("Sequence") || CurrentWorkflow.Root.ToolboxName.Contains("Flowchart")))
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
                        int_DynamicWaitCount = 0;
                        int_IfCount = 0;
                        PreCondition = false;
                        PostCondition = false;
                        //Look for the pre and post condition inside the retry scope
                        LookForPreAndPost(RetryScope);
                        //if this workflow inside read then sheck for pre condition only else check for pre and post
                        if (CurrentWorkflow.RelativePath.Contains(ReadWorkflowPath))
                        {
                            if (!(int_IfCount >= 1 && int_DynamicWaitCount >= 1 && PreCondition))
                            {
                               
                                messageList.Add(string.Format("The following Workflow: {0} does not have pre condition.", CurrentWorkflow.DisplayName));
                            }
                        }
                        //if the counts of if and dynamic waits is bigger than 1 then the flow has pre and post conditions else it does not have one
                        else
                        {
                            if (!(int_IfCount >= 2 && int_DynamicWaitCount >= 2 && PreCondition && PostCondition))
                            {
                               
                                messageList.Add(string.Format("The following Workflow: {0} does not have pre or post condition.", CurrentWorkflow.DisplayName));
                            }
                        }

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
                    RecommendationMessage = "Use Pre and Post Conditions in Automation Workflows.",
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
