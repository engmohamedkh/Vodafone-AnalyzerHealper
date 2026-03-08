using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UiPath.Studio.Activities.Api;
using UiPath.Studio.Activities.Api.Analyzer;
using UiPath.Studio.Activities.Api.Analyzer.Rules;
using UiPath.Studio.Analyzer.Models;

namespace ArgsNamingConventionSpace
{
    public class ArgNamingConvention : IRegisterAnalyzerConfiguration
    {
        // Configs
        string RuleID = "VF-051";
        string RuleName = "Args Naming Convention";
        string WFAnalyzerVersion = "WorkflowAnalyzerV4";
        // init the rule to the studio
        public void Initialize(IAnalyzerConfigurationService workflowAnalyzerConfigService)
        {
            // checking the wf version
            if (!workflowAnalyzerConfigService.HasFeature(WFAnalyzerVersion))
                return;

            var newRule = new Rule<IWorkflowModel>(RuleName, RuleID, ArgsNamingConventionRule)
            {
                /// Off and Verbose are not supported.
                ErrorLevel = System.Diagnostics.TraceLevel.Error,
                RecommendationMessage = "Checking Arguments Naming Convention"
            };

            workflowAnalyzerConfigService.AddRule<IWorkflowModel>(newRule);
        }

        // Implemetation
        private InspectionResult ArgsNamingConventionRule(IWorkflowModel CurrentWorkflow, Rule theNewRule)
        {
            var messageList = new List<string>();
            //string[] DataTypesPrefix = "str,int,dt,bool,tsp,sstr,dct".Split(',');
            IDictionary<string, string> DataTypesPrefix = new Dictionary<string, string>();

            //adding a key/value using the Add() method
            DataTypesPrefix.Add("DateTime", "dtm");
            DataTypesPrefix.Add("Searchoption", "sop");
            DataTypesPrefix.Add("IEnumerable", "ie");
            DataTypesPrefix.Add("NetworkCredential", "crd");
            DataTypesPrefix.Add("List", "lst");
            DataTypesPrefix.Add("Double", "dbl");
            DataTypesPrefix.Add("Decimal", "dec");
            DataTypesPrefix.Add("QueueItem", "qi");
            DataTypesPrefix.Add("JObject", "jobj");
            DataTypesPrefix.Add("Object", "obj");
            DataTypesPrefix.Add("DatabaseConnection", "db");
            DataTypesPrefix.Add("UiElement", "ui");
            DataTypesPrefix.Add("Array", "arr");
            DataTypesPrefix.Add("DataView", "dv");
            DataTypesPrefix.Add("Regex", "rgx");
            DataTypesPrefix.Add("Int32", "int");
            DataTypesPrefix.Add("Boolean", "bool");
            DataTypesPrefix.Add("DataTable", "dt");
            DataTypesPrefix.Add("Credential", "crd");
            DataTypesPrefix.Add("TimeSpan", "tsp");
            DataTypesPrefix.Add("SecureString", "sstr");
            DataTypesPrefix.Add("Dictionary", "dct");
            DataTypesPrefix.Add("String", "str");
            DataTypesPrefix.Add("GenericValue", "gen");
            DataTypesPrefix.Add("Browser", "brw");
            char[] SpecialChars = { ',', '\'', '.', '"', '?', '>', '<', ';', ':', '/', '\\', ';', '|', ']', '[', '}', '{', '=', '+', '-', '(', ')', '*', '&', '^', '%', '$', '#', '@', '!', '`', '~', ' ' };

            
            //Arguments Naming Convention
            foreach (var arg in CurrentWorkflow.Arguments)
            {
                String Direction = arg.DisplayName.Split('_').First();
                bool ValidPreFix = false;
                String NeededPrefix = DataTypesPrefix.Keys.Where(x => arg.Type.ToLower().Contains(x.ToLower())).FirstOrDefault();
                if (!string.IsNullOrEmpty(NeededPrefix))
                {
                    ValidPreFix = arg.DisplayName.Replace(Direction + "_", "").StartsWith(DataTypesPrefix[NeededPrefix]);
                }

                bool ValidDirection = Direction.Trim().Equals(arg.Direction.ToString().ToLower());
                bool SpecialCharsExists = SpecialChars.Any(x => arg.DisplayName.Contains(x));
                if (!ValidPreFix || !ValidDirection || SpecialCharsExists)
                    messageList.Add(string.Format("The following Argument: '{0}' doesn't match the Needed Naming Convention 'direction_argtypeArgName' taking in consideration the case senstivity. ", arg.DisplayName) + 
                        String.Format("[Direction Validity: {0} ; Prefix Validity: {1} ; Special Chars: {2}].", ValidDirection, ValidPreFix, SpecialCharsExists));

            }

            // errors existed
            if (messageList.Count > 0)
            {
                return new InspectionResult()
                {
                    HasErrors = true,
                    Messages = messageList,
                    RecommendationMessage = "Please use the following Naming Convention without any Special Charachters: " + 
                    "'direction_argtypeArgName' for Arguments taking in consideration the case senstivity,", 
                    ErrorLevel = theNewRule.DefaultErrorLevel
                };
            }
            else
            {
                // No Error message existed return Success
                return new InspectionResult()
                {
                    HasErrors = false
                };
            }
        }
    }
}

