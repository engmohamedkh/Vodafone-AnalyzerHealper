using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UiPath.Studio.Activities.Api;
using UiPath.Studio.Activities.Api.Analyzer;
using UiPath.Studio.Activities.Api.Analyzer.Rules;
using UiPath.Studio.Analyzer.Models;

namespace WorkqueueEncryptionSpace
{
    public class WorkqueueEncryption : IRegisterAnalyzerConfiguration
    {

        // Configs
        string RuleID = "VF-024";
        string RuleName = "WorkqueueEncryption";
        string WFAnalyzerVersion = "WorkflowAnalyzerV4";
        string CommentOutActivityName = "decrypttext;encrypttext";

        // init the rule to the studio
        public void Initialize(IAnalyzerConfigurationService workflowAnalyzerConfigService)
        {
            // checking the wf version
            if (!workflowAnalyzerConfigService.HasFeature(WFAnalyzerVersion))
                return;

            var newRule = new Rule<IActivityModel>(RuleName, RuleID, CommentOutImplementation)
            {
                /// Off and Verbose are not supported.
                ErrorLevel = System.Diagnostics.TraceLevel.Info,
                RecommendationMessage = "Checking all invoke activities that use Warkqueue Encryption."
            };

            workflowAnalyzerConfigService.AddRule<IActivityModel>(newRule);
        }

        // Rule implementation
        private InspectionResult CommentOutImplementation(IActivityModel activity, Rule theNewRule)
        {
            var messageList = new List<string>();



            // return Error if existed
            if (activity.ToolboxName.ToLower().Contains(CommentOutActivityName.Split(';')[0]) || activity.ToolboxName.ToLower().Contains(CommentOutActivityName.Split(';')[1]))
            {
                messageList.Add(string.Format("The following activity {0} is encryption or decryption activity.", activity.DisplayName));

                return new InspectionResult()
                {
                    HasErrors = true,
                    Messages = messageList,
                    RecommendationMessage = "List of all WQ Encryption Activities.",
                    ErrorLevel = theNewRule.DefaultErrorLevel
                };
            };

            return new InspectionResult()
            {
                HasErrors = false
            };
        }
    }
}