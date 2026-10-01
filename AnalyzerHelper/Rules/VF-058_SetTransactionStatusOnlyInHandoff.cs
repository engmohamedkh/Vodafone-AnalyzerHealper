using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using AnalyzerHelper.Interfaces;
using AnalyzerHelper.Models;

namespace AnalyzerHelper.Rules
{
    /// <summary>
    /// VF-058 (also covers VF-062) — Enforces that the Set Transaction Status activity is ONLY allowed
    /// inside the workflow invoked from the Handoff state (settransactionstatus.xaml).
    /// Also validates that invocations of settransactionstatus.xaml occur strictly inside the Handoff state
    /// of the State Machine.
    /// </summary>
    public sealed class SetTransactionStatusOnlyInHandoff : IAnalyzerRule
    {
        public string RuleId => "VF-058";
        public string RuleName => "prohibited SetTransactionStatus";
        public string DefaultRecommendation =>
            "Set Transaction Status activity is only allowed in the workflow invoked from Handoff state (settransactionstatus.xaml). Remove it from any other workflow.";
        public bool RequiresUserInteraction => false;

        private const string SetTransactionStatusXamlName = "settransactionstatus.xaml";

        public IReadOnlyList<RuleCheckResult> Check(string filePath, string content)
        {
            var results = new List<RuleCheckResult>();

            if (string.IsNullOrWhiteSpace(filePath) || !filePath.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
                return results;

            string workflowDisplayName = Path.GetFileNameWithoutExtension(filePath);

            if (string.IsNullOrWhiteSpace(content))
                return results;

            if (!XamlActivityHelper.TryParse(content, out var doc) || doc?.Root == null)
                return results;

            bool isSetTransactionStatusFile = IsSetTransactionStatusWorkflow(filePath);

            // 1. Check invocations of settransactionstatus.xaml (must be inside Handoff state)
            foreach (var el in doc.Descendants())
            {
                if (!XamlActivityHelper.NameContains(el, "InvokeWorkflowFile"))
                    continue;

                string? targetWf = XamlActivityHelper.GetAttribute(el, "WorkflowFileName");
                if (string.IsNullOrWhiteSpace(targetWf))
                {
                    // Check argument element for WorkflowFileName
                    var argEl = el.Elements().FirstOrDefault(e =>
                        e.Name.LocalName.EndsWith(".Arguments", StringComparison.OrdinalIgnoreCase));
                    var wfArg = argEl?.Elements().FirstOrDefault(e =>
                        string.Equals(e.Attribute("Key")?.Value, "WorkflowFileName", StringComparison.OrdinalIgnoreCase));
                    targetWf = wfArg?.Value?.Trim(' ', '"');
                }

                if (string.IsNullOrWhiteSpace(targetWf))
                    continue;

                bool targetsSetTxStatus = targetWf.Trim().EndsWith(SetTransactionStatusXamlName, StringComparison.OrdinalIgnoreCase) ||
                                          string.Equals(Path.GetFileName(targetWf.Trim()), SetTransactionStatusXamlName, StringComparison.OrdinalIgnoreCase);

                if (!targetsSetTxStatus)
                    continue;

                if (!IsInsideHandoffState(el))
                {
                    string stateName = GetEnclosingStateName(el);
                    string inState = string.IsNullOrWhiteSpace(stateName) ? "unknown" : stateName;

                    results.Add(new RuleCheckResult
                    {
                        RuleId = RuleId,
                        RuleName = RuleName,
                        Level = RuleLevel.Error,
                        Message = $"settransactionstatus.xaml is invoked outside the Handoff state (state: {inState}). Move it under the Handoff state section of the state machine. Invoking path: {Path.GetFileName(filePath)}.",
                        FilePath = filePath,
                        Recommendation = "SetTransactionStatus.xaml may only be invoked from the Handoff section. Remove the invoke from this workflow or move it to the Handoff state."
                    });
                }
            }

            // 2. If this workflow is settransactionstatus.xaml itself, it is authorized to contain SetTransactionStatus activities
            if (isSetTransactionStatusFile)
                return results;

            // 3. Prohibit Set Transaction Status activities in any other workflow
            foreach (var el in XamlActivityHelper.EnumerateActivities(doc))
            {
                if (IsSetTransactionStatusActivity(el))
                {
                    results.Add(new RuleCheckResult
                    {
                        RuleId = RuleId,
                        RuleName = RuleName,
                        Level = RuleLevel.Error,
                        Message = $"Workflow '{workflowDisplayName}' contains Set Transaction Status activity.",
                        FilePath = filePath,
                        Recommendation = DefaultRecommendation
                    });
                    break; // Flag once per workflow
                }
            }

            return results;
        }

        private static bool IsSetTransactionStatusWorkflow(string filePath)
        {
            string fileName = Path.GetFileName(filePath);
            return fileName.ToLowerInvariant().Split('_').Last().Equals(SetTransactionStatusXamlName, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSetTransactionStatusActivity(XElement el)
        {
            if (el == null) return false;

            string localName = XamlActivityHelper.GetLocalName(el);
            if (string.Equals(localName, "SetTransactionStatus", StringComparison.OrdinalIgnoreCase))
                return true;

            string displayName = XamlActivityHelper.GetDisplayName(el);
            if (displayName.IndexOf("Set Transaction Status", StringComparison.OrdinalIgnoreCase) >= 0 ||
                displayName.IndexOf("SetTransactionStatus", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                // Verify it's not a log message or comment
                if (!localName.Contains("Log") && !localName.Contains("Comment") && !localName.Contains("Invoke"))
                    return true;
            }

            return false;
        }

        private static bool IsInsideHandoffState(XElement element)
        {
            XElement? current = element.Parent;
            while (current != null)
            {
                if (string.Equals(current.Name.LocalName, "State", StringComparison.OrdinalIgnoreCase))
                {
                    string displayName = current.Attribute("DisplayName")?.Value ?? "";
                    string key = current.Attributes().FirstOrDefault(a => string.Equals(a.Name.LocalName, "Key", StringComparison.OrdinalIgnoreCase))?.Value ?? "";

                    if (displayName.IndexOf("Handoff", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        key.IndexOf("Handoff", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                    return false; // Inside a State that is not Handoff
                }
                current = current.Parent;
            }
            return false;
        }

        private static string GetEnclosingStateName(XElement element)
        {
            XElement? current = element.Parent;
            while (current != null)
            {
                if (string.Equals(current.Name.LocalName, "State", StringComparison.OrdinalIgnoreCase))
                {
                    string displayName = current.Attribute("DisplayName")?.Value ?? "";
                    string key = current.Attributes().FirstOrDefault(a => string.Equals(a.Name.LocalName, "Key", StringComparison.OrdinalIgnoreCase))?.Value ?? "";
                    string name = !string.IsNullOrWhiteSpace(displayName) ? displayName : key;
                    return name.Trim();
                }
                current = current.Parent;
            }
            return string.Empty;
        }
    }
}
