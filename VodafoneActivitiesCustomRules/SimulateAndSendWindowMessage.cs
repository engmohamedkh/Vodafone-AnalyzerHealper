using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UiPath.Studio.Activities.Api;
using UiPath.Studio.Activities.Api.Analyzer;
using UiPath.Studio.Activities.Api.Analyzer.Rules;
using UiPath.Studio.Analyzer.Models;

namespace SimulateAndSendWindowMessageSpace
{
    public class SimulateAndSendWindowMessage : IRegisterAnalyzerConfiguration
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
        string RuleID = "VF-010";
        string RuleName = "SimulateAndSendWindowMessage";
        string WFAnalyzerVersion = "WorkflowAnalyzerV4";
        string ActivitiesList = "TypeSecureText,TypeInto,SendHotkey,Hover,Click";
        // init the rule to the studio
        public void Initialize(IAnalyzerConfigurationService workflowAnalyzerConfigService)
        {
            // checking the wf version
            if (!workflowAnalyzerConfigService.HasFeature(WFAnalyzerVersion))
                return;

            var newRule = new Rule<IActivityModel>(RuleName, RuleID, ImageBasedActivitiesCheck)
            {
                /// Off and Verbose are not supported.
                ErrorLevel = System.Diagnostics.TraceLevel.Warning,
                RecommendationMessage = "Checking SimulateClick / SimulateType / SendWindowMessage properties."
            };

            workflowAnalyzerConfigService.AddRule<IActivityModel>(newRule);
        }

        // Rule implementation
        private InspectionResult ImageBasedActivitiesCheck(IActivityModel activity, Rule theNewRule)
        {
            var messageList = new List<string>();
            bool simulateExist = false;
            bool simulateUsed = false;
            bool sendwindowmessagesExist = false;
            bool sendwindowmessagesUsed = false;

            // Search for the prohibt activities if existed 
            foreach (var activ in ActivitiesList.Split(',').ToList())
            {
                // return Error if existed
                if (activity.ToolboxName.Contains(activ))
                {
                    // check for the property
                    if (activity.Arguments.FirstOrDefault(x => x.DisplayName.ToLower().Contains("simulate")) != null)
                    {
                        simulateExist = true;
                        if (activity.Arguments.FirstOrDefault(x => x.DisplayName.ToLower().Contains("simulate")).DefinedExpression == "True")
                        {
                            simulateUsed = true;
                        }

                    }
                    // check for the property
                    if (activity.Arguments.FirstOrDefault(x => x.DisplayName.ToLower().Contains("sendwindowmessages")) != null)
                    {
                        sendwindowmessagesExist = true;
                        if (activity.Arguments.FirstOrDefault(x => x.DisplayName.ToLower().Contains("sendwindowmessages")).DefinedExpression == "True")
                        {
                            sendwindowmessagesUsed = true;
                        }
                    }

                    // doing the validations
                    if (simulateExist && sendwindowmessagesExist)
                    {
                        if ((simulateUsed || sendwindowmessagesUsed) != true)
                        {
                            messageList.Add(string.Format("The following activity: {0} has simulate and sendwindowmessages properties but not checked.", activity.DisplayName, activity.Arguments.FirstOrDefault(x => x.DisplayName.ToLower().Contains("simulate")).DefinedExpression));
                        }
                    }
                    else if (simulateExist)
                    {
                        if (!simulateUsed)
                            messageList.Add(string.Format("The following activity: {0} has simulate property but not checked.", activity.DisplayName, activity.Arguments.FirstOrDefault(x => x.DisplayName.ToLower().Contains("simulate")).DefinedExpression));
                    }
                    else if (sendwindowmessagesExist)
                    {
                        if (!sendwindowmessagesUsed)
                            messageList.Add(string.Format("The following activity: {0} has sendwindowmessages property but not checked.", activity.DisplayName, activity.Arguments.FirstOrDefault(x => x.DisplayName.ToLower().Contains("sendwindowmessages")).DefinedExpression));
                    }
                    else { };

                    // if there is any error
                    if (!IsEmpty(messageList))
                    {
                        return new InspectionResult()
                        {
                            HasErrors = true,
                            Messages = messageList,
                            RecommendationMessage = "Check proper property or add annotation for the reason why it's not checked.",
                            ErrorLevel = theNewRule.DefaultErrorLevel
                        };
                    }

                };
            }


            return new InspectionResult()
            {
                HasErrors = false
            };
        }
    }
}