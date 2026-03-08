using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UiPath.Studio.Activities.Api;
using UiPath.Studio.Activities.Api.Analyzer;
using UiPath.Studio.Activities.Api.Analyzer.Rules;
using UiPath.Studio.Analyzer.Models;

namespace BranchesLogSpace
{
    public class BranchesLogs : IRegisterAnalyzerConfiguration
    {

        // Configs
        string RuleID = "VF-038";
        string RuleName = "Branches Logging";
        string WFAnalyzerVersion = "WorkflowAnalyzerV4";
        string[] BranchesActivityName = {"If", "InterruptibleWhile", "InterruptibleDoWhile", "ForEach", "ForEachRow", "Switch", "FlowDecision", "FlowSwitch" };

        // init the rule to the studio
        public void Initialize(IAnalyzerConfigurationService workflowAnalyzerConfigService)
        {
            // checking the wf version
            if (!workflowAnalyzerConfigService.HasFeature(WFAnalyzerVersion))
                return;

            var newRule = new Rule<IActivityModel>(RuleName, RuleID, BrancheLoggingImplementation)
            {
                /// Off and Verbose are not supported.
                ErrorLevel = System.Diagnostics.TraceLevel.Warning,
                RecommendationMessage = "Checking that all Loops and Branches starting with Log Message"
            };

            workflowAnalyzerConfigService.AddRule<IActivityModel>(newRule);
        }

        // Rule implementation
        private InspectionResult BrancheLoggingImplementation(IActivityModel activity, Rule theNewRule)
        {
            var messageList = new List<string>();
            

            //check if one of the needed activities
            if (BranchesActivityName.Any(x => x.Trim().ToLower().Equals(activity.ToolboxName.ToString().ToLower().Trim())))
            {   //Activity direct children
                var Children = activity.Children;
                //check children count
                if ((activity.ToolboxName.ToLower().Equals("if") && Children.Count == 2) || (activity.ToolboxName.ToLower().Equals("foreach") && Children.Count >= 1)|| (activity.ToolboxName.ToLower().Equals("flowdecision") && Children.Count >= 1)|| (activity.ToolboxName.ToLower().Equals("switch") && Children.Count >= 1) || (activity.ToolboxName.ToLower().Equals("foreachrow") && Children.Count >= 1) || (activity.ToolboxName.ToLower().Equals("flowswitch") && Children.Count >= 1))
                {
                    //loop on every child to check i its _log or its sequence/flowchart
                    foreach (var ElseOrThen in Children)
                    {
                        String LogLevelActivty = "";

                        if (ElseOrThen.ToolboxName.Contains("Sequence") || ElseOrThen.ToolboxName.Contains("Flowchart"))
                        {
                            if (ElseOrThen.Children.Count == 0)
                                LogLevelActivty = ElseOrThen.ToolboxName.ToString();
                            else
                                LogLevelActivty = ElseOrThen.Children.ToArray()[0].ToolboxName.ToString();
                        }
                        else
                        {
                            LogLevelActivty = ElseOrThen.ToolboxName.ToString();
                        }

                        if (String.IsNullOrEmpty(LogLevelActivty) || !LogLevelActivty.ToLower().EndsWith("_log"))
                        {
                            messageList.Add(string.Format("The following activity: {0} doeasn't start with Log Activity inside its branches.", activity.DisplayName));
                        }
                    }
                }//invalid children countFlowSwitch

                else if ((activity.ToolboxName.ToLower().Contains("while")))
                {
                    foreach (var whileChildern in Children)
                    {
                        if(whileChildern.ToolboxName.ToLower().Equals("sequence"))
                        {
                            var sequanceChils = whileChildern.Children;
                            var result = sequanceChils.FirstOrDefault();

                            if (result == null)
                            {
                                messageList.Add(string.Format("The following activity: {0} has empty branch/es. should start with Log Activities", activity.DisplayName));
                            }
                            else
                            {
                                if(result.ToolboxName.ToLower()== "info_log")
                                {

                                }
                                else
                                {
                                    messageList.Add(string.Format("The following activity: {0} has empty branch/es. should start with Log Activities", activity.DisplayName));
                                }         
                            }

                        }
                        else if(whileChildern.ToolboxName.ToLower().Equals("info_log"))
                        {
                            messageList.Add(string.Format("The following activity: {0} has empty branch/es.Please delete it incase not needed", activity.DisplayName));
                        }
                        else if(whileChildern.ToolboxName.ToLower().Equals("literal"))
                        {

                        }
                        else
                        {
                            messageList.Add(string.Format("Dee The following activity: {0} has empty branch/es. should start with Log Activities", activity.DisplayName));

                        }
                    }

                }
                else
                {
                    messageList.Add(string.Format("The following activity: {0} has empty branch/es. should start with Log Activities", activity.DisplayName));
                }
                
                if (messageList.Count > 0)
                {
                    messageList = messageList.Distinct().ToList();
                    return new InspectionResult()
                    {
                        HasErrors = true,
                        Messages = messageList,
                        RecommendationMessage = "Add log message at the beginning of every branch.",
                        ErrorLevel = theNewRule.DefaultErrorLevel
                    };

                }
                return new InspectionResult()
                {
                    HasErrors = false
                };
            }
            else
            {
                return new InspectionResult()
                {
                    HasErrors = false
                };
            }
        }
    }
}
