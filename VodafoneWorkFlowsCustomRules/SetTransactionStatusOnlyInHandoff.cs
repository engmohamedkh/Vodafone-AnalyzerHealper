using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UiPath.Studio.Activities.Api;
using UiPath.Studio.Activities.Api.Analyzer;
using UiPath.Studio.Activities.Api.Analyzer.Rules;
using UiPath.Studio.Analyzer.Models;

namespace SetTransactionStatusOnlyInHandoffSpace
{
    /// <summary>
    /// Per framework: only the sequence invoked from the Handoff state (settransactionstatus.xaml)
    /// may contain Set Transaction Status activity. If Set Transaction Status exists in any other
    /// workflow, report an error.
    /// Also: settransactionstatus.xaml must only be invoked from the Handoff section; if invoked
    /// from any other file or section, report an error and include the invoking path in the message.
    /// </summary>
    public class SetTransactionStatusOnlyInHandoff : IRegisterAnalyzerConfiguration
    {
        const string RuleID = "VF-058";
        const string RuleName = "prohibted  SetTransactionStatus";
        const string WFAnalyzerVersion = "WorkflowAnalyzerV4";

        static readonly XNamespace UiNs = XNamespace.Get("http://schemas.uipath.com/workflow/activities");
        static readonly XNamespace XNs = XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml");

        /// <summary>Activity name as in toolbox / display (Orchestrator Set Transaction Status).</summary>
        const string SetTransactionStatusActivityName = "SetTransactionStatus";

        /// <summary>Workflow file name that may only be invoked from Handoff section.</summary>
        const string SetTransactionStatusXamlName = "settransactionstatus.xaml";

        /// <summary>Allowed workflow: the one invoked from Handoff state in main state machine.</summary>
        private static bool IsSetTransactionStatusWorkflow(IWorkflowModel workflow)
        {
            if (workflow?.RelativePath == null)
                return false;
            return workflow.RelativePath.ToLower().Split('_').Last().ToLower().Equals(SetTransactionStatusXamlName);
        }

        /// <summary>Get full file path for the workflow.</summary>
        private static string GetWorkflowFilePath(IWorkflowModel workflow)
        {
            string dir = workflow?.Project?.Directory?.ToString()?.TrimEnd('\\', '/') ?? "";
            string rel = workflow?.RelativePath?.Replace('/', Path.DirectorySeparatorChar).TrimStart('\\', '/') ?? "";
            if (string.IsNullOrEmpty(dir)) return null;
            return Path.Combine(dir, rel);
        }

