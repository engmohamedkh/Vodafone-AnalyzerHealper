using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UiPath.Studio.Activities.Api;
using UiPath.Studio.Activities.Api.Analyzer;
using UiPath.Studio.Activities.Api.Analyzer.Rules;
using UiPath.Studio.Analyzer.Models;

namespace FolderStructure
{
    public class FolderStructure : IRegisterAnalyzerConfiguration
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
        string RuleID = "VF-046";
        string RuleName = "Folder Structure";
        string WFAnalyzerVersion = "WorkflowAnalyzerV4";


        //Rule init
        public void Initialize(IAnalyzerConfigurationService workflowAnalyzerConfigService)
        {
            if (!workflowAnalyzerConfigService.HasFeature(WFAnalyzerVersion))
                return;

            var newRule = new Rule<IWorkflowModel>(RuleName, RuleID, FolderStructureImplementation)
            {
                /// Off and Verbose are not supported.
                ErrorLevel = System.Diagnostics.TraceLevel.Warning,
                RecommendationMessage = "Please move all .Xaml files to structured folders"
            };

            workflowAnalyzerConfigService.AddRule<IWorkflowModel>(newRule);
        }

        // Rule implementation
        private InspectionResult FolderStructureImplementation(IWorkflowModel CurrentWorkflow, Rule theNewRule)
        {
            var messageList = new List<string>();
            var FilePath = (CurrentWorkflow.Project.Directory + "\\" + CurrentWorkflow.RelativePath);
            if (FilePath.ToLower().Contains("worker.xaml") || FilePath.ToLower().Contains("loader.xaml") || FilePath.ToLower().Contains("loaderworker.xaml"))
            {
                return new InspectionResult() { HasErrors = false };
            }
            else
            {
                
                if (FilePath.ToLower().Contains("\\navigate\\") || FilePath.ToLower().Contains("\\other\\") || FilePath.ToLower().Contains("\\read\\") || FilePath.ToLower().Contains("\\write\\") || FilePath.ToLower().Contains("\\logic\\") || FilePath.ToLower().Contains("\\subprocess\\") || FilePath.ToLower().Contains("\\testing\\"))

                {
                    return new InspectionResult() { HasErrors = false };
                }
                else
                {
                    messageList.Add(string.Format("Add Sequence {0} to structured folders", CurrentWorkflow.DisplayName));
                }
            }

            if (messageList.Count > 0)
            {
                return new InspectionResult()
                {
                    HasErrors = true,
                    Messages = messageList,
                    RecommendationMessage = "Folow the folder structure of the process",
                    ErrorLevel = theNewRule.DefaultErrorLevel
                };
            }
            return new InspectionResult() { HasErrors = false };
        }
    }
}