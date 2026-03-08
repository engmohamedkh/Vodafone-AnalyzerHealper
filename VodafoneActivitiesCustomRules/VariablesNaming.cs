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

namespace VariablesNamingSpace
{
    public class VariablesNaming : IRegisterAnalyzerConfiguration
    {
        string RuleID = "VF-035";
        string RuleName = "VariablesNaming";
        string WFAnalyzerVersion = "WorkflowAnalyzerV4";
        public void Initialize(IAnalyzerConfigurationService workflowAnalyzerConfigService)
        {
            if (!workflowAnalyzerConfigService.HasFeature(WFAnalyzerVersion))
                return;
            var forbiddenStringRule = new Rule<IActivityModel>(RuleName, RuleID, InspectVariable);
            forbiddenStringRule.DefaultErrorLevel = System.Diagnostics.TraceLevel.Warning;
            forbiddenStringRule.RecommendationMessage = "Checking Variables Naming Convention.";
            workflowAnalyzerConfigService.AddRule<IActivityModel>(forbiddenStringRule);
        }

        private InspectionResult InspectVariable(IActivityModel ActivityToInspect, Rule ConfiguredRule)
        {
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
            char[] SpecialChars = { ',', '\'', '.', '"', '?', '>', '<', ';', ':', '/', '\\', ';', '|', ']', '[', '}', '{', '=', '+', '-', '(', ')', '*', '&', '^', '%', '$', '#', '@', '!', '`', '~', ' ' };

            var messageList = new List<string>();
            //variables Naming Convention
            foreach (var variable in ActivityToInspect.Variables)
            {
                bool ValidPreFix = false;
                String NeededPrefix = DataTypesPrefix.Keys.Where(x => variable.Type.ToLower().Contains(x.ToLower())).FirstOrDefault();
                if (!string.IsNullOrEmpty(NeededPrefix))
                {
                    ValidPreFix = variable.DisplayName.StartsWith(DataTypesPrefix[NeededPrefix]);
                }
                //bool ValidPreFix = DataTypesPrefix.Any(x => variable.DisplayName.StartsWith(x.ToLower()));
                bool SpecialCharsExists = SpecialChars.Any(x => variable.DisplayName.Contains(x));
                if (!ValidPreFix || SpecialCharsExists)
                    messageList.Add(string.Format("The following variable: '{0}' doesn't match the Needed Naming Convention 'argtypeArgName' taking in consideration the case senstivity. ", variable.DisplayName)+
                        String.Format("[Prefix Validity: {0} ; Special Chars: {1}.]", ValidPreFix, SpecialCharsExists) );

            }


            if (messageList.Count > 0)
            {
                return new InspectionResult()
                {
                    HasErrors = true,
                    Messages = messageList,
                    RecommendationMessage = "Please use the following Naming Convention without any Special Charachters: " +
                    "'argtypeArgName' for Variables taking in consideration the case senstivity.",
                    ErrorLevel = ConfiguredRule.ErrorLevel
                };
            }
            return new InspectionResult() { HasErrors = false };

        }
    }

}

