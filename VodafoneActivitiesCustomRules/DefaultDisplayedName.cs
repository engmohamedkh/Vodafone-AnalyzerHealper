using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UiPath.Studio.Activities.Api;
using UiPath.Studio.Activities.Api.Analyzer;
using UiPath.Studio.Activities.Api.Analyzer.Rules;
using UiPath.Studio.Analyzer.Models;

namespace DefaultDisplayedNameSpace
{
    public class DefaultDisplayedName: IRegisterAnalyzerConfiguration
    {

        // Configs
        string RuleID = "VF-031";
        string RuleName = "DefaultDisplayedName";
        string WFAnalyzerVersion = "WorkflowAnalyzerV4";
        string DefaultDisplayedNAames = "Sequence,Click,Get Text,Type Into,Activate,Find Element,Maximize Window,Do While,For Each,If,Switch,While,Flowchart,Flow Switch,Flow Decision";

        // init the rule to the studio
        public void Initialize(IAnalyzerConfigurationService workflowAnalyzerConfigService)
        {
            // checking the wf version
            if (!workflowAnalyzerConfigService.HasFeature(WFAnalyzerVersion))
                return;

            var newRule = new Rule<IActivityModel>(RuleName, RuleID, DefaultDisplayedNAamesImplementation)
            {
                /// Off and Verbose are not supported.
                ErrorLevel = System.Diagnostics.TraceLevel.Info,
                RecommendationMessage = "Checking the Displayed default name for each activity."
            };

            workflowAnalyzerConfigService.AddRule<IActivityModel>(newRule);
        }

        // Rule implementation
        private InspectionResult DefaultDisplayedNAamesImplementation(IActivityModel activity, Rule theNewRule)
        {
            var messageList = new List<string>();

            //Create default name list
            string[] DefaultDisplayedNAameslist = DefaultDisplayedNAames.Split(',');
            //flage represent the activity displayed name is defaultor not
            bool NameCheck = true;
            try
            {
                //search for the activity into list
                DefaultDisplayedNAameslist.Where(i => i.Equals(activity.DisplayName)).First();
                //Activity displayed name is default
                NameCheck = true;
            }
            catch (InvalidOperationException)
            {
                //Activity displayed name is not deafult
                NameCheck = false;
            }
            //check the flag 
            if (NameCheck == true)
            {

                messageList.Add(string.Format("The following activity: {0} has a default displayed name.", activity.DisplayName));

                return new InspectionResult()
                {
                    HasErrors = true,
                    Messages = messageList,
                    RecommendationMessage = "Please change the default dispalyed name to a descriptive name",
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