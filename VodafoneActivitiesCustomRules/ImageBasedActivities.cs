using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UiPath.Studio.Activities.Api;
using UiPath.Studio.Activities.Api.Analyzer;
using UiPath.Studio.Activities.Api.Analyzer.Rules;
using UiPath.Studio.Analyzer.Models;

namespace ImageBasedActivitiesSpace
{
    public class ImageBasedActivities : IRegisterAnalyzerConfiguration
    {

        // Configs
        string RuleID = "VF-002";
        string RuleName = "ImageBasedActivities";
        string WFAnalyzerVersion = "WorkflowAnalyzerV4";
        string ImageBasedActivitiesList = "OnImageAppear,OnImageVanish,WaitingImageAppear,FindImageMatches,ImageFound,ClickImage,HoverImage,WaitImageVanish,ClickImageTrigger";

        // init the rule to the studio
        public void Initialize(IAnalyzerConfigurationService workflowAnalyzerConfigService)
        {
            // checking the wf version
            if (!workflowAnalyzerConfigService.HasFeature(WFAnalyzerVersion))
                return;

            var newRule = new Rule<IActivityModel>(RuleName, RuleID, ImageBasedActivitiesCheck)
            {
                /// Off and Verbose are not supported.
                ErrorLevel = System.Diagnostics.TraceLevel.Warning ,
                RecommendationMessage = "This rule will check for the following image based activities: "+ ImageBasedActivitiesList + " ."
            };

            workflowAnalyzerConfigService.AddRule<IActivityModel>(newRule);
        }

        // Rule implementation
        private InspectionResult ImageBasedActivitiesCheck(IActivityModel activity, Rule theNewRule)
        {
            var messageList = new List<string>();

            // Search for the image based activities if existed 
            foreach (var activ in ImageBasedActivitiesList.Split(',').ToList())
            {
                // return Error if existed
                if (activity.ToolboxName.Contains(activ))
                {
                    messageList.Add(string.Format("The following activity: {0} is image based activity, and it's not recommended to be used.", activity.DisplayName));

                    return new InspectionResult()
                    {
                        HasErrors = true,
                        Messages = messageList,
                        RecommendationMessage = "Please verify if required or just use windows activities instead.",
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