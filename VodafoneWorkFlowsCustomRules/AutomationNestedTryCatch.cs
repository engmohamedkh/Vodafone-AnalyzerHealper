using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UiPath.Studio.Activities.Api;
using UiPath.Studio.Activities.Api.Analyzer;
using UiPath.Studio.Activities.Api.Analyzer.Rules;
using UiPath.Studio.Analyzer.Models;

namespace NestedTryCatchSpace
{
    public class AutomationNestedTryCatch : IRegisterAnalyzerConfiguration
    {

        // Configs
        string RuleID = "VF-043";
        string RuleName = "Nested TryCatch";
        string WFAnalyzerVersion = "WorkflowAnalyzerV4";
        bool NestedMatched = false;

        //Rule init
        public void Initialize(IAnalyzerConfigurationService workflowAnalyzerConfigService)
        {
            if (!workflowAnalyzerConfigService.HasFeature(WFAnalyzerVersion))
                return;

            var newRule = new Rule<IWorkflowModel>(RuleName, RuleID, UINestedTryCatch)
            {
                /// Off and Verbose are not supported.
                ErrorLevel = System.Diagnostics.TraceLevel.Warning,
                RecommendationMessage = "Checking if there is any Nested Try/Catch Activities."
            };

            workflowAnalyzerConfigService.AddRule<IWorkflowModel>(newRule);
        }

        // Rule implementation
        private InspectionResult UINestedTryCatch(IWorkflowModel CurrentWorkflow, Rule theNewRule)
        {
            var messageList = new List<string>();

            if ((CurrentWorkflow.Project.Directory.ToString() + "\\" + CurrentWorkflow.RelativePath).ToLower().Contains("automation"))
            {
                // read the current workflow as a text of lines
                string XamlFileLines = File.ReadAllText(CurrentWorkflow.Project.Directory.ToString() + "\\" + CurrentWorkflow.RelativePath);
                // string strRegexPattern = @"(?<=<TryCatch \b[^>]*)(?<x>[\s\S]*<TryCatch [\s\S]*)<\/TryCatch>";
                string strRegexPattern = @"(?<=<TryCatch \b[^>]*)(?<x>[\s\S]*<TryCatch [\s\S]*<\/TryCatch>[\s\S]*)<\/TryCatch>";
                
                NestedMatched = Regex.IsMatch(XamlFileLines, strRegexPattern,System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (NestedMatched)
                {
                    messageList.Add(string.Format("The following Workflow: '{0}' has Nested Try/Catche Activities.", CurrentWorkflow.DisplayName));
                }
                    
                               
                
            }
            
            // No Error message existed return Success
            if (messageList.Count > 0)
            {
                // Error message existed return the error
                return new InspectionResult()
                {
                    HasErrors = true,
                    Messages = messageList,
                    RecommendationMessage = "Remove any Nested TryCatches from the Logic.",
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