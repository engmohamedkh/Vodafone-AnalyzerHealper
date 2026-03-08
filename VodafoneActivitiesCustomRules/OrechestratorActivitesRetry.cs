using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UiPath.Studio.Activities.Api;
using UiPath.Studio.Activities.Api.Analyzer;
using UiPath.Studio.Activities.Api.Analyzer.Rules;
using UiPath.Studio.Analyzer.Models;


namespace OrechestratorActivitesRetrySpace
{
    public class OrechestratorActivitesRetry : IRegisterAnalyzerConfiguration
    {
        string RuleID = "VF-048";
        string RuleName = "OrechestratorActivitesRetry";
        string WFAnalyzerVersion = "WorkflowAnalyzerV4";
        string QueueActivitiesList = "AddQueueItem,AddTransactionItem,BulkAddQueueItems,DeleteQueueItems,GetQueueItems,GetQueueItem,PostponeTransactionItem,SetTransactionProgress,SetTransactionStatus";
        bool retrScopeExist = false;


        // check list size
        public static bool IsEmpty<T>(List<T> list)
        {
            if (list == null)
            {
                return true;
            }

            return !list.Any();
        }
        // init the rule to the studio
        public void Initialize(IAnalyzerConfigurationService workflowAnalyzerConfigService)
        {
            // checking the wf version
            if (!workflowAnalyzerConfigService.HasFeature(WFAnalyzerVersion))
                return;

            var newRule = new Rule<IActivityModel>(RuleName, RuleID, QueueActivitiesInRetry)
            {
                /// Off and Verbose are not supported.
                ErrorLevel = System.Diagnostics.TraceLevel.Warning,
                RecommendationMessage = "Checking if Queue activities are inside a retry scope ."
            };

            workflowAnalyzerConfigService.AddRule<IActivityModel>(newRule);
        }
        private void LookForRetryScope(IActivityModel FirstParent)
        {//if the parent is retry scope
            if (FirstParent.ToolboxName.Contains("RetryScope"))
            {
                retrScopeExist = true;
            }
            if (FirstParent.Parent == null)
            {//break  the loop if this is the root activity
            }
            else
            {
                LookForRetryScope(FirstParent.Parent);
            }
        }

        // Rule implementation
        private InspectionResult QueueActivitiesInRetry(IActivityModel activity, Rule theNewRule)
        {
            var messageList = new List<string>();
            // Search for the queue activities if existed 
            // search for retry scope inside it if existed
            if (QueueActivitiesList.Split(',').ToList().Contains(activity.ToolboxName))
            {
                //reset the retry scope bool 
                retrScopeExist = false;
                LookForRetryScope(activity.Parent);
                if (!retrScopeExist)
                {
                    messageList.Add(string.Format("The following activity: {0} is not included in Retry Scope.", activity.DisplayName));
                }
            };


            if (!IsEmpty(messageList))
            {
                // Error message existed return the error
                return new InspectionResult()
                {
                    HasErrors = true,
                    Messages = messageList,
                    RecommendationMessage = "Make sure to include the queue activity in a retry scope.",
                    ErrorLevel = theNewRule.DefaultErrorLevel
                };
            }
            return new InspectionResult()
            {
                HasErrors = false
            };
        }
    }
}