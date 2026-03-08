using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UiPath.Studio.Activities.Api;
using UiPath.Studio.Activities.Api.Analyzer;
using UiPath.Studio.Activities.Api.Analyzer.Rules;
using UiPath.Studio.Analyzer.Models;


namespace UndocumentedDelaySpace
{
    public class UndocumentedDelay: IRegisterAnalyzerConfiguration
    {
        // Configs
        string RuleID = "VF-003";
        string RuleName = "UndocumentedDelay";
        string WFAnalyzerVersion = "WorkflowAnalyzerV4";

        // init the rule to the studio
        public void Initialize(IAnalyzerConfigurationService workflowAnalyzerConfigService)
        {
            // checking the wf version
            if (!workflowAnalyzerConfigService.HasFeature(WFAnalyzerVersion))
                return;

            var newRule = new Rule<IActivityModel>(RuleName, RuleID, UndocumentedDelayRole)
            {
                /// Off and Verbose are not supported.
                ErrorLevel = System.Diagnostics.TraceLevel.Warning,
                RecommendationMessage = "Checking the delay activities."
            };

            workflowAnalyzerConfigService.AddRule<IActivityModel>(newRule);
        }

        // Implemetation
        private InspectionResult UndocumentedDelayRole(IActivityModel CurrentActivity, Rule theNewRule)
        {
            var messageList = new List<string>();

            if (CurrentActivity.ToolboxName.Contains("Delay"))
            {
                // the delay activity doesn't have any annotation text
                messageList.Add(string.Format("The following activity: {0} is a delay activity. Try to delete it if it's not necessary.", CurrentActivity.DisplayName));

                try
                {
                    var TimeStringValue = CurrentActivity.Arguments.FirstOrDefault(x => x.Direction.Equals(ArgumentDirection.In) && x.Type.Contains("TimeSpan")).DefinedExpression;
                    if (TimeStringValue.Contains(":"))
                    {
                        messageList.Add(string.Format("The following delay activity: {0} has hardcoded value.", CurrentActivity.DisplayName));
                    }
                }
                catch (Exception)
                {

                    var TimeStringValue = CurrentActivity.Arguments.FirstOrDefault(x => x.Direction.Equals(ArgumentDirection.In) && x.Type.Contains("DateTime")).DefinedExpression;
                    if (TimeStringValue.Contains(":"))
                    {
                        messageList.Add(string.Format("The following delay Until activity: {0} has hardcoded value.", CurrentActivity.DisplayName));
                    }
                }
                // Error message existed return the error
                return new InspectionResult()
                {
                    HasErrors = true,
                    Messages = messageList,
                    RecommendationMessage = "Please use delay after or delay before instead.",
                    ErrorLevel = theNewRule.DefaultErrorLevel
                };
                
            };

            // No Error message existed return Success
            return new InspectionResult()
            {
                HasErrors = false
            };
        }
    }
}


