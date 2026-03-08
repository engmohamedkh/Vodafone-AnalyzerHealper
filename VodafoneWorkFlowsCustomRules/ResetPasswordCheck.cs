using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UiPath.Studio.Activities.Api;
using UiPath.Studio.Activities.Api.Analyzer;
using UiPath.Studio.Activities.Api.Analyzer.Rules;
using UiPath.Studio.Analyzer.Models;

namespace ResetPasswordCheckSpace
{

    public class LogicFileUICheck : IRegisterAnalyzerConfiguration
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
        string RuleID = "VF-052";
        string RuleName = "ResetPasswordCheckSpace";
        string WFAnalyzerVersion = "WorkflowAnalyzerV4";


        //Rule init
        public void Initialize(IAnalyzerConfigurationService workflowAnalyzerConfigService)
        {
            if (!workflowAnalyzerConfigService.HasFeature(WFAnalyzerVersion))
                return;

            var newRule = new Rule<IWorkflowModel>(RuleName, RuleID, ResetPasswordCheckImplemntation)
            {
                /// Off and Verbose are not supported.
                ErrorLevel = System.Diagnostics.TraceLevel.Warning,
                RecommendationMessage = "Checking the Reset password is used or not"
            };

            workflowAnalyzerConfigService.AddRule<IWorkflowModel>(newRule);
        }


        // Rule implementation
        private InspectionResult ResetPasswordCheckImplemntation(IWorkflowModel CurrentWorkflow, Rule theNewRule)
        {
            var messageList = new List<string>();
            try
            {
                if (CurrentWorkflow.DisplayName.ToLower().Contains("initialiseapplications"))
                {
                    
                    //Read current work flow as a text file

                    var XamlFileLines = File.ReadAllText(CurrentWorkflow.Project.Directory + "\\" + CurrentWorkflow.RelativePath);

                    // string[] XamlFileLines = File.ReadAllLines(CurrentWorkflow.RelativePath);
                    
                    if (!(XamlFileLines.ToLower().Contains("iap_validateapplicationscredentials") && XamlFileLines.ToLower().Contains("reset")))
                    {

                        messageList.Add(string.Format("The following workflow: {0} does not have reset password", CurrentWorkflow.DisplayName));


                    }

                }


                // No Error message existed return Success
                if (!IsEmpty(messageList))
                {
                    // Error message existed return the error
                    return new InspectionResult()
                    {
                        HasErrors = true,
                        Messages = messageList,
                        RecommendationMessage = "",
                        ErrorLevel = theNewRule.DefaultErrorLevel
                    };
                }

                return new InspectionResult()
                {
                    HasErrors = false
                };
            }
            catch (Exception ex)
            {
                if (!IsEmpty(messageList))
                {
                    // Error message existed return the error
                    return new InspectionResult()
                    {
                        HasErrors = true,
                        Messages = messageList,
                        RecommendationMessage = "Please ensure that Reset Password Mechanism is being used",
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
}