        /// <summary>Check if the given XElement is inside a Handoff state (State with DisplayName or x:Key containing "Handoff").</summary>
        private static bool IsInsideHandoffState(XElement element)
        {
            if (element == null) return false;
            XElement current = element.Parent;
            while (current != null)
            {
                if (string.Equals(current.Name.LocalName, "State", StringComparison.OrdinalIgnoreCase))
                {
                    string displayName = current.Attribute("DisplayName")?.Value ?? "";
                    var keyAttr = current.Attribute(XNs + "Key") ?? current.Attributes().FirstOrDefault(a => string.Equals(a.Name.LocalName, "Key", StringComparison.OrdinalIgnoreCase));
                    string key = keyAttr?.Value ?? "";
                    if (displayName.IndexOf("Handoff", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        key.IndexOf("Handoff", StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                    return false; // Inside a State but not Handoff
                }
                current = current.Parent;
            }
            return false;
        }

        /// <summary>Return the enclosing State's DisplayName or x:Key if available, else empty.</summary>
        private static string GetEnclosingStateName(XElement element)
        {
            if (element == null) return string.Empty;
            XElement current = element.Parent;
            while (current != null)
            {
                if (string.Equals(current.Name.LocalName, "State", StringComparison.OrdinalIgnoreCase))
                {
                    string displayName = current.Attribute("DisplayName")?.Value ?? "";
                    var keyAttr = current.Attribute(XNs + "Key") ?? current.Attributes().FirstOrDefault(a => string.Equals(a.Name.LocalName, "Key", StringComparison.OrdinalIgnoreCase));
                    string key = keyAttr?.Value ?? "";
                    string name = !string.IsNullOrWhiteSpace(displayName) ? displayName : key;
                    return name?.Trim() ?? string.Empty;
                }
                current = current.Parent;
            }
            return string.Empty;
        }

        /// <summary>
        /// Validates all invocations of settransactionstatus.xaml in a caller workflow.
        /// Allowed case only: invoke is inside Handoff state.
        /// Returns error messages for all invalid locations; otherwise empty list.
        /// </summary>
        private static List<string> GetInvalidSetTransactionStatusInvocationMessages(string xamlFilePath)
        {
            if (string.IsNullOrEmpty(xamlFilePath) || !File.Exists(xamlFilePath))
                return new List<string>();
            try
            {
                var messages = new List<string>();
                var doc = XDocument.Load(xamlFilePath);
                foreach (var invoke in doc.Descendants(UiNs + "InvokeWorkflowFile"))
                {
                    string wfFileName = invoke.Attribute("WorkflowFileName")?.Value;
                    if (string.IsNullOrWhiteSpace(wfFileName))
                    {
                        var args = invoke.Elements(UiNs + "InvokeWorkflowFile.Arguments").FirstOrDefault();
                        var argEl = args?.Elements().FirstOrDefault(e =>
                            string.Equals(e.Attributes().FirstOrDefault(a => a.Name.LocalName == "Key")?.Value, "WorkflowFileName", StringComparison.OrdinalIgnoreCase));
                        wfFileName = argEl?.Value?.Trim(' ', '"');
                    }
                    if (string.IsNullOrWhiteSpace(wfFileName)) continue;
                    bool targetsSetTransactionStatus = wfFileName.Trim().EndsWith(SetTransactionStatusXamlName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(Path.GetFileName(wfFileName.Trim()), SetTransactionStatusXamlName, StringComparison.OrdinalIgnoreCase);
                    if (!targetsSetTransactionStatus) continue;
                    if (!IsInsideHandoffState(invoke))
                    {
                        string stateName = GetEnclosingStateName(invoke);
                        string inState = string.IsNullOrWhiteSpace(stateName) ? "unknown" : stateName;
                        messages.Add(string.Format(
                            "settransactionstatus.xaml is invoked outside the Handoff state (state: {0}). Move it under the Handoff state section of the state machine.",
                            inState));
                    }
                }
                return messages;
            }
            catch { return new List<string>(); }
        }

        /// <summary>Recursively checks if any activity in the tree is Set Transaction Status.</summary>
        private static bool ContainsSetTransactionStatusActivity(IActivityModel activity)
        {
            if (activity == null)
                return false;
            if (activity.ToolboxName != null &&
                activity.ToolboxName.IndexOf(SetTransactionStatusActivityName, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (activity.DisplayName != null &&
                activity.DisplayName.IndexOf("Set Transaction Status", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (activity.Children != null)
            {
                foreach (var child in activity.Children)
                {
                    if (ContainsSetTransactionStatusActivity(child))
                        return true;
                }
            }
            return false;
        }

        public void Initialize(IAnalyzerConfigurationService workflowAnalyzerConfigService)
        {
            if (!workflowAnalyzerConfigService.HasFeature(WFAnalyzerVersion))
                return;

            var newRule = new Rule<IWorkflowModel>(RuleName, RuleID, SetTransactionStatusOnlyInHandoffRole)
            {
                ErrorLevel = System.Diagnostics.TraceLevel.Error,
                RecommendationMessage = "Set Transaction Status activity is only allowed in the workflow invoked from Handoff state (settransactionstatus.xaml). Remove it from any other workflow."
            };

            workflowAnalyzerConfigService.AddRule<IWorkflowModel>(newRule);
        }

        private InspectionResult SetTransactionStatusOnlyInHandoffRole(IWorkflowModel currentWorkflow, Rule theNewRule)
        {
            var messageList = new List<string>();

            // Enforce location: settransactionstatus.xaml may only be invoked from Handoff state.
            if (!IsSetTransactionStatusWorkflow(currentWorkflow))
            {
                string xamlPath = GetWorkflowFilePath(currentWorkflow);
                List<string> invalidInvocationMessages = GetInvalidSetTransactionStatusInvocationMessages(xamlPath);
                if (invalidInvocationMessages != null && invalidInvocationMessages.Count > 0)
                {
                    string pathForMessage = string.IsNullOrEmpty(currentWorkflow.RelativePath) ? xamlPath : currentWorkflow.RelativePath;
                    foreach (var msg in invalidInvocationMessages)
                    {
                        messageList.Add(string.Format("{0} Invoking path: {1}.", msg, pathForMessage));
                    }
                }
            }

            // Only settransactionstatus.xaml is allowed to contain Set Transaction Status
            if (IsSetTransactionStatusWorkflow(currentWorkflow))
                return messageList.Count > 0
                    ? new InspectionResult() { HasErrors = true, Messages = messageList, RecommendationMessage = "settransactionstatus.xaml may only be invoked from the Handoff section.", ErrorLevel = theNewRule.DefaultErrorLevel }
                    : new InspectionResult() { HasErrors = false };

            if (messageList.Count > 0)
            {
                return new InspectionResult()
                {
                    HasErrors = true,
                    Messages = messageList,
                    RecommendationMessage = "SetTransactionStatus.xaml may only be invoked from the Handoff section. Remove the invoke from this workflow or move it to the Handoff state.",
                    ErrorLevel = theNewRule.DefaultErrorLevel
                };
            }

            if (currentWorkflow?.Root == null)
                return new InspectionResult() { HasErrors = false };

            bool found = ContainsSetTransactionStatusActivity(currentWorkflow.Root);
            if (!found)
                return new InspectionResult() { HasErrors = false };

            messageList.Add(string.Format("Workflow '{0}' contains Set Transaction Status activity.",
                currentWorkflow.DisplayName));

            return new InspectionResult()
            {
                HasErrors = true,
                Messages = messageList,
                RecommendationMessage = "Set Transaction Status activity is only allowed in settransactionstatus.xaml (invoked from Handoff state). Remove it from any other workflow.",
                ErrorLevel = theNewRule.DefaultErrorLevel
            };
        }
    }
}
