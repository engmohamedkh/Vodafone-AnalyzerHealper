using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UiPath.Studio.Activities.Api;
using UiPath.Studio.Activities.Api.Analyzer;
using UiPath.Studio.Activities.Api.Analyzer.Rules;
using UiPath.Studio.Analyzer.Models;

namespace RetryScopeSpace
{
    public class RetryScope : IRegisterAnalyzerConfiguration
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
        string RuleID = "VF-022";
        string RuleName = "RetryScope";
        string WFAnalyzerVersion = "WorkflowAnalyzerV4";
        string ExcludedWorkFlows = "Main,Process";


        // init the rule to the studio
        public void Initialize(IAnalyzerConfigurationService workflowAnalyzerConfigService)
        {
            // checking the wf version
            if (!workflowAnalyzerConfigService.HasFeature(WFAnalyzerVersion))
                return;

            var newRule = new Rule<IWorkflowModel>(RuleName, RuleID, RetryScopeRule)
            {
                /// Off and Verbose are not supported.
                ErrorLevel = System.Diagnostics.TraceLevel.Warning,
                RecommendationMessage = "Checking the retry scope and its condition."
            };

            workflowAnalyzerConfigService.AddRule<IWorkflowModel>(newRule);
        }

        // Rule implementation
        private InspectionResult RetryScopeRule(IWorkflowModel CurrentWorkflow, Rule theNewRule)
        {
            try
            {
                var messageList = new List<string>();

                // Exclude Main & Process
                if (ExcludedWorkFlows.Contains(CurrentWorkflow.DisplayName))
                {
                    // No Error message existed return Success
                    return new InspectionResult()
                    {
                        HasErrors = false
                    };
                }
                if (!CurrentWorkflow.RelativePath.Contains("Automation"))
                {
                    return new InspectionResult()
                    {
                        HasErrors = false
                    };
                }
                // no sequence or workflow added
                if (CurrentWorkflow.Root == null)
                {
                    // No Error message existed return Success
                    messageList.Add(string.Format("The following workflow: {0} is empty.", CurrentWorkflow.DisplayName));
                }
                // For any WF type (Sequence or Flowchart)
                else if (CurrentWorkflow.Root.ToolboxName.Contains("Sequence") || CurrentWorkflow.Root.ToolboxName.Contains("Flowchart"))
                {
                    var childs = CurrentWorkflow.Root.Children.ToList();

                    // For any WF type (Sequence or Flowchart)
                    if (childs.FirstOrDefault(x => x.ToolboxName.Contains("TryCatch")) != null)
                    {
                        var tryCatch = childs.FirstOrDefault(x => x.ToolboxName.Contains("TryCatch"));
                        var container = tryCatch.Children.FirstOrDefault(y => y.ToolboxName.Contains("Sequence") || y.ToolboxName.Contains("Flowchart"));
                        var RetryScope = tryCatch.Children.FirstOrDefault(y => y.ToolboxName.Contains("RetryScope"));

                        if (RetryScope == null && container != null)
                        {
                            RetryScope = container.Children.FirstOrDefault(y => y.ToolboxName.Contains("RetryScope"));
                        }

                        // Retry scope existed or not
                        if (RetryScope != null)
                        {
                            var child = RetryScope.Children.ToList();

                            if (child.Count > 0)
                            {
                                // check for the condition
                                if (child[child.Count - 1].Arguments.FirstOrDefault(x => x.Direction.Equals(ArgumentDirection.Out) && x.Type.Contains("Boolean")) == null)
                                {
                                    messageList.Add(string.Format("The following workflow: {0} has retry scope activity but doesn't have condition activity.", CurrentWorkflow.DisplayName));
                                }
                            }
                            else // No activities existed
                            {
                                messageList.Add(string.Format("The following workflow: {0} has retry scope activity but doesn't have condition activity.", CurrentWorkflow.DisplayName));
                            }
                        }
                        else
                        {
                            messageList.Add(string.Format("The following workflow: {0} doesn't have retry scope activity.", CurrentWorkflow.DisplayName));
                        }

                        // Check if TryCatch contains another TryCatch in the Catch block
                        var catchBlock = tryCatch.Children.FirstOrDefault(y => y.ToolboxName.Contains("Catch"));
                        if (catchBlock != null)
                        {
                            var innerTryCatch = catchBlock.Children.FirstOrDefault(y => y.ToolboxName.Contains("TryCatch"));
                            if (innerTryCatch != null)
                            {
                                messageList.Add(string.Format("The following workflow: {0} has a TryCatch activity but the Catch block does not contain another TryCatch activity.", CurrentWorkflow.DisplayName));
                            }
                        }
                    }
                    else
                    {
                        messageList.Add(string.Format("The following workflow: {0} doesn't have try catch activity.", CurrentWorkflow.DisplayName));
                    }
                };

                // errors existed
                if (!IsEmpty(messageList))
                {
                    return new InspectionResult()
                    {
                        HasErrors = true,
                        Messages = messageList,
                        RecommendationMessage = "Please add retry scope activity and its condition.",
                        ErrorLevel = theNewRule.DefaultErrorLevel
                    };
                }

                // No Error message existed return Success
                return new InspectionResult()
                {
                    HasErrors = false
                };
            }
            catch (Exception ex)
            {
                // No Error message existed return Success
                return new InspectionResult()
                {
                    HasErrors = false
                };
            }
        }
    }
}
