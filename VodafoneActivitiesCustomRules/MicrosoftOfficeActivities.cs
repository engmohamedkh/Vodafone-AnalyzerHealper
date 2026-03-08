using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UiPath.Studio.Activities.Api;
using UiPath.Studio.Activities.Api.Analyzer;
using UiPath.Studio.Activities.Api.Analyzer.Rules;
using UiPath.Studio.Analyzer.Models;

namespace MicrosoftOfficeActivitiesSpace
{
    public class MicrosoftOfficeActivities : IRegisterAnalyzerConfiguration
    {
        string RuleID = "VF-036";
        string RuleName = "MicrosoftOfficeActivities";
        string WFAnalyzerVersion = "WorkflowAnalyzerV4";
        string MicrosoftOfficeActivitiesList = "Excel,Word,Database";
        string ExcludedArgumentName = "workbook path";
        string ExcelScopeName = "ExcelApplicationScope";

        // init the rule to the studio
        public void Initialize(IAnalyzerConfigurationService workflowAnalyzerConfigService)
        {
            // checking the wf version
            if (!workflowAnalyzerConfigService.HasFeature(WFAnalyzerVersion))
                return;

            var newRule = new Rule<IActivityModel>(RuleName, RuleID, MicrosoftOfficeActivity)
            {
                /// Off and Verbose are not supported.
                ErrorLevel = System.Diagnostics.TraceLevel.Warning,
                RecommendationMessage = "Checking the Microsoft Office Activities: " + MicrosoftOfficeActivitiesList + " ."
            };

            workflowAnalyzerConfigService.AddRule<IActivityModel>(newRule);
        }

        // Rule implementation
        private InspectionResult MicrosoftOfficeActivity(IActivityModel activity, Rule theNewRule)
        {
            var messageList = new List<string>();

            // Search for the microsoft activities if existed 
            foreach (var activ in MicrosoftOfficeActivitiesList.Split(',').ToList())
            {
                // return Error if existed
                if (activity.Type.Contains(activ))
                {   //if this is workbook activities exclude it
                    if ((activity.Arguments.FirstOrDefault(x => x.DisplayName.ToLower().Contains(ExcludedArgumentName)) == null) || activity.Type.Contains(ExcelScopeName))
                    {
                        messageList.Add(string.Format("The following activity: {0} requires Microsoft Office to be Installed.", activity.DisplayName, activ));

                        return new InspectionResult()
                        {
                            HasErrors = true,
                            Messages = messageList,
                            RecommendationMessage = "Make sure the process has Microsoft Office license.",
                            ErrorLevel = theNewRule.DefaultErrorLevel
                        };
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