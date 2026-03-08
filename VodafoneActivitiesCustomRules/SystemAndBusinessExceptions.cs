using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UiPath.Studio.Activities.Api;
using UiPath.Studio.Activities.Api.Analyzer;
using UiPath.Studio.Activities.Api.Analyzer.Rules;
using UiPath.Studio.Analyzer.Models;

namespace BusinessSystemExceptionSpace
{
    public class BusinessSystemException : IRegisterAnalyzerConfiguration
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
        string RuleID = "VF-015";
        string RuleName = "BusinessSystemException";
        string WFAnalyzerVersion = "WorkflowAnalyzerV4";
        string ThrowExceptionActivity = "throw";
        string RethrowExceptionActivity = "rethrow";

        // init the rule to the studio
        public void Initialize(IAnalyzerConfigurationService workflowAnalyzerConfigService)
        {
            // checking the wf version
            if (!workflowAnalyzerConfigService.HasFeature(WFAnalyzerVersion))
                return;

            var newRule = new Rule<IActivityModel>(RuleName, RuleID, BusinessSystemExceptionList)
            {
                /// Off and Verbose are not supported.
                ErrorLevel = System.Diagnostics.TraceLevel.Info,
                RecommendationMessage = "List System Exceptions and Business Exceptions."
            };

            workflowAnalyzerConfigService.AddRule<IActivityModel>(newRule);
        }

        // Rule implementation
        private InspectionResult BusinessSystemExceptionList(IActivityModel activity, Rule theNewRule)
        {
            var messageList = new List<string>();

            // Throw activity
            if (activity.ToolboxName.ToLower().Contains(ThrowExceptionActivity.ToLower()) && (!(activity.ToolboxName.ToLower().Contains(RethrowExceptionActivity.ToLower()))))
            {
                if (activity.Arguments.FirstOrDefault(x => x.DisplayName.ToLower().Contains("exception")).DefinedExpression.ToLower().Contains("businessruleexception"))
                {
                    messageList.Add(string.Format("Business exception found: {0}.", activity.Arguments.FirstOrDefault(x => x.DisplayName.ToLower().Contains("exception")).DefinedExpression));
                    return new InspectionResult()
                    {
                        HasErrors = true,
                        Messages = messageList,
                        RecommendationMessage = "",
                        ErrorLevel = theNewRule.DefaultErrorLevel,
                    };
                }
                else if (activity.Arguments.FirstOrDefault(x => x.DisplayName.ToLower().Contains("exception")).DefinedExpression.ToLower().Contains("systemexception"))
                {
                    messageList.Add(string.Format("System exception found: {0}.", activity.Arguments.FirstOrDefault(x => x.DisplayName.ToLower().Contains("exception")).DefinedExpression));
                    return new InspectionResult()
                    {
                        HasErrors = true,
                        Messages = messageList,
                        RecommendationMessage = "",
                        ErrorLevel = theNewRule.DefaultErrorLevel,
                    };
                }


            }

            return new InspectionResult()
            {
                HasErrors = false
            };
        }
    }
}