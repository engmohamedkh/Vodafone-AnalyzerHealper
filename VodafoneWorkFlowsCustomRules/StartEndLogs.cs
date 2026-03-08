using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UiPath.Studio.Activities.Api;
using UiPath.Studio.Activities.Api.Analyzer;
using UiPath.Studio.Activities.Api.Analyzer.Rules;
using UiPath.Studio.Analyzer.Models;


namespace StartEndLogsSpace
{
    public class StartEndLogs : IRegisterAnalyzerConfiguration
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
        string RuleID = "VF-009";
        string RuleName = "StartEndLogs";
        string WFAnalyzerVersion = "WorkflowAnalyzerV4";
        string ExcludedWorkFlows = "";
        string ExcludWorkFlows = "LoaderMain,WorkerMain,Process,Framework/";

        // init the rule to the studio
        public void Initialize(IAnalyzerConfigurationService workflowAnalyzerConfigService)
        {
            // checking the wf version
            if (!workflowAnalyzerConfigService.HasFeature(WFAnalyzerVersion))
                return;

            var newRule = new Rule<IWorkflowModel>(RuleName, RuleID, StartEndLogsRole)
            {
                /// Off and Verbose are not supported.
                ErrorLevel = System.Diagnostics.TraceLevel.Error,
                RecommendationMessage = "Checking IAP loging (Start/End) also check for the End logging in the exception handling."
            };

            workflowAnalyzerConfigService.AddRule<IWorkflowModel>(newRule);
        }

