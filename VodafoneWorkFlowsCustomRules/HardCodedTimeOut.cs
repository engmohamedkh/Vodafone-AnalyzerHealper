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

namespace HardCodedTimeOut
{
    public class HardCodedTimeOut : IRegisterAnalyzerConfiguration
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
        string RuleID = "VF-044";
        string RuleName = "HardCoded TimeOut";
        string WFAnalyzerVersion = "WorkflowAnalyzerV4";


        //Rule init
        public void Initialize(IAnalyzerConfigurationService workflowAnalyzerConfigService)
        {
            if (!workflowAnalyzerConfigService.HasFeature(WFAnalyzerVersion))
                return;

            var newRule = new Rule<IWorkflowModel>(RuleName, RuleID, HardCodedTimeOutImplemetation)
            {
                /// Off and Verbose are not supported.
                ErrorLevel = System.Diagnostics.TraceLevel.Warning,
                RecommendationMessage = "Please Check HardCoded Timeouts."
            };

            workflowAnalyzerConfigService.AddRule<IWorkflowModel>(newRule);
        }

        // Rule implementation
        private InspectionResult HardCodedTimeOutImplemetation(IWorkflowModel CurrentWorkflow, Rule theNewRule)
        {
            Regex validateNumberRegex = new Regex("^\\d+$");
            //Regex validateTimeoutRegex = new Regex("(?>TimeoutMS=)(?<x>\\S*)(?=>*)");
            Regex validateTimeoutRegex = new Regex("(?<=TimeoutMS=)(?<xy>.*)(?=>)");
            //Regex validateTimeoutRegex = new Regex("TimeoutMS=(?<xy>.*)>");

            var messageList = new List<string>();
            string[] XamlFileLines = File.ReadAllLines(CurrentWorkflow.Project.Directory.ToString() + "\\" + CurrentWorkflow.RelativePath);
            var TimeoutsARR = new List<string>();
            foreach (string item in XamlFileLines)
            {
                if (item.ToLower().Contains("timeoutms"))
                {
                    TimeoutsARR.Add(item);
                }
            }
            foreach (string item2 in TimeoutsARR)
            {
                if (validateNumberRegex.IsMatch(validateTimeoutRegex.Match(item2).Value.Replace("\"", "").Split(' ')[0]))
                {
                    messageList.Add(string.Format("Hardcoded Timeout exist at {0}", CurrentWorkflow.DisplayName));
                }
            }
            if (messageList.Count > 0)
            {
                return new InspectionResult()
                {
                    HasErrors = true,
                    Messages = messageList,
                    RecommendationMessage = "don't use any Hardcoded Values",
                    ErrorLevel = theNewRule.DefaultErrorLevel
                };
            }
            return new InspectionResult() { HasErrors = false };
        }
    }
}