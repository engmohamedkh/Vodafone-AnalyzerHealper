using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UiPath.Studio.Activities.Api;
using UiPath.Studio.Activities.Api.Analyzer;
using UiPath.Studio.Activities.Api.Analyzer.Rules;
using UiPath.Studio.Analyzer.Models;


namespace AddLogFieldsSpace
{
    public class AddLogFields : IRegisterAnalyzerConfiguration
    {

        // Configs
        string RuleID = "VF-030";
        string RuleName = "AddLogFields";
        string WFAnalyzerVersion = "WorkflowAnalyzerV4";
        string AddLogFieldsArguments = "VOISLM,projectName,processIdentifier,processStage,function,PPMID";
        string StatusLogFieldsArguments = "executionTime,Case Duration,exceptionMessage,ItemEndTime,BE_Category,TransactionStatus";
        int LogActivitiesCounter = 0;
        int FieldsCounter = 0;
        List<string> TempList = new List<string>();

        // init the rule to the studio
        public void Initialize(IAnalyzerConfigurationService workflowAnalyzerConfigService)
        {
            // checking the wf version
            if (!workflowAnalyzerConfigService.HasFeature(WFAnalyzerVersion))
                return;
            var newRule = new Rule<IWorkflowModel>(RuleName, RuleID, AddLogFieldsRole)
            {
                /// Off and Verbose are not supported.
                ErrorLevel = System.Diagnostics.TraceLevel.Error,
                RecommendationMessage = "Checking IAP Add log fileds activity at the beginning of worker or loader with the correct arguments."
            };

            workflowAnalyzerConfigService.AddRule<IWorkflowModel>(newRule);
        }

        //Recursive function
        private void CheckAddLogFields(IActivityModel ActivityToSearchIn, IWorkflowModel currWorkflow)
        {
            if (ActivityToSearchIn.Children.Count > 0)
            {
                foreach (var chi in ActivityToSearchIn.Children.ToList())
                {
                    CheckAddLogFields(chi, currWorkflow);
                }
            }

            else if (ActivityToSearchIn.ToolboxName.Contains("AddLogFields"))
            {
                // enhance: sign == changed to >= just in case some one needs to add new fields
                LogActivitiesCounter++;
                if ((!(ActivityToSearchIn.Arguments.Count == 0)) && ActivityToSearchIn.Arguments.Count >= StatusLogFieldsArguments.Split(',').ToList().Count)
                {
                    //bool ArgumentFlag = false;
                    foreach (var argument in ActivityToSearchIn.Arguments)
                    {

                        // messageList.Add(string.Format("{0}", argument.DisplayName));
                        if (StatusLogFieldsArguments.Contains(argument.DisplayName))
                        {
                            FieldsCounter++;
                            if (string.IsNullOrEmpty(argument.DefinedExpression))
                                TempList.Add(string.Format("The following workflow: {0} has '{1}' Activity that has empty mandatory argument '{2}'.", currWorkflow.DisplayName, ActivityToSearchIn.DisplayName, argument.DisplayName));
                            if (argument.DisplayName.Contains("TransactionStatus") && !argument.DefinedExpression.Contains("strAnalyticsMessage"))
                                TempList.Add(string.Format("The settransaction status workflow: have empty/wrong TransactionStatus value"));
                            if (argument.DisplayName.Contains("BE_Category") && !argument.DefinedExpression.Contains("BE_Category"))
                                TempList.Add(string.Format("The settransaction status workflow: have empty/wrong BE_Category value"));
                            if (argument.DisplayName.Equals("ItemEndTime") && !argument.DefinedExpression.Contains("MMM d,yyyy @ HH:mm:ss"))
                                TempList.Add(string.Format("The following workflow: {0} has '{1}' Activity that has empty/wrong mandatory argument '{2}'.", currWorkflow.DisplayName, ActivityToSearchIn.DisplayName, argument.DisplayName));
                            if (argument.DisplayName.Equals("executionTime") && !argument.Type.Contains("Double"))
                                TempList.Add(string.Format("The following workflow: {0} has '{1}' Activity that has Wrong argument Type '{2}'.", currWorkflow.DisplayName, ActivityToSearchIn.DisplayName, argument.DisplayName));


                            else if ((argument.DisplayName.Equals("exceptionMessage") || argument.DisplayName.Equals("Case Duration")) && !argument.Type.Contains("String"))
                                TempList.Add(string.Format("The following workflow: {0} has '{1}' Activity that has Wrong argument Type '{2}'.", currWorkflow.DisplayName, ActivityToSearchIn.DisplayName, argument.DisplayName));


                        }
                        /*else
                        {
                            ArgumentFlag = true;
                            break;
                        }*/
                    }
                    if (FieldsCounter < 3)
                    {
                        TempList.Add(string.Format("The following Workflow {0}: has '{1}' activity that is missing one or more of the Add log fields Arguments, please refer to the latest framework template", currWorkflow.DisplayName, ActivityToSearchIn.DisplayName));

                    }
                    /*if (ArgumentFlag)
                        TempList.Add(string.Format("The following workflow: {0} has '{1}' Activity that does not have the mandatory arguments '{2}'.", currWorkflow.DisplayName,ActivityToSearchIn.DisplayName, StatusLogFieldsArguments.Replace(",", "/")));
                */

                }
                else
                    TempList.Add(string.Format("The following workflow: {0} has '{1}' Activity that has empty/missing mandatory arguments '{2}'.", currWorkflow.DisplayName, ActivityToSearchIn.DisplayName, StatusLogFieldsArguments.Replace(",", "/")));


            }



        }


