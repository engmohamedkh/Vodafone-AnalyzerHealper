using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UiPath.Studio.Activities.Api;
using UiPath.Studio.Activities.Api.Analyzer;
using UiPath.Studio.Activities.Api.Analyzer.Rules;
using UiPath.Studio.Analyzer.Models;

namespace LogBrowserURLSpace
{
    public class LogBrowserURL : IRegisterAnalyzerConfiguration
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
        string RuleID = "VF-008";
        string RuleName = "Log Browser URL";
        string WFAnalyzerVersion = "WorkflowAnalyzerV4";
        string OpenBrowserActivity = "OpenBrowser";

        // init the rule to the studio
        public void Initialize(IAnalyzerConfigurationService workflowAnalyzerConfigService)
        {
            // checking the wf version
            if (!workflowAnalyzerConfigService.HasFeature(WFAnalyzerVersion))
                return;

            var newRule = new Rule<IActivityModel>(RuleName, RuleID, LogBrowserURLCheck)
            {
                /// Off and Verbose are not supported.
                ErrorLevel = System.Diagnostics.TraceLevel.Warning,
                RecommendationMessage = "Check URL logging activity inside the Open Browser activity."
            };

            workflowAnalyzerConfigService.AddRule<IActivityModel>(newRule);
        }

        // Rule implementation
        private InspectionResult LogBrowserURLCheck(IActivityModel activity, Rule theNewRule)
        {
            var messageList = new List<string>();

            // Throw activity
            if (activity.ToolboxName.ToLower().Contains(OpenBrowserActivity.ToLower()))
            {
                bool URLLogExisted = false;
                var childs = activity.Children;


                // if log existed check the values
                if (childs.Count > 0)
                {
                    // get any log activity inside the open browser 
                    var URLLog = childs.FirstOrDefault(x =>  (x.ToolboxName.Contains("Info_Log")));


                    if (URLLog != null)
                    {
                        // check inside the Open Browser
                        if (URLLog.Arguments.FirstOrDefault(y => y.DisplayName.Contains("Message")).DefinedExpression.ToLower().Trim().Replace("\"", "").Contains(activity.Arguments.FirstOrDefault(x => x.DisplayName.ToLower().Equals("url")).DefinedExpression.ToLower().Trim().Replace("\"", "")))
                        {
                            URLLogExisted = true;
                        }
                    }
                    else
                    {
                        childs = childs.ToArray()[0].Children;
                        if (childs.Count > 0)
                        {
                            URLLog = childs.FirstOrDefault(x => (x.ToolboxName.Contains("Info_Log")));

                            if (URLLog != null)
                            {
                                // check inside the Open Browser
                                if (URLLog.Arguments.FirstOrDefault(y => y.DisplayName.Contains("Message")).DefinedExpression.ToLower().Trim().Replace("\"", "").Contains(activity.Arguments.FirstOrDefault(x => x.DisplayName.ToLower().Equals("url")).DefinedExpression.ToLower().Trim().Replace("\"", "")))
                                {
                                    URLLogExisted = true;
                                }
                            }
                        }
                    }
                }

                if (URLLogExisted == false)
                {
                    messageList.Add(string.Format("URL logging activity is not existed in the following activity: {0}. ",activity.DisplayName));

                    return new InspectionResult()
                    {
                        HasErrors = true,
                        Messages = messageList,
                        RecommendationMessage = "Please make sure to add it inside the open browser activity.",
                        ErrorLevel = theNewRule.DefaultErrorLevel,
                    };
                }
                else
                {
                    messageList.Add(string.Format("URL logging activity is existed in the following activity: {0}. ",activity.DisplayName));

                    return new InspectionResult()
                    {
                        HasErrors = false,
                        Messages = messageList,
                        RecommendationMessage = "please use Logging actvity for the URL inside the browser activity",
                        ErrorLevel = System.Diagnostics.TraceLevel.Info,
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