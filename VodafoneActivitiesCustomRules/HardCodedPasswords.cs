using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UiPath.Studio.Activities.Api;
using UiPath.Studio.Activities.Api.Analyzer;
using UiPath.Studio.Activities.Api.Analyzer.Rules;
using UiPath.Studio.Analyzer.Models;


namespace HardCodedPasswordsSpace
{
    public class HardCodedPasswords : IRegisterAnalyzerConfiguration
    {
        public void Initialize(IAnalyzerConfigurationService workflowAnalyzerConfigService)
        {
            var forbiddenStringRule = new Rule<IActivityModel>("HardCodedPasswords", "VF-005", InspectVariableForString);
            forbiddenStringRule.DefaultErrorLevel = System.Diagnostics.TraceLevel.Error;
            forbiddenStringRule.RecommendationMessage = "Check the SecureString variables if it has default value, Also checks Password/Secure Password property.";
            workflowAnalyzerConfigService.AddRule<IActivityModel>(forbiddenStringRule);
        }

        private InspectionResult InspectVariableForString(IActivityModel ActivityToInspect, Rule ConfiguredRule)
        {
            //&& x.Type.ToLower().Equals("string")
            var messageList = new List<string>();
            var PasswordArg = ActivityToInspect.Arguments.FirstOrDefault(x => x.DisplayName.ToLower().Contains("password") && x.Type.ToLower().Equals("string"));
            var SecurePWArg = ActivityToInspect.Arguments.FirstOrDefault(x => (x.DisplayName.ToLower().Contains("securepassword")|| x.DisplayName.ToLower().Contains("password")) && x.Type.ToLower().Equals("securestring"));
            var ActivityVariables = ActivityToInspect.Variables.ToList();
                        // check password property
            if (PasswordArg != null)
            {
                if (!string.IsNullOrEmpty(PasswordArg.DefinedExpression))
                {
                    messageList.Add(string.Format("The following activity {0} is using string password property. Please use the secure string instead.", ActivityToInspect.DisplayName));
                }
            }

            // check secured password property if it has any hard coded value
            if (SecurePWArg != null)
            {
                if (!string.IsNullOrEmpty(SecurePWArg.DefinedExpression))
                    if (SecurePWArg.DefinedExpression.Contains("\",\""))
                    {
                        messageList.Add(string.Format("The following activity {0} has hard coded secure password value.", ActivityToInspect.DisplayName));
                    }
            }

            //check hardcoded pass
            var HardcodedPass = ActivityVariables.Where(x => x.Type.ToLower().Contains("securestring") && !String.IsNullOrEmpty(x.DefaultValue)).ToList();
            if (HardcodedPass.Count > 0)
            {
                messageList.Add(string.Format("The following activity {0} has hard coded password variable.", ActivityToInspect.DisplayName));
            }


            if (messageList.Count > 0)
            {
                return new InspectionResult()
                {
                    HasErrors = true,
                    Messages = messageList,
                    RecommendationMessage = "Please don't use any hardcoded secured password. secure string variable or password property.",
                    ErrorLevel = ConfiguredRule.ErrorLevel
                };
            }
            return new InspectionResult() { HasErrors = false };

        }
    }

}