        // Implemetation
        private InspectionResult AddLogFieldsRole(IWorkflowModel CurrentWorkflow, Rule theNewRule)
        {

            List<string> messageList = new List<string>();

            if (CurrentWorkflow.RelativePath.ToLower().Split('_').Last().Equals("worker.xaml") || CurrentWorkflow.RelativePath.ToLower().Split('_').Last().Equals("loader.xaml") || CurrentWorkflow.RelativePath.ToLower().Split('_').Last().Equals("loaderworker.xaml"))
            {

                // no sequence or workflow added
                if (CurrentWorkflow.Root == null)
                {
                    // No Error message existed return Success
                    messageList.Add(string.Format("The following workflow: {0} is empty.", CurrentWorkflow.DisplayName));
                }
                // For any WF type (Sequence or Flowchart)
                else if (CurrentWorkflow.Root.ToolboxName.Contains("TryCatch"))
                {

                    // no activities yet
                    if (CurrentWorkflow.Root.Children.ToList().Count == 0)
                    {
                        messageList.Add(string.Format("The following workflow: {0} has empty try catch.", CurrentWorkflow.DisplayName));
                    }
                    else
                    {


                        var childs = CurrentWorkflow.Root.Children.ToList();
                        //Margo: Not needed to check on this counr since we are already in the else of being == 0
                        if (childs.Count > 0)
                        {

                            // Start Log message
                            if (((childs[0].ToolboxName.ToLower().Contains("sequence"))))
                            {
                                //Margo: i knew that its try sequcne because of line 65 Root Check?
                                var trySequence = childs[0];
                                if (trySequence.Children.Count == 0)
                                {
                                    messageList.Add(string.Format("The following workflow: {0} has empty sequence in the try catch.", CurrentWorkflow.DisplayName));
                                }
                                else
                                {
                                    //Margo: noo need for another if since im on the else branch already
                                    if (!(trySequence.Children.Count == 0))
                                    {
                                        bool AddLogFieldExistCheck = true;

                                        foreach (var child in trySequence.Children)
                                        {

                                            /////////////////////////////////////////////////////////////////
                                            if (child.ToolboxName.Contains("StateMachine"))
                                            {
                                                foreach (var ChildinStateMachine in child.Children)
                                                {
                                                    // Try Catch - Get Transaction
                                                    if (ChildinStateMachine.DisplayName.Contains("Try Catch - Get Transaction"))
                                                    {
                                                        foreach (var ChildinTryCatch in ChildinStateMachine.Children)
                                                        {
                                                            if (ChildinTryCatch.DisplayName.Contains("Get Transaction"))
                                                            {

                                                                foreach (var ChildinGetTrans in ChildinTryCatch.Children)
                                                                {
                                                                    if (ChildinGetTrans.ToolboxName.Contains("If"))
                                                                    {
                                                                        foreach (var ChildinIf in ChildinGetTrans.Children)
                                                                        {
                                                                            if (ChildinIf.ToolboxName.Contains("Sequence"))
                                                                            {
                                                                                foreach (var ChildinSequence in ChildinIf.Children)
                                                                                {
                                                                                    if (ChildinSequence.ToolboxName.Contains("AddLogFields"))
                                                                                    {
                                                                                        foreach (var argument in ChildinSequence.Arguments)
                                                                                        {
                                                                                            if (argument.DisplayName.Contains("TransactionKey") && !argument.DefinedExpression.Contains("qitransactionItem.ItemKey.ToString"))
                                                                                            {
                                                                                                messageList.Add(string.Format("The following workflow: Get Transaction has empty/wrong mandatory argument value: {0} ", argument.DisplayName));
                                                                                            }
                                                                                        }
                                                                                        if (ChildinSequence.Arguments.ToList().Count < 3)
                                                                                        {
                                                                                            messageList.Add(string.Format("The following workflow: Add Transaction Fields activity in Get Transaction have missing arguments"));
                                                                                        }
                                                                                    }

                                                                                }
                                                                            }


                                                                        }
                                                                    }
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                            /////////////////////////////////////////////////////////////////
                                            if (child.ToolboxName.Contains("AddLogFields"))
                                            {

                                                AddLogFieldExistCheck = false;
                                                if ((!(child.Arguments.Count == 0)) && child.Arguments.Count == AddLogFieldsArguments.Split(',').ToList().Count)
                                                {
                                                    bool ArgumentFlag = false;
                                                    foreach (var argument in child.Arguments)
                                                    {

                                                        // messageList.Add(string.Format("{0}", argument.DisplayName));
                                                        if (AddLogFieldsArguments.Contains(argument.DisplayName))
                                                        {
                                                            if (string.IsNullOrEmpty(argument.DefinedExpression))
                                                                messageList.Add(string.Format("The following workflow: {0} has empty mandatory argument {1}.", CurrentWorkflow.DisplayName, argument.DisplayName));
                                                            if (CurrentWorkflow.RelativePath.ToLower().Split('_').Last().Equals("worker.xaml"))
                                                            {
                                                                if (argument.DisplayName.Contains("processStage") && !argument.DefinedExpression.Contains("Worker"))
                                                                    messageList.Add(string.Format("The Main workflow: have empty/wrong processStage value"));
                                                            }
                                                            if (!argument.Type.Contains("String"))
                                                                messageList.Add(string.Format("The following workflow: {0} has Wrong argument Type {1}.", CurrentWorkflow.DisplayName, argument.DisplayName));
                                                        }
                                                        else
                                                        {
                                                            ArgumentFlag = true;
                                                            break;

                                                        }

                                                    }

                                                    if (ArgumentFlag)
                                                    {
                                                        messageList.Add(string.Format("The following workflow: {0} does not have the mandatory add log fields arguments {1}.", CurrentWorkflow.DisplayName, AddLogFieldsArguments.Replace(",", "/")));


                                                    }

                                                }
                                                else
                                                {
                                                    messageList.Add(string.Format("The following workflow: {0} has empty/missing mandatory arguments in the add log fields activity {1}.", CurrentWorkflow.DisplayName, AddLogFieldsArguments.Replace(",", "/")));
                                                }
                                            }
                                        }
                                        if (AddLogFieldExistCheck)
                                        {
                                            messageList.Add(string.Format("The following workflow: {0} does not have the mandatory add log fields activity.", CurrentWorkflow.DisplayName));

                                        }
                                    }
                                    else
                                    {
                                        messageList.Add(string.Format("The following workflow: {0} has empty try sequence.", CurrentWorkflow.DisplayName));

                                    }
                                }
                            }
                        }

                    }

                }
                else if (CurrentWorkflow.Root.ToolboxName.Contains("Sequence"))
                {

                    // no activities yet
                    if (CurrentWorkflow.Root.Children.ToList().Count == 0)
                    {//Margo: This should mean that it has no TryCatch ( empty Sequence )
                        messageList.Add(string.Format("The following workflow: {0} has empty try catch.", CurrentWorkflow.DisplayName));
                    }
                    else
                    {
                        var childs = CurrentWorkflow.Root.Children.ToList();
                        if (childs.Count > 0)
                        {

                            // Start Log message
                            if (((childs[0].ToolboxName.ToLower().Contains("trycatch"))))
                            {

                                var trycatch = childs[0];
                                if (trycatch.Children.ToList().Count == 0)
                                {
                                    messageList.Add(string.Format("The following workflow: {0} has empty try catch.", CurrentWorkflow.DisplayName));
                                }
                                else
                                {


                                    var tryCatchChild = trycatch.Children.ToList();
                                    var trySequence = tryCatchChild[0];
                                    if (trySequence.Children.Count == 0)
                                    {
                                        messageList.Add(string.Format("The following workflow: {0} has empty sequence in the try catch.", CurrentWorkflow.DisplayName));
                                    }
                                    else
                                    {

                                        if (!(trySequence.Children.Count == 0))
                                        {

                                            foreach (var child in trySequence.Children)
                                            {

                                                if (child.ToolboxName.Contains("AddLogFields"))
                                                {

                                                    if ((!(child.Arguments.Count == 0)) && child.Arguments.Count == AddLogFieldsArguments.Split(',').ToList().Count)
                                                    {
                                                        bool ArgumentFlag = false;
                                                        foreach (var argument in child.Arguments)
                                                        {
                                                            // messageList.Add(string.Format("{0}", argument.DisplayName));
                                                            if (AddLogFieldsArguments.Contains(argument.DisplayName))
                                                            {
                                                                if (string.IsNullOrEmpty(argument.DefinedExpression))
                                                                    messageList.Add(string.Format("The following workflow: {0} has empty mandatory argument {1}.", CurrentWorkflow.DisplayName, argument.DisplayName));

                                                                //     if (argument.DisplayName.Contains("processStage") && !argument.DefinedExpression.Contains("Worker"))
                                                                //       messageList.Add(string.Format("The Main workflow: have empty/wrong processStage value"));

                                                                if (!argument.Type.Contains("String"))
                                                                    messageList.Add(string.Format("The following workflow: {0} has Wrong argument Type {1}.", CurrentWorkflow.DisplayName, argument.DisplayName));


                                                            }
                                                            else
                                                            {
                                                                ArgumentFlag = true;
                                                                break;

                                                            }



                                                        }

                                                        if (ArgumentFlag)
                                                        {
                                                            messageList.Add(string.Format("The following workflow: {0} does not have the mandatory add log fields arguments {1}.", CurrentWorkflow.DisplayName, AddLogFieldsArguments.Replace(",", "/")));


                                                        }

                                                    }
                                                    else
                                                    {
                                                        messageList.Add(string.Format("The following workflow: {0} has empty/missing mandatory arguments in the add log fields activity {1}.", CurrentWorkflow.DisplayName, AddLogFieldsArguments.Replace(",", "/")));
                                                    }
                                                }

                                            }
                                        }
                                        else
                                        {
                                            messageList.Add(string.Format("The following workflow: {0} has empty try sequence.", CurrentWorkflow.DisplayName));

                                        }
                                    }

                                }
                            }
                            else
                            {
                                messageList.Add(string.Format("The following workflow: {0} does not have try catch.", CurrentWorkflow.DisplayName));

                            }
                        }

                    }

                }

            }

            else if (CurrentWorkflow.RelativePath.ToLower().Split('_').Last().ToLower().Equals("settransactionstatus.xaml"))
            {
                // no sequence or workflow added
             
                if (CurrentWorkflow.Root == null)
                {
                    // No Error message existed return Success
                    messageList.Add(string.Format("The following workflow: {0} is empty.", CurrentWorkflow.DisplayName));
                }
                foreach (var child in CurrentWorkflow.Root.Children.ToList())
                {
                    CheckAddLogFields(child, CurrentWorkflow);
                }
                if (TempList.Count > 0)
                {
                    messageList.AddRange(TempList);
                }
                if (LogActivitiesCounter < 3)
                {
                    messageList.Add(string.Format("The following workflow: {0} has only {1} out of 3 add log fields activities", CurrentWorkflow.DisplayName, LogActivitiesCounter.ToString()));
                }


            }
            TempList.Clear();
            LogActivitiesCounter = 0;
            FieldsCounter = 0;
            // errors existed
            if (messageList.Count > 0)
            {
                return new InspectionResult()
                {
                    HasErrors = true,
                    Messages = messageList,
                    RecommendationMessage = "Please add the mandatory add log fields activity with the mandatory arguments",
                    ErrorLevel = theNewRule.DefaultErrorLevel
                };

            }


            // No Error message existed return Success
            return new InspectionResult()
            {
                HasErrors = false
            };
        }
    }
}