using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UiPath.Studio.Activities.Api;
using UiPath.Studio.Activities.Api.Analyzer;
using UiPath.Studio.Activities.Api.Analyzer.Rules;
using UiPath.Studio.Analyzer.Models;

namespace MissingInOutArgumentSpace
{
    public class MissingInOutArgument : IRegisterAnalyzerConfiguration
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
        string RuleID = "VF-014";
        string RuleName = "MissingInOutArgument";
        string WFAnalyzerVersion = "WorkflowAnalyzerV4";
        string InvokeActivity = "invokeworkflowfile";

        // init the rule to the studio
        public void Initialize(IAnalyzerConfigurationService workflowAnalyzerConfigService)
        {
            // checking the wf version
            if (!workflowAnalyzerConfigService.HasFeature(WFAnalyzerVersion))
                return;

            var newRule = new Rule<IActivityModel>(RuleName, RuleID, MissingInOutArgumentCheck)
            {
                /// Off and Verbose are not supported.
                ErrorLevel = System.Diagnostics.TraceLevel.Error,
                RecommendationMessage = "Checking all invoke activities and validate every argument."
            };

            workflowAnalyzerConfigService.AddRule<IActivityModel>(newRule);
        }

        // Rule implementation
        private InspectionResult MissingInOutArgumentCheck(IActivityModel activity, Rule theNewRule)
        {
            var messageList = new List<string>();

            // Throw activity
            if (activity.ToolboxName.ToLower().Contains(InvokeActivity.ToLower()))
            {
                foreach (var arg in activity.Arguments)
                {
                    if (!(arg.DisplayName.Equals("WorkflowFileName") || arg.DisplayName.Replace(" ","").ToLower().Equals("continueonerror") || arg.DisplayName.Replace(" ", "").ToLower().Equals("timeout") || arg.DisplayName.Replace(" ", "").ToLower().Equals("logentry") || arg.DisplayName.Replace(" ", "").ToLower().Equals("logexit") || arg.DisplayName.Replace(" ", "").ToLower().Equals("argumentsvariable") || arg.DisplayName.Replace(" ", "").ToLower().Equals("loglevel") || arg.DisplayName.Replace(" ", "").ToLower().Equals("logentry") || arg.DisplayName.Replace(" ", "").ToLower().Equals("level")))
                    {
                        if (string.IsNullOrEmpty(arg.DefinedExpression))
                        {
                            messageList.Add(string.Format("The following activity {0} has empty {1} argument: {2}.", activity.DisplayName, arg.Direction, arg.DisplayName));
                        }
                    }
                }

                if (!IsEmpty(messageList))
                    return new InspectionResult()
                    {
                        HasErrors = true,
                        Messages = messageList,
                        RecommendationMessage = "Kindly add default value for the mentioned argument.",
                        ErrorLevel = theNewRule.DefaultErrorLevel,
                    };

            }

            return new InspectionResult()
            {
                HasErrors = false
            };
        }
    }
}