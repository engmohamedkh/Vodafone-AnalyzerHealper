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

namespace TryCatchCheck
{
    public class TryCatchRule : IRegisterAnalyzerConfiguration
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
        string RuleID = "VF-012";
        string RuleName = "TryCatch";
        string WFAnalyzerVersion = "WorkflowAnalyzerV4";
        // Excluded WF from the rule
        string ExcludedWorkFlows = "";

        //Rule init
        public void Initialize(IAnalyzerConfigurationService workflowAnalyzerConfigService)
        {
            if (!workflowAnalyzerConfigService.HasFeature(WFAnalyzerVersion))
                return;

            var newRule = new Rule<IWorkflowModel>(RuleName, RuleID, TryCatchCheck)
            {
                /// Off and Verbose are not supported.
                ErrorLevel = System.Diagnostics.TraceLevel.Error,
                RecommendationMessage = "Checking the try catch activity and the exception logging (exception.source/exception.message). Also check for any activity used outside trycach scope."
            };

            workflowAnalyzerConfigService.AddRule<IWorkflowModel>(newRule);
        }

        // Rule implementation
        private InspectionResult TryCatchCheck(IWorkflowModel CurrentWorkflow, Rule theNewRule)
        {
            var messageList = new List<string>();
            bool TryCatchExisted = false;
            bool OutsideTryCatch = false;

            // read the current workflow as a text of lines
            string[] XamlFileLines = File.ReadAllLines(CurrentWorkflow.Project.Directory.ToString() + "\\" + CurrentWorkflow.RelativePath);
            // reverse it
            Array.Reverse(XamlFileLines);
            // get the main catch block start and end
            var CatchStartLine = XamlFileLines.ToList().FindIndex(x => x.Contains("<TryCatch.Catches>"));
            var CatchEndLine = XamlFileLines.ToList().FindIndex(x => x.Contains("</TryCatch.Catches>"));
            // convert this block to string
            string XAMLfile= "";
            if (CatchStartLine > 0 && CatchEndLine > 0)
            XAMLfile = string.Join(",", XamlFileLines.ToList().GetRange(CatchEndLine, CatchStartLine - CatchEndLine));

          
            // ExceptedWorkFlows always success
            if ((ExcludedWorkFlows.Contains(CurrentWorkflow.DisplayName)) || ((CurrentWorkflow.Project.Directory.ToString() + "\\" + CurrentWorkflow.RelativePath).Contains("IAP_")) || ((CurrentWorkflow.Project.Directory.ToString() + "\\" + CurrentWorkflow.RelativePath).Contains("ProcessName_")) || ((CurrentWorkflow.Project.Directory.ToString() + "\\" + CurrentWorkflow.RelativePath).Contains("Subprocess")) || ((CurrentWorkflow.Project.Directory.ToString() + "\\" + CurrentWorkflow.RelativePath).Contains("_Loader")) || ((CurrentWorkflow.Project.Directory.ToString() + "\\" + CurrentWorkflow.RelativePath).Contains("_Worker")) || ((CurrentWorkflow.Project.Directory.ToString() + "\\" + CurrentWorkflow.RelativePath).Contains("_LoaderWorker")))
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
                // looping on every activity
                foreach (var child in CurrentWorkflow.Root.Children)
                {
                    if (child.ToolboxName.Contains("TryCatch"))
                    {
                        TryCatchExisted = true;
                    }
                    else if (child.ToolboxName.ToLower().Contains("process_monitoring") || child.ToolboxName.ToLower().Contains("app_monitoring"))
                    {

                    }
                    else
                    {
                        OutsideTryCatch = true;
                    }
                }

                // activity existed outside the WF
                if (OutsideTryCatch == true)
                    messageList.Add(string.Format("There are activities existed outside the try catch activity."));

                // No try catch in the WF
                if (TryCatchExisted == false)
                    messageList.Add(string.Format("The following workflow: {0} doesn't have try catch activity.", CurrentWorkflow.DisplayName));
                else
                {
                    // if the try catch existed do the following checks
                    var childs = CurrentWorkflow.Root.Children.ToList();
                    var TryCatch = childs.FirstOrDefault(x => x.ToolboxName.Contains("TryCatch"));
                    bool FirstChild = true;
                    int ChildIndex = 1;


                   

                    // check for the generic exception in the xaml file (main catch block)
                    if (!XAMLfile.Contains("x:TypeArguments=\"s:Exception\""))
                    {
                        messageList.Add(string.Format("The following workflow: {0} doesn't have generic exception handler.", CurrentWorkflow.DisplayName));
                    }

                    // Log messages in the exceptions
                    foreach (var child in TryCatch.Children)
                    {
                        if (FirstChild)
                        {
                            // Exclude the checks on the Try Sequence
                            FirstChild = false;
                        }
                        else
                        {
                           
                            
                           
                            // info Log message
                            if (child.Children.FirstOrDefault(x => x.ToolboxName.ToLower().Contains("info_log")) != null)
                            {
                                messageList.Add(string.Format("The following workflow: {0} has IAP info log activity at the exception handling number {1}. Please replace it with Error log activity.", CurrentWorkflow.DisplayName, ChildIndex));
                            }
                          
                            ChildIndex++;
                        }
                    }
                }
            };

            // No Error message existed return Success
            if (!IsEmpty(messageList))
            {
                // Error message existed return the error
                return new InspectionResult()
                {
                    HasErrors = true,
                    Messages = messageList,
                    RecommendationMessage = "Use try catch activity. Or make sure that all invoked activities is inside the try catch.",
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