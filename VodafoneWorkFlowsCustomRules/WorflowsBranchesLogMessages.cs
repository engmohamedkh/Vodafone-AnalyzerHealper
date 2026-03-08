using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.IO;
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

        // init the rule to the studio
        public void Initialize(IAnalyzerConfigurationService workflowAnalyzerConfigService)
        {

            // checking the wf version
            if (!workflowAnalyzerConfigService.HasFeature(WFAnalyzerVersion))
                return;
            
            var newRule = new Rule<IWorkflowModel>(RuleName, RuleID, BrancheLoggingImplementation)
            {
                /// Off and Verbose are not supported.
                ErrorLevel = System.Diagnostics.TraceLevel.Warning,
                RecommendationMessage = "Checking that all Loops and Branches starting with Log Message"
            };

            workflowAnalyzerConfigService.AddRule<IWorkflowModel>(newRule);
        }

        // Rule implementation
        private InspectionResult BrancheLoggingImplementation(IWorkflowModel CurrentWorkflow, Rule theNewRule)
        {
            var messageList = new List<string>();
            // read the current workflow as a text of lines
            string XamlFileLines = File.ReadAllText(CurrentWorkflow.Project.Directory.ToString() + "\\" + CurrentWorkflow.RelativePath);
            // get the lines between start and end of a flow decision
            MatchCollection FlowDec = Regex.Matches(XamlFileLines, @"(<FlowDecision x:(.\n*)*)(?=<\/FlowDecision>)");
            foreach (var DecMatch in FlowDec)
            {
                var DisplayedNameIndex = DecMatch.ToString().Split('"').ToList().FindIndex(x => x.Contains("DisplayName=")) + 1;
                MatchCollection TrueFlowDec = Regex.Matches(DecMatch.ToString(), @"(<FlowDecision\.True(.\n*)*)(?=<\/FlowDecision\.True>)");
                MatchCollection FalseFlowDec = Regex.Matches(XamlFileLines, @"(<FlowDecision\.False(.\n*)*)(?=<\/FlowDecision\.False>)");
                if (TrueFlowDec.Count == 0)
                {
                    messageList.Add(String.Format("The following workflow '{0}' has a FlowDecision '{1}' that has an empty True branch; please consider adding a log activity.",CurrentWorkflow.DisplayName, DecMatch.ToString().Split('"').ToList()[DisplayedNameIndex]));
                }
                else if (!TrueFlowDec[0].ToString().Contains("<i:Info_Log"))
                {
                    messageList.Add(String.Format("The following workflow '{0}' has a FlowDecision '{1}' that doeasn't have Log Activity inside its True branch.",CurrentWorkflow.DisplayName, DecMatch.ToString().Split('"').ToList()[DisplayedNameIndex]));
                }
                if (FalseFlowDec.Count == 0)
                {
                    messageList.Add(String.Format("The following workflow '{0}' has a FlowDecision '{1}' that has an empty False branch; please consider adding a log activity.",CurrentWorkflow.DisplayName, DecMatch.ToString().Split('"').ToList()[DisplayedNameIndex]));

                }
                else if (!FalseFlowDec[0].ToString().Contains("<i:Info_Log"))
                {
                    messageList.Add(String.Format("The following workflow '{0}' has a FlowDecision '{1}' that doeasn't have Log Activity inside its False branch.",CurrentWorkflow.DisplayName, DecMatch.ToString().Split('"').ToList()[DisplayedNameIndex]));
                }
            }
            
            //Flow Switch
            MatchCollection FlowSwitch = Regex.Matches(XamlFileLines, @"(<FlowSwitch x:(.\n*)*)(?=<\/FlowSwitch>)");
            foreach (var SwitchMatch in FlowSwitch)
            {
                var DisplayedNameIndex = SwitchMatch.ToString().Split('"').ToList().FindIndex(x => x.Contains("DisplayName=")) + 1;
                //get default case
                MatchCollection DefaultCase = Regex.Matches(SwitchMatch.ToString(), @"(<FlowSwitch\.Default(.\n*)*)(?=<\/FlowSwitch\.Default>)");
                if (DefaultCase.Count == 0)
                {
                    messageList.Add(String.Format("The following FlowSwitch '{0}' has an empty Default branch, please consider adding a log activity.",  SwitchMatch.ToString().Split('"').ToList()[DisplayedNameIndex]));
                }
                else if (!DefaultCase[0].ToString().Contains("<i:Info_Log"))
                {
                    messageList.Add(String.Format("The following workflow '{0}' has a FlowSwitch '{1}' that doeasn't have Log Activity inside its Default branch.", CurrentWorkflow.DisplayName, SwitchMatch.ToString().Split('"').ToList()[DisplayedNameIndex]));

                }
                //exclude default case lines ans start countinf cases after it >> SwitchMatch.ToString().Split(DefaultCase[0].ToString().ToCharArray())[1]
                MatchCollection CaseNum = Regex.Matches(SwitchMatch.ToString(), @"(<FlowStep x:Key((.\n*)(<\/FlowStep>)?)*)");


                if (CaseNum.Count == 0)
                {
                    messageList.Add(String.Format("The following FlowSwitch '{0}' has an No Cases branches other than the default one", SwitchMatch.ToString().Split('"').ToList()[DisplayedNameIndex]));
                }
                else if (CaseNum.Count == 1)
                {
                    //ensure that regex is working fine 
                    var Cases = CaseNum[0].Groups[1].ToString().Split(new[] { "<FlowStep x:Key" }, StringSplitOptions.None).ToList();
                    Cases.RemoveAll(s => s.Trim() == "");
                    foreach (var Scase in Cases)
                    {
                        if (!Scase.ToString().Contains("<i:Info_Log"))
                        {
                            messageList.Add(String.Format("The following workflow '{0}' has a FlowSwitch '{1}' that doeasn't have Log Activity inside one/more of its branches", CurrentWorkflow.DisplayName, SwitchMatch.ToString().Split('"').ToList()[DisplayedNameIndex]));
                        }
                    }
                }
                else
                {
                    foreach (var Scase in CaseNum)
                    {
                        if (!Scase.ToString().Contains("<i:Info_Log"))
                        {
                            messageList.Add(String.Format("The following workflow '{0}' has a FlowSwitch '{1}' that doeasn't have Log Activity inside one/more of its branches", CurrentWorkflow.DisplayName, SwitchMatch.ToString().Split('"').ToList()[DisplayedNameIndex]));
                            
                        }
                    }
                }
                



                /*   do
                   {
                       MatchCollection CaseNum = Regex.Matches(SwitchMatch.ToString(), @"(<FlowStep x:Key="" + incrementer.ToString() + "" (.\n*)*)(?=(<FlowStep x:Key="" + (incrementer + 1).ToString() + ""))");
                       if(CaseNum.Count == 0)
                       {
                           MatchCollection LastCase = Regex.Matches(SwitchMatch.ToString(), @"(<FlowStep x:Key=""+ incrementer.ToString() +"" (.\n*)*)(<\/FlowSwitch>)");
                           if (LastCase.Count == 0 || !LastCase[0].ToString().Contains("<i:Info_Log"))
                           {
                               messageList.Add(String.Format("The following FlowSwitch '{0}' doeasn't have Log Activity inside its branch number {1}.",  SwitchMatch.ToString().Split('"').ToList()[DisplayedNameIndex], incrementer.ToString()));
                           }
                           break;
                       }
                       else if (!CaseNum[0].ToString().Contains("<i:Info_Log"))
                       {
                           messageList.Add(String.Format("The following workflow '{0}' has a FlowSwitch '{1}' that doeasn't have Log Activity inside its branch number {2}.", CurrentWorkflow.DisplayName, SwitchMatch.ToString().Split('"').ToList()[DisplayedNameIndex],incrementer.ToString()));
                           incrementer++;  
                       }
                   } while (incrementer<=11);
                */
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
    }
}
