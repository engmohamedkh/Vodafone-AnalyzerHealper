using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UiPath.Studio.Activities.Api;
using UiPath.Studio.Activities.Api.Analyzer;
using UiPath.Studio.Activities.Api.Analyzer.Rules;
using UiPath.Studio.Analyzer.Models;
using TryCatchCheck;
using ValidateWorkFlowsName;
using RetryScopeSpace;
//using StartEndLogsSpace;
//using StartEndAppMonitoringLogsSpace;
using HardcodedArgumentsSpace;
using AddLogFieldsSpace;
using AnnotationsSpace;
using LogicFileUICheckSpace;
using NamingConventionSpace;
using UnusedArgumentsSpace;
using ActivitiesNumberSpace;
using MultipleAppsSpace;
using ValidateInputsSpace;
using SetTransactionStatusOnlyInHandoffSpace;
using SelectorAttributeFromXamlSpace;
using SelectorAttributeValueFromXamlSpace;


namespace VodafoneWorkFlowsCustomRules
{
    public class VodafoneWorkFlowsCustomRules
    {
        ValidateWorkFlowName ValidateWorkFlowsNameNew = new ValidateWorkFlowName();
        TryCatchRule TryCatchRuleNew = new TryCatchRule();
        //StartEndLogs StartEndLogsNew = new StartEndLogs();
        RetryScope RetryScope = new RetryScope();
        //StartEndAppMonitoringLogs StartEndAppMonitoringLogsNew = new StartEndAppMonitoringLogs();
        HardcodedArguments HardcodedArgumentsNew = new HardcodedArguments();
        AddLogFields AddLogFiledsNew = new AddLogFields();
        Annotations  AnnotationsNew = new Annotations();
        LogicFileUICheck logicFileUICheckNew = new LogicFileUICheck();
        NamingConvention NamingConventionObj = new NamingConvention();
        UnusedArgumentsRule UnusedArgObj = new UnusedArgumentsRule();
        ActivitiesNumber ActivitiesNumObj = new ActivitiesNumber();
        MultipleAppsRule MultipleappsObj = new MultipleAppsRule();
        ValidateInput ValidateInput = new ValidateInput();
        SetTransactionStatusOnlyInHandoff SetTransactionStatusOnlyInHandoffNew = new SetTransactionStatusOnlyInHandoff();
        SelectorAttributeFromXaml SelectorAttributeFromXamlNew = new SelectorAttributeFromXaml();
        SelectorAttributeValueFromXaml SelectorAttributeValueFromXamlNew = new SelectorAttributeValueFromXaml();

        public void Initialize(IAnalyzerConfigurationService workflowAnalyzerConfigService)
        {
            ValidateWorkFlowsNameNew.Initialize(workflowAnalyzerConfigService);
            TryCatchRuleNew.Initialize(workflowAnalyzerConfigService);
            //StartEndLogsNew.Initialize(workflowAnalyzerConfigService);
            RetryScope.Initialize(workflowAnalyzerConfigService);
            //StartEndAppMonitoringLogsNew.Initialize(workflowAnalyzerConfigService);
            HardcodedArgumentsNew.Initialize(workflowAnalyzerConfigService);
            AddLogFiledsNew.Initialize(workflowAnalyzerConfigService);
            AnnotationsNew.Initialize(workflowAnalyzerConfigService);
            logicFileUICheckNew.Initialize(workflowAnalyzerConfigService);
            NamingConventionObj.Initialize(workflowAnalyzerConfigService);
            UnusedArgObj.Initialize(workflowAnalyzerConfigService);
            ActivitiesNumObj.Initialize(workflowAnalyzerConfigService);
            MultipleappsObj.Initialize(workflowAnalyzerConfigService);
            ValidateInput.Initialize(workflowAnalyzerConfigService);
            SetTransactionStatusOnlyInHandoffNew.Initialize(workflowAnalyzerConfigService);
            SelectorAttributeFromXamlNew.Initialize(workflowAnalyzerConfigService);
            SelectorAttributeValueFromXamlNew.Initialize(workflowAnalyzerConfigService);
        }
    }
}
