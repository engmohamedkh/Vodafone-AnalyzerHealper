using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UiPath.Studio.Activities.Api;
using UiPath.Studio.Activities.Api.Analyzer;
using UiPath.Studio.Activities.Api.Analyzer.Rules;
using UiPath.Studio.Analyzer.Models;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NamingConventionSpace
{
    public class NamingConvention : IRegisterAnalyzerConfiguration
    {
        // Configs
        string RuleID = "VF-034";
        string RuleName = "Naming Convention";
        string WFAnalyzerVersion = "WorkflowAnalyzerV4";
        String ProcessIdentifierVarName = "strProcessIdentifier";
        String BusinessVerticals = "CFSL,CARE,Enterprise,HR,TES,Audit,Finance-Operations,Business,SCM,Human-Resources,HumanResources,RSA,Network-Operations,Technology-Enterprise";

        String LocalMarkets = "IT,DE,SPAIN,UK,VNO,Group,VOIS,AL,RO,CZ,ES,GR,SA,IE,VOIS-UK,VNO-UK,VNO-RO,ES-Mob,ES-WP,VSSI";
        // init the rule to the studio
        public void Initialize(IAnalyzerConfigurationService workflowAnalyzerConfigService)
        {
            // checking the wf version
            if (!workflowAnalyzerConfigService.HasFeature(WFAnalyzerVersion))
                return;

            var newRule = new Rule<IWorkflowModel>(RuleName, RuleID, NamingConventionRule)
            {
                /// Off and Verbose are not supported.
                ErrorLevel = System.Diagnostics.TraceLevel.Error,
                RecommendationMessage = "Checking Main Xaml Name, Process Identifier variable Naming Convention"
            };

            workflowAnalyzerConfigService.AddRule<IWorkflowModel>(newRule);
        }

        // Implemetation
        private InspectionResult NamingConventionRule(IWorkflowModel CurrentWorkflow, Rule theNewRule)
        {
            var messageList = new List<string>();
            
            IDictionary<string, string> DataTypesPrefix = new Dictionary<string, string>();
            //string[] DataTypesPrefix = "str,int,dt,bool,tsp,sstr,dct".Split(',');
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

            if (CurrentWorkflow.RelativePath.ToLower().Split('_').Last().Equals("worker.xaml") || CurrentWorkflow.RelativePath.ToLower().Split('_').Last().Equals("loader.xaml") || CurrentWorkflow.RelativePath.ToLower().Split('_').Last().Equals("loaderworker.xaml"))
            {
                string[] BusinessVerticalsList = BusinessVerticals.Split(',');
                string[] LocalMarketsList = LocalMarkets.Split(',');
                bool ValidPPMID = false;
                bool ValidBV = false;
                bool ValidLM = false;
                //Workflow Display Name
                var MainDisplayNameTokens = new List<string>();
                MainDisplayNameTokens = CurrentWorkflow.DisplayName.Split('_').ToList();
                if (MainDisplayNameTokens.Count >= 4)
                {
                    ValidPPMID = int.TryParse(MainDisplayNameTokens[0], out _) && MainDisplayNameTokens[0].Length == 6;
                    ValidBV = BusinessVerticalsList.Any(x => x.ToLower().Trim().Equals(MainDisplayNameTokens[1].ToString().ToLower().Trim()));
                    ValidLM = LocalMarketsList.Any(x => x.ToLower().Trim().Equals(MainDisplayNameTokens[2].ToString().ToLower().Trim()));
                    if (!ValidPPMID || !ValidBV || !ValidLM)
                        messageList.Add(string.Format("The following workflow Name: '{0}' doesn't match the Needed Structure 'PPMID_BusinessVertical_LocalMarket_ProcessName'. ", CurrentWorkflow.DisplayName) +
                            String.Format("[PPMID Validity: {0} ; BV Valididty: {1} ; LM Validity: {2}]", ValidPPMID, ValidBV, ValidLM));
                }
                else
                    messageList.Add(string.Format("The following workflow Name: '{0}' doesn't match the needed number of fields 'PPMID_BusinessVertical_LocalMarket_ProcessName'.", CurrentWorkflow.DisplayName));

                //------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
                //Process Identifier Name
                var ProcessIdentifierVar = CurrentWorkflow.Root.Variables.FirstOrDefault(x => x.DisplayName.ToLower().Equals(ProcessIdentifierVarName.ToLower()));
                if (ProcessIdentifierVar != null)
                {

                    if (String.IsNullOrEmpty(ProcessIdentifierVar.DefaultValue))
                    {
                        messageList.Add(string.Format("The following workflow: '{0}' has '{1}' variable but its default value is empty.", CurrentWorkflow.DisplayName, ProcessIdentifierVarName));
                    }
                    else
                    {
                        var ProcessIDentifierTokens = new List<string>();
                        ProcessIDentifierTokens = ProcessIdentifierVar.DefaultValue.Replace("\"", "").Split('_').ToList();
                        if (ProcessIDentifierTokens.Count >= 4)
                        {
                            ValidPPMID = int.TryParse(ProcessIDentifierTokens[0], out _) && ProcessIDentifierTokens[0].Length == 6;
                            ValidBV = BusinessVerticalsList.Any(x => x.ToLower().Trim().Equals(ProcessIDentifierTokens[1].ToString().ToLower().Trim()));
                            ValidLM = LocalMarketsList.Any(x => x.ToLower().Trim().Equals(ProcessIDentifierTokens[2].ToString().ToLower().Trim()));

                            if (!ValidPPMID || !ValidBV || !ValidLM)
                                messageList.Add(string.Format("The following workflow: '{0}' has '{1}' variable but its default value doesn't match the Needed Structure 'PPMID_BusinessVertical_LocalMarket_ProcessName'. ", CurrentWorkflow.DisplayName, ProcessIdentifierVarName) +
                                    String.Format("[PPMID Validity: {0} ; BV Valididty: {1} ; LM Validity: {2}]", ValidPPMID, ValidBV, ValidLM));

                        }
                        else
                            messageList.Add(string.Format("The following workflow: '{0}' has '{1}' variable but its default value doesn't match the Needed Number of fields 'PPMID_BusinessVertical_LocalMarket_ProcessName'.", CurrentWorkflow.DisplayName, ProcessIdentifierVarName));
                    }
                }
                else
                {
                    messageList.Add(string.Format("The following workflow: '{0}' Doesn't have {1} Variable", CurrentWorkflow.DisplayName, ProcessIdentifierVarName));
                }
            }



            //project.json

            if ((CurrentWorkflow.Project.Directory + "\\" + CurrentWorkflow.RelativePath).ToLower().Split('_').Last().Contains("worker.xaml") || (CurrentWorkflow.Project.Directory + "\\" + CurrentWorkflow.RelativePath).ToLower().Split('_').Last().Contains("loader.xaml"))
            {
                JObject JObj = JObject.Parse(File.ReadAllText(CurrentWorkflow.Project.Directory + "\\" + "project.json"));

                if (!(JObj.GetValue("name").ToString().Equals(JObj.GetValue("main").ToString().Replace(".xaml", ""))))
                {
                    messageList.Add("Name and Main don't have the same value");
                }
            }


                   

                    // errors existed
            if (messageList.Count > 0)
            {
                return new InspectionResult()
                {
                    HasErrors = true,
                    Messages = messageList,
                    RecommendationMessage = "Please use the following Naming Convention without any Special Charachters: " + 
                    "'PPMID_BusinessVertical_LocalMarket_ProcessName' For Main Wofkflows Name and Process Identifier value,",
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

