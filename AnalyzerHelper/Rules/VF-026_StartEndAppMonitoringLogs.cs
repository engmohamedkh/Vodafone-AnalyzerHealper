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
    /// VF-026 — Checks whether workflows implement Start and/or End IAP App Monitoring logging activities.
    /// Used for application performance tracking and audit reporting (Info level).
    /// </summary>
    public sealed class StartEndAppMonitoringLogs : IAnalyzerRule
    {
        public string RuleId => "VF-026";
        public string RuleName => "StartEndAppMonitoringLogs";
        public string DefaultRecommendation => "Please note that app monitoring start/end logs use to evaluate the application performance.";
        public bool RequiresUserInteraction => false;

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

            bool appMonitoringStart = false;
            bool appMonitoringEnd = false;

            // Find root activity under <Activity>
            var rootActivity = doc.Root.Elements().FirstOrDefault(e =>
                !XamlActivityHelper.IsPropertyElement(e) &&
                !e.Name.LocalName.StartsWith("WorkflowViewState", StringComparison.OrdinalIgnoreCase) &&
                !e.Name.LocalName.StartsWith("ViewStateService", StringComparison.OrdinalIgnoreCase) &&
                !e.Name.LocalName.Equals("Members", StringComparison.OrdinalIgnoreCase) &&
                !e.Name.LocalName.Equals("VisualBasic.Settings", StringComparison.OrdinalIgnoreCase) &&
                !e.Name.LocalName.Equals("TextExpression.NamespacesForImplementation", StringComparison.OrdinalIgnoreCase) &&
                !e.Name.LocalName.Equals("TextExpression.ReferencesForImplementation", StringComparison.OrdinalIgnoreCase));

            if (rootActivity == null)
                return results;

            // Check positional boundaries (first and last child of root activity container)
            var topChildren = GetTopLevelActivities(rootActivity).ToList();
            if (topChildren.Count > 0)
            {
                if (IsAppMonitoringLog(topChildren[0], isStart: true))
                    appMonitoringStart = true;

                if (IsAppMonitoringLog(topChildren[topChildren.Count - 1], isStart: false))
                    appMonitoringEnd = true;
            }

            // Also scan all activities in the workflow to catch App Monitoring logs inside Try blocks or sequences
            if (!appMonitoringStart || !appMonitoringEnd)
            {
                foreach (var activity in doc.Descendants())
                {
                    if (XamlActivityHelper.IsStructural(activity) && !IsAppMonitoringLog(activity, isStart: true) && !IsAppMonitoringLog(activity, isStart: false))
                        continue;

                    if (!appMonitoringStart && IsAppMonitoringLog(activity, isStart: true))
                        appMonitoringStart = true;

                    if (!appMonitoringEnd && IsAppMonitoringLog(activity, isStart: false))
                        appMonitoringEnd = true;

                    if (appMonitoringStart && appMonitoringEnd)
                        break;
                }
            }

            if (appMonitoringStart && appMonitoringEnd)
            {
                results.Add(new RuleCheckResult
                {
                    RuleId = RuleId,
                    RuleName = RuleName,
                    Level = RuleLevel.Info,
                    Message = $"The following workflow: {workflowDisplayName} has start/end app monitoring logging activity.",
                    FilePath = filePath,
                    Recommendation = DefaultRecommendation
                });
            }
            else if (appMonitoringEnd)
            {
                results.Add(new RuleCheckResult
                {
                    RuleId = RuleId,
                    RuleName = RuleName,
                    Level = RuleLevel.Info,
                    Message = $"The following workflow: {workflowDisplayName} has end app monitoring logging activity.",
                    FilePath = filePath,
                    Recommendation = DefaultRecommendation
                });
            }
            else if (appMonitoringStart)
            {
                results.Add(new RuleCheckResult
                {
                    RuleId = RuleId,
                    RuleName = RuleName,
                    Level = RuleLevel.Info,
                    Message = $"The following workflow: {workflowDisplayName} has start app monitoring logging activity.",
                    FilePath = filePath,
                    Recommendation = DefaultRecommendation
                });
            }

            return results;
        }

        private static IEnumerable<XElement> GetTopLevelActivities(XElement container)
        {
            if (container == null) yield break;

            foreach (var child in container.Elements())
            {
                if (XamlActivityHelper.IsPropertyElement(child))
                {
                    foreach (var inner in child.Elements().Where(e => !XamlActivityHelper.IsPropertyElement(e)))
                    {
                        yield return inner;
                    }
                }
                else
                {
                    yield return child;
                }
            }
        }

        private static bool IsAppMonitoringLog(XElement el, bool isStart)
        {
            if (el == null) return false;

            string localName = XamlActivityHelper.GetLocalName(el);
            string displayName = XamlActivityHelper.GetDisplayName(el);
            string typeHint = XamlActivityHelper.GetTypeHint(el);
            string fullName = el.Name.ToString();

            string combined = $"{localName} {displayName} {typeHint} {fullName}".ToLowerInvariant();

            bool hasAppMonitoring = combined.Contains("app_monitoring") ||
                                    combined.Contains("app monitoring") ||
                                    combined.Contains("appmonitoring");

            bool targetKeyword = isStart
                ? combined.Contains("start")
                : combined.Contains("end");

            if (hasAppMonitoring && targetKeyword)
                return true;

            // Also check InvokeWorkflowFile activities targeting App Monitoring workflows
            string? wf = XamlActivityHelper.GetAttribute(el, "WorkflowFileName");
            if (!string.IsNullOrEmpty(wf))
            {
                string wfLower = wf.ToLowerInvariant();
                bool wfHasAppMonitoring = wfLower.Contains("app_monitoring") ||
                                          wfLower.Contains("app monitoring") ||
                                          wfLower.Contains("appmonitoring");
                bool wfTargetKeyword = isStart
                    ? wfLower.Contains("start")
                    : wfLower.Contains("end");

                if (wfHasAppMonitoring && wfTargetKeyword)
                    return true;
            }

            return false;
        }
    }
}
