using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UiPath.Studio.Activities.Api;
using UiPath.Studio.Activities.Api.Analyzer;
using UiPath.Studio.Activities.Api.Analyzer.Rules;
using UiPath.Studio.Analyzer.Models;
using ProhibtedActivitySpace;
using UndocumentedDelaySpace;
using ImageBasedActivitiesSpace;
using SimulateAndSendWindowMessageSpace;
using BusinessSystemExceptionSpace;
using ArchetypeLogsSpace;
using MissingInOutArgumentSpace;
using WorkqueueEncryptionSpace;
using HardCodedPasswordsSpace;
using LogBrowserURLSpace;
using CommentOutSpace;
using IfElseSpace;
using VariablesNamingSpace;
using BranchesLogSpace;
using SelectorAttributeSpace;
using SelectorAttributeValueSpace;

namespace VodafoneActivitiesCustomRules
{
    public class VodafoneActivitiesCustomRules
    {
        // use the lib
        ProhibtedActivity ProhibtedActivityNew = new ProhibtedActivity();
        SelectorAttribute SelectorAttributeNew = new SelectorAttribute();
        SelectorAttributeValue SelectorAttributeValueNew = new SelectorAttributeValue();
        ImageBasedActivities ImageBasedActivitiesNew  = new ImageBasedActivities();
        UndocumentedDelay UndocumentedDelayNew = new UndocumentedDelay();
        SimulateAndSendWindowMessage SimulateAndSendWindowMessageNew = new SimulateAndSendWindowMessage();
        BusinessSystemException BusinessSystemExceptionNew = new BusinessSystemException();
        ArchetypeLogs ArchetypeLogsNew = new ArchetypeLogs();
        MissingInOutArgument MissingInOutArgumentNew = new MissingInOutArgument();
        WorkqueueEncryption WorkqueueEncryptionNew = new WorkqueueEncryption();
        HardCodedPasswords HardCodedPasswordsNew = new HardCodedPasswords();
        LogBrowserURL LogBrowserURLNew = new LogBrowserURL();
        CommentOut CommentOutNew = new CommentOut();
        IfElse IfElseNew = new IfElse();
        VariablesNaming VarsName = new VariablesNaming();
        BranchesLogs BranchingLogObj = new BranchesLogs();

        public void Initialize(IAnalyzerConfigurationService workflowAnalyzerConfigService)
        {
            ProhibtedActivityNew.Initialize(workflowAnalyzerConfigService);
            UndocumentedDelayNew.Initialize(workflowAnalyzerConfigService);
            ImageBasedActivitiesNew.Initialize(workflowAnalyzerConfigService);
            SimulateAndSendWindowMessageNew.Initialize(workflowAnalyzerConfigService);
            BusinessSystemExceptionNew.Initialize(workflowAnalyzerConfigService);
            ArchetypeLogsNew.Initialize(workflowAnalyzerConfigService);
            MissingInOutArgumentNew.Initialize(workflowAnalyzerConfigService);
            WorkqueueEncryptionNew.Initialize(workflowAnalyzerConfigService);
            HardCodedPasswordsNew.Initialize(workflowAnalyzerConfigService);
            LogBrowserURLNew.Initialize(workflowAnalyzerConfigService);
            CommentOutNew.Initialize(workflowAnalyzerConfigService);
            IfElseNew.Initialize(workflowAnalyzerConfigService);
            VarsName.Initialize(workflowAnalyzerConfigService);
            BranchingLogObj.Initialize(workflowAnalyzerConfigService);
        SelectorAttributeNew.Initialize(workflowAnalyzerConfigService);
        SelectorAttributeValueNew.Initialize(workflowAnalyzerConfigService);
        }
    }
}
