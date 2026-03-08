using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO.Enumeration;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Threading.Tasks;
using UiPath.Studio.Activities.Api;
using UiPath.Studio.Activities.Api.Analyzer;
using UiPath.Studio.Activities.Api.Analyzer.Rules;
using UiPath.Studio.Analyzer.Models;

namespace ValidateInputsSpace
{
    public class ValidateInput : IRegisterAnalyzerConfiguration
    {

        // Configs
        string RuleID = "VF-049";
        string RuleName = "InputValidation";
        string WFAnalyzerVersion = "WorkflowAnalyzerV4";
        List<string> TempList = new List<string>();
        bool ValidateFound = false;
        bool FileExists = false;
        // init the rule to the studio
        public void Initialize(IAnalyzerConfigurationService workflowAnalyzerConfigService)
        {
            // checking the wf version
            if (!workflowAnalyzerConfigService.HasFeature(WFAnalyzerVersion))
                return;

            var newRule = new Rule<IWorkflowModel>(RuleName, RuleID, InputsValidation)
            {
                
                /// Off and Verbose are not supported.
                ErrorLevel = System.Diagnostics.TraceLevel.Warning,
                RecommendationMessage = "Checking that Validate Inputs is used correctly"
            };

            workflowAnalyzerConfigService.AddRule<IWorkflowModel>(newRule);
        }
        //Recursive Function
        private void CheckInputValidation(IActivityModel ActivityToSearchIn)
        {
            if (ActivityToSearchIn.Children.Count > 0)
            {
                foreach (var chi in ActivityToSearchIn.Children.ToList())
                {
                    CheckInputValidation(chi);
                    if (ValidateFound)
                    {
                        break;
                    }
                }
            }
            else
            {
                if (ActivityToSearchIn.ToolboxName.ToString().ToLower().Trim().Contains("invokeworkflowfile"))
                {
                    ValidateFound = ActivityToSearchIn.Arguments.Any(arg => arg.DisplayName.ToLower().Equals("workflowfilename") && arg.DefinedExpression.ToLower().Contains(("Logic\\IAP_ValidateInputFiles.xaml").ToLower()));
                    if (ValidateFound)
                    {
                        foreach (var arg in ActivityToSearchIn.Arguments)
                        {
                            //if ((string.IsNullOrEmpty(arg.DefinedExpression) || arg.DefinedExpression.Equals("{}")) && (!(arg.DisplayName.Equals("WorkflowFileName") || arg.DisplayName.Equals("ContinueOnError") || arg.DisplayName.Equals("Timeout") || arg.DisplayName.Equals("Log Entry") || arg.DisplayName.Equals("Log Exit") || arg.DisplayName.Equals("ArgumentsVariable") || arg.DisplayName.Equals("LogLevel"))))
                            if ((string.IsNullOrEmpty(arg.DefinedExpression) || arg.DefinedExpression.Equals("new string(){}") || arg.DefinedExpression.Equals("new string() {}") || arg.DefinedExpression.Equals("{}") || arg.DefinedExpression.Equals("{\"\"}")) && !(arg.DisplayName.Equals("WorkflowFileName") || arg.DisplayName.Equals("ContinueOnError") || arg.DisplayName.Equals("Timeout") || arg.DisplayName.Equals("Log Entry") || arg.DisplayName.Equals("Log Exit") || arg.DisplayName.Equals("ArgumentsVariable") || arg.DisplayName.Equals("LogLevel")))
                            {
                                //if the arguments are not empty or {}
                                TempList.Add(string.Format("The Input file validation Workflow '{0}' has empty {1} argument: {2}.", ActivityToSearchIn.DisplayName, arg.Direction, arg.DisplayName));
                            }
                        }
                    }
                }
            }
        }
        // Rule implementation
        private InspectionResult InputsValidation(IWorkflowModel CurrentWorkflow, Rule theNewRule)
        {
            var messageList = new List<string>();
            
            if ((CurrentWorkflow.Project.Directory + "\\" + CurrentWorkflow.RelativePath).ToLower().Split('_').Last().Contains("worker.xaml") || (CurrentWorkflow.Project.Directory + "\\" + CurrentWorkflow.RelativePath).ToLower().Split('_').Last().Contains("loader.xaml"))
            {
                foreach (var file in Directory.EnumerateFiles(CurrentWorkflow.Project.Directory + "\\Logic"))
                {
                    if (file.Contains("Logic\\IAP_ValidateInputFiles.xaml"))
                    {
                        FileExists = true;
                    }
                }
                foreach (var child in CurrentWorkflow.Root.Children.ToList())
                { 
                    ValidateFound = false;
                    CheckInputValidation(child);
                    if (ValidateFound)
                    {
                        break;
                    }
                }
                if (!ValidateFound)
                {
                    messageList.Add("The Input file validation Workflow is not used in the process.");
                }

                if (TempList.Count > 0)
                {
                    messageList.AddRange(TempList);
                }
                TempList.Clear();
                if (!FileExists)
                {
                    messageList.Add("The Input file validation Workflow doesn't exist in the process.");
                }
                
            }
            
            if (messageList.Count > 0)
            {
                messageList = messageList.Distinct().ToList();
                return new InspectionResult()
                {
                    HasErrors = true,
                    Messages = messageList,
                    RecommendationMessage = "Please Use Input Validation file to validate inputs",
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
