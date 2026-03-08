using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UiPath.Studio.Activities.Api;
using UiPath.Studio.Activities.Api.Analyzer;
using UiPath.Studio.Activities.Api.Analyzer.Rules;
using UiPath.Studio.Analyzer.Models;

namespace ProhibtedActivitySpace
{
    public class ProhibtedActivity : IRegisterAnalyzerConfiguration
    {

        // Configs
        string RuleID = "VF-016";
        string RuleName = "ProhibitedActivities";
        string WFAnalyzerVersion = "WorkflowAnalyzerV4";
        string ProhibtedActivitiesList = "Exchange,Outlook,WriteLine,MessageBox,DeleteQueueItems";

        // init the rule to the studio
        public void Initialize(IAnalyzerConfigurationService workflowAnalyzerConfigService)
        {
            // checking the wf version
            if (!workflowAnalyzerConfigService.HasFeature(WFAnalyzerVersion))
                return;

            var newRule = new Rule<IActivityModel>(RuleName, RuleID , ProhibtedActivities)
            {
                /// Off and Verbose are not supported.
                ErrorLevel = System.Diagnostics.TraceLevel.Error,
                RecommendationMessage = "Checking the prohibit activities: " + ProhibtedActivitiesList + " ."
            };

            workflowAnalyzerConfigService.AddRule<IActivityModel>(newRule);
        }

        // Rule implementation
        private InspectionResult ProhibtedActivities(IActivityModel activity, Rule theNewRule)
        {
            var messageList = new List<string>();

            // Search for the prohibt activities if existed 
            foreach (var activ in ProhibtedActivitiesList.Split(',').ToList())
            {
                // return Error if existed
                if (activity.ToolboxName.Contains(activ))
                {
                    messageList.Add(string.Format("The following activity: {0} is existing in the prohibited activity list. Please don't use it.", activity.DisplayName, activ));

                    return new InspectionResult()
                    {
                        HasErrors = true,
                        Messages = messageList,
                        RecommendationMessage = "Use the allowed activities instead.",
                        ErrorLevel = theNewRule.DefaultErrorLevel
                    };                    
                };
            }
            

            return new InspectionResult()
            {
                HasErrors = false
            };
        }
    }
}