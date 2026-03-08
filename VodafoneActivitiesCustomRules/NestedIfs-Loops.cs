using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UiPath.Studio.Activities.Api;
using UiPath.Studio.Activities.Api.Analyzer;
using UiPath.Studio.Activities.Api.Analyzer.Rules;
using UiPath.Studio.Analyzer.Models;

namespace NestedIfsLoops
{
    public class NestedIfsLoops : IRegisterAnalyzerConfiguration
    {

        // Configs
        string RuleID = "VF-047";
        string RuleName = "Nested IFs";
        string WFAnalyzerVersion = "WorkflowAnalyzerV4";
        List<string> TempList = new List<string>();
        string[] loops = { "InterruptibleWhile", "InterruptibleDoWhile", "ForEach", "ForEachRow" }; 
        // init the rule to the studio
        public void Initialize(IAnalyzerConfigurationService workflowAnalyzerConfigService)
        {
            // checking the wf version
            if (!workflowAnalyzerConfigService.HasFeature(WFAnalyzerVersion))
                return;

            var newRule = new Rule<IActivityModel>(RuleName, RuleID, NestedIfsRule)
            {
                /// Off and Verbose are not supported.
                ErrorLevel = System.Diagnostics.TraceLevel.Warning,
                RecommendationMessage = "Checking if there is any Nested IF/Loops Activities."
            };

            workflowAnalyzerConfigService.AddRule<IActivityModel>(newRule);
        }

        //Recusrive IFs function
        private void CountNestedIfs(IActivityModel IFsChild, IActivityModel BigIf, String ActivityName)
        {
            //Nested Found?
            if (IFsChild.ToolboxName.Trim().ToLower().Equals(ActivityName))
            {
                TempList.Add(string.Format("The following If Activity: '{0}' is Nested to another bigger one '{1}'. please Remove any Nested Ifs.", IFsChild.DisplayName, BigIf.DisplayName, ActivityName));

            }
            else
            {
                foreach (var chi in IFsChild.Children.ToList())
                {
                    CountNestedIfs(chi, BigIf, ActivityName);
                }
            }
        }
        //Recusrive Loops function
        private void CountNestedLoops(IActivityModel IFsChild, IActivityModel BigLoop)
        {
            //Nested Found?
            if (loops.Any(x => x.Trim().ToLower().Equals(IFsChild.ToolboxName.Trim().ToLower())))
            {
                TempList.Add(string.Format("The following Loop Activity: '{0}' is Nested to another bigger one '{1}'. please Remove any Nested Loopss.", IFsChild.DisplayName, BigLoop.DisplayName));

            }
            else
            {
                foreach (var chi in IFsChild.Children.ToList())
                {
                    CountNestedLoops(chi, BigLoop);
                }
            }


        }


        // Rule implementation
        private InspectionResult NestedIfsRule(IActivityModel activity, Rule theNewRule)
        {

            var messageList = new List<string>();
            
            // NEsted IFs Checks
            if (activity.ToolboxName.Trim().ToLower().Equals("if"))
            {
                var IFsChilds = activity.Children;
                
                foreach (var child in IFsChilds)
                {
                    CountNestedIfs(child,activity, activity.ToolboxName.Trim().ToLower());
                }
              
                if (TempList.Count > 0)
                {
                    messageList.AddRange(TempList);
                }
            }
            //Nested Loops Check
            else if (loops.Any(x=>x.Trim().ToLower().Equals(activity.ToolboxName.Trim().ToLower())))
            {
                var LoopsChils = activity.Children;

                foreach (var child in LoopsChils)
                {
                    CountNestedLoops(child,activity);
                }

                if (TempList.Count > 0)
                {
                    messageList.AddRange(TempList);
                }
            };
            if (messageList.Count > 0)
            {
                TempList.Clear();
                messageList = messageList.Distinct().ToList();
                return new InspectionResult()
                {
                    HasErrors = true,
                    Messages = messageList,
                    RecommendationMessage = "Remove Any Nested IFs/Loops from the logic.",
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