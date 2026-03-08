using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UiPath.Studio.Activities.Api;
using UiPath.Studio.Activities.Api.Analyzer;
using UiPath.Studio.Activities.Api.Analyzer.Rules;
using UiPath.Studio.Analyzer.Models;

namespace CommentOutSpace
{
    public class CommentOut : IRegisterAnalyzerConfiguration
    {

        // Configs
        string RuleID = "VF-027";
        string RuleName = "CommentOutActivity";
        string WFAnalyzerVersion = "WorkflowAnalyzerV4";
        string CommentOutActivityName = "CommentOut";

        // init the rule to the studio
        public void Initialize(IAnalyzerConfigurationService workflowAnalyzerConfigService)
        {
            // checking the wf version
            if (!workflowAnalyzerConfigService.HasFeature(WFAnalyzerVersion))
                return;

            var newRule = new Rule<IActivityModel>(RuleName, RuleID, CommentOutImplementation)
            {
                /// Off and Verbose are not supported.
                ErrorLevel = System.Diagnostics.TraceLevel.Warning,
                RecommendationMessage = "Checking the comment out activities for every workflow."
            };

            workflowAnalyzerConfigService.AddRule<IActivityModel>(newRule);
        }

        // Rule implementation
        private InspectionResult CommentOutImplementation(IActivityModel activity, Rule theNewRule)
        {
            var messageList = new List<string>();

            // return Error if existed
            if (activity.ToolboxName.Contains(CommentOutActivityName))
            {
                messageList.Add(string.Format("The following activity: {0} is not recommended to use.", activity.DisplayName));

                return new InspectionResult()
                {
                    HasErrors = true,
                    Messages = messageList,
                    RecommendationMessage = "Please delete any comment out activies existed in the project.",
                    ErrorLevel = theNewRule.DefaultErrorLevel
                };
            };

            return new InspectionResult()
            {
                HasErrors = false
            };
        }
    }
}