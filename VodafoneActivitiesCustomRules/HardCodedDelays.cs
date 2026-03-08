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

namespace HardCodedDelays
{
    public class HardCodedDelays : IRegisterAnalyzerConfiguration
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
        string RuleID = "VF-045";
        string RuleName = "HardCodedDelays";
        string WFAnalyzerVersion = "WorkflowAnalyzerV4";

        public void Initialize(IAnalyzerConfigurationService workflowAnalyzerConfigService)
        {
            // checking the wf version
            if (!workflowAnalyzerConfigService.HasFeature(WFAnalyzerVersion))
                return;

            var newRule = new Rule<IActivityModel>(RuleName, RuleID, InspectVariableForString)
            {
                /// Off and Verbose are not supported.
                ErrorLevel = System.Diagnostics.TraceLevel.Warning,
                RecommendationMessage = "Checking if Hardcoded Delays activities found."
            };

            workflowAnalyzerConfigService.AddRule<IActivityModel>(newRule);
        }

        // Rule implementation
        private InspectionResult InspectVariableForString(IActivityModel ActivityToInspect, Rule ConfiguredRule)
        {
            var messageList = new List<string>();
            var Delprops = ActivityToInspect.Arguments.Where(x => x.DisplayName.ToLower().Contains("delay")).ToList();
            Regex validateNumberRegex = new Regex("^\\d+$");

            foreach (var item in Delprops)
            {
                if (item.DefinedExpression == null)
                {

                }
                else
                {
                    string y = item.DefinedExpression;
                    if (validateNumberRegex.IsMatch(y))
                    {
                        messageList.Add(string.Format("The following activity {0} has hard coded {1} value {2}.", ActivityToInspect.DisplayName, item.DisplayName, item.DefinedExpression));
                    }
                }
            }
            if (messageList.Count > 0)
            {
                return new InspectionResult()
                {
                    HasErrors = true,
                    Messages = messageList,
                    RecommendationMessage = "Please don't use any hardcoded Delays.",
                    ErrorLevel = ConfiguredRule.ErrorLevel
                };
            }
            return new InspectionResult() { HasErrors = false };

        }
    }
}