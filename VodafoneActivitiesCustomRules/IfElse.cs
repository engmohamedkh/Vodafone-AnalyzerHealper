using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UiPath.Studio.Activities.Api;
using UiPath.Studio.Activities.Api.Analyzer;
using UiPath.Studio.Activities.Api.Analyzer.Rules;
using UiPath.Studio.Analyzer.Models;

namespace IfElseSpace
{
    public class IfElse : IRegisterAnalyzerConfiguration
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
        string RuleID = "VF-029";
        string RuleName = "IfElseCheck";
        string WFAnalyzerVersion = "WorkflowAnalyzerV4";
        string IfElseActivityName = "If";

        // init the rule to the studio
        public void Initialize(IAnalyzerConfigurationService workflowAnalyzerConfigService)
        {
            // checking the wf version
            if (!workflowAnalyzerConfigService.HasFeature(WFAnalyzerVersion))
                return;

            var newRule = new Rule<IActivityModel>(RuleName, RuleID, IfElseImplementation)
            {
                /// Off and Verbose are not supported.
                ErrorLevel = System.Diagnostics.TraceLevel.Warning,
                RecommendationMessage = "Checking the if activies if it empty or not."
            };

            workflowAnalyzerConfigService.AddRule<IActivityModel>(newRule);
        }

        // Rule implementation
        private InspectionResult IfElseImplementation(IActivityModel activity, Rule theNewRule)
        {
            try
            {
                var messageList = new List<string>();

                // return Error if existed
                if (activity.ToolboxName.Equals(IfElseActivityName))
                {
                    var IfElseChilds = activity.Children;

                    if (!(IfElseChilds.Count == 2))
                    {
                        messageList.Add(string.Format("The following activity: {0} have empty if/else activity.", activity.DisplayName));
                    }
                    else
                    {
                        foreach (var child in IfElseChilds)
                        {
                            if (child.ToolboxName.Contains("Sequence") || child.ToolboxName.Contains("Flowchart"))
                            {
                                if (child.Children.Count == 0)
                                {
                                    messageList.Add(string.Format("The following activity: {0} have empty if/else activity.", activity.DisplayName));
                                }
                            }
                        }
                    }
                    if (!IsEmpty(messageList))
                        return new InspectionResult()
                        {
                            HasErrors = true,
                            Messages = messageList,
                            RecommendationMessage = "Add at least log message.",
                            ErrorLevel = theNewRule.DefaultErrorLevel
                        };
                };

                return new InspectionResult()
                {
                    HasErrors = false
                };
            }
            catch (Exception ex)
            {
                return new InspectionResult()
                {
                    HasErrors = false
                };
            }

        }
    }
}