        // Implemetation
        private InspectionResult StartEndLogsRole(IWorkflowModel CurrentWorkflow, Rule theNewRule)
        {
            var messageList = new List<string>();
            bool FirstChild = true;

            // ExceptedWorkFlows always success
            if ((ExcludedWorkFlows.Contains(CurrentWorkflow.DisplayName)) || (CurrentWorkflow.DisplayName.Contains("IAP_")) || (CurrentWorkflow.DisplayName.Contains("ProcessName_")) || (CurrentWorkflow.DisplayName.Contains("Subprocess")) || (CurrentWorkflow.RelativePath.ToLower().Contains("subprocess")))
            {
                // No Error message existed return Success
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
                // no activities yet
                if (CurrentWorkflow.Root.Children.ToList().Count == 0)
                {
                    // No Error message existed return Success
                    return new InspectionResult()
                    {
                        HasErrors = false
                    };
                }

                var childs = CurrentWorkflow.Root.Children.ToList();
                if (childs.Count > 0)
                {
                    // Start Log message
                    if (!((childs[0].ToolboxName.ToLower().Contains("process_monitoring") || childs[0].ToolboxName.ToLower().Contains("app_monitoring")) && childs[0].ToolboxName.ToLower().Contains("start")))
                    {
                        messageList.Add(string.Format("The following workflow: {0} doesn't have start logging activity.", CurrentWorkflow.DisplayName));
                    }
                }
                else
                {
                    messageList.Add(string.Format("The following workflow: {0} is empty.", CurrentWorkflow.DisplayName));
                    // errors existed
                    if (!IsEmpty(messageList))
                    {
                        return new InspectionResult()
                        {
                            HasErrors = true,
                            Messages = messageList,
                            RecommendationMessage = "Please add start/end logs for every workflow created.",
                            ErrorLevel = theNewRule.DefaultErrorLevel
                        };
                    }
                }


                // For any WF type (Sequence or Flowchart)
                if (childs.FirstOrDefault(x => x.ToolboxName.Contains("TryCatch")) != null)
                {
                    var TryCatch = childs.FirstOrDefault(x => x.ToolboxName.Contains("TryCatch"));
                    // End Log message in the exceptions
                    if (TryCatch.Children.Count == 0)
                    {
                        messageList.Add(string.Format("The following workflow: {0} have try catch activity but it's empty.", CurrentWorkflow.DisplayName));
                        // errors existed
                        if (!IsEmpty(messageList))
                        {
                            return new InspectionResult()
                            {
                                HasErrors = true,
                                Messages = messageList,
                                RecommendationMessage = "Please add start/end logs for every workflow created.",
                                ErrorLevel = theNewRule.DefaultErrorLevel
                            };
                        }
                    }
                    else if (TryCatch.Children.Count == 1)
                    {
                        messageList.Add(string.Format("The following workflow: {0} have try catch activity but it doesn't have exception handling.", CurrentWorkflow.DisplayName));
                    }


                    foreach (var child in TryCatch.Children)
                    {


                        if (FirstChild)
                        {
                            // if try does have sequence search in its children
                            if (child.ToolboxName.Contains("Sequence") || child.ToolboxName.Contains("Flowchart"))
                            {
                                var tryChilds = child.Children.ToList();
                                if (tryChilds.Count > 0)
                                {
                                    // search for End log message at the end of the try
                                    if (!((tryChilds[tryChilds.Count - 1].ToolboxName.ToLower().Contains("process_monitoring") || tryChilds[tryChilds.Count - 1].ToolboxName.ToLower().Contains("app_monitoring")) && tryChilds[tryChilds.Count - 1].ToolboxName.ToLower().Contains("end")))
                                    {
                                        //if ((tryChilds.FirstOrDefault(x => (x.ToolboxName.ToLower().Contains("process_monitoring") || x.ToolboxName.ToLower().Contains("app_monitoring")) && x.ToolboxName.ToLower().Contains("end"))) == null)
                                        // Search for End Log message at the end of the root
                                        //  if (!((childs[childs.Count - 1].ToolboxName.ToLower().Contains("process_monitoring") || childs[childs.Count - 1].ToolboxName.ToLower().Contains("app_monitoring")) && childs[childs.Count - 1].ToolboxName.ToLower().Contains("end")))
                                        // {
                                        //   messageList.Add(string.Format("The following workflow: {0} doesn't have end logging activity at end of the workflow or end of the try catch.", CurrentWorkflow.DisplayName));
                                        //}
                                        messageList.Add(string.Format("The following workflow: {0} doesn't have end logging activity at end of the workflow or end of the try catch.", CurrentWorkflow.DisplayName));

                                    }
                                }
                                else
                                {
                                    // Search for End Log message at the end of the root
                                    // if (!((childs[childs.Count - 1].ToolboxName.ToLower().Contains("process_monitoring") || childs[childs.Count - 1].ToolboxName.ToLower().Contains("app_monitoring")) && childs[childs.Count - 1].ToolboxName.ToLower().Contains("end")))
                                    //{
                                    messageList.Add(string.Format(" Empty sequence The following workflow: {0} doesn't have end logging activity at end of the workflow or end of the try catch.", CurrentWorkflow.DisplayName));
                                    //}
                                }

                            }
                            else
                            {

                                messageList.Add(string.Format("The following workflow: {0} doesn't use the agreed template", CurrentWorkflow.DisplayName));

                            }

                            FirstChild = false;
                        }
                        else
                        {
                            if (child.ToolboxName.Contains("Sequence") || child.ToolboxName.Contains("Flowchart"))
                            {
                                var endlog = child.Children.FirstOrDefault(x => ((x.ToolboxName.ToLower().Contains("process_monitoring") || x.ToolboxName.ToLower().Contains("app_monitoring")) && x.ToolboxName.ToLower().Contains("end")));
                                if ((endlog == null))
                                {
                                    // End Log message
                                    messageList.Add(string.Format("The following workflow: {0} doesn't have end logging activity at the exception handling.", CurrentWorkflow.DisplayName));
                                }
                                else
                                {
                                    // End Log message
                                    if (endlog.Arguments.FirstOrDefault(x => x.DisplayName.Equals("strexecutionmessage", StringComparison.OrdinalIgnoreCase) && x.DefinedExpression.ToLower().Contains("exception.message")) == null)
                                    {
                                        messageList.Add(string.Format("The following workflow: {0} doesn't have exception.message and exception.source in the IAP end logging activity at the exception handling.", CurrentWorkflow.DisplayName));
                                    }
                                }
                            }
                            else
                            {
                                if (!((child.ToolboxName.ToLower().Contains("process_monitoring") || child.ToolboxName.ToLower().Contains("app_monitoring")) && child.ToolboxName.ToLower().Contains("end")))
                                {
                                    messageList.Add(string.Format("The following workflow: {0} doesn't have end logging activity at the exception handling.", CurrentWorkflow.DisplayName));
                                }
                                else
                                {
                                    // End Log message
                                    if (child.Arguments.FirstOrDefault(x => x.DisplayName.Equals("strexecutionmessage", StringComparison.OrdinalIgnoreCase) && x.DefinedExpression.ToLower().Contains("exception.message")) == null)
                                    {
                                        messageList.Add(string.Format("The following workflow: {0} doesn't have exception.message in the IAP end logging activity at the exception handling.", CurrentWorkflow.DisplayName));
                                    }
                                }
                            }
                        }

                    }
                }
                else
                {
                    messageList.Add(string.Format("The following workflow: {0} doesn't use the agreed template.", CurrentWorkflow.DisplayName));
                }

            }

            // errors existed
            if (!IsEmpty(messageList))
            {
                return new InspectionResult()
                {
                    HasErrors = true,
                    Messages = messageList,
                    RecommendationMessage = "Please add start/end logs for every workflow created.",
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