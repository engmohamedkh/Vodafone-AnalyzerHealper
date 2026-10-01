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
    /// VF-009 — Checks that workflows contain mandatory Start and End IAP logging activities,
    /// as well as an End log passing exception.message in exception handling blocks.
    /// </summary>
    public sealed class StartEndLogs : IAnalyzerRule
    {
        public string RuleId => "VF-009";
        public string RuleName => "StartEndLogs";
        public string DefaultRecommendation => "Please add start/end logs for every workflow created.";
        public bool RequiresUserInteraction => false;

        public IReadOnlyList<RuleCheckResult> Check(string filePath, string content)
        {
            var results = new List<RuleCheckResult>();

            if (string.IsNullOrWhiteSpace(filePath) || !filePath.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
                return results;

            string workflowDisplayName = Path.GetFileNameWithoutExtension(filePath);

            if (IsExempt(filePath, workflowDisplayName))
                return results;

            if (string.IsNullOrWhiteSpace(content))
            {
                results.Add(CreateResult(filePath, $"The following workflow: {workflowDisplayName} is empty."));
                return results;
            }

            if (!XamlActivityHelper.TryParse(content, out var doc) || doc?.Root == null)
            {
                results.Add(CreateResult(filePath, $"The following workflow: {workflowDisplayName} is empty."));
                return results;
            }

            var messageList = new List<string>();

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
            {
                messageList.Add($"The following workflow: {workflowDisplayName} is empty.");
            }
            else if (rootActivity.Name.LocalName.Equals("Sequence", StringComparison.OrdinalIgnoreCase) ||
                     rootActivity.Name.LocalName.Equals("Flowchart", StringComparison.OrdinalIgnoreCase))
            {
                var childActivities = rootActivity.Elements().Where(e =>
                    !XamlActivityHelper.IsPropertyElement(e) &&
                    !XamlActivityHelper.IsStructural(e)).ToList();

                if (childActivities.Count == 0)
                {
                    // No activities yet in root container
                    return results;
                }

                // 1. Start Log activity check (first child activity)
                if (!IsStartLogActivity(childActivities[0]))
                {
                    messageList.Add($"The following workflow: {workflowDisplayName} doesn't have start logging activity.");
                }

                // 2. TryCatch activity check
                var tryCatch = childActivities.FirstOrDefault(e => e.Name.LocalName.Equals("TryCatch", StringComparison.OrdinalIgnoreCase))
                               ?? rootActivity.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals("TryCatch", StringComparison.OrdinalIgnoreCase));

                if (tryCatch == null)
                {
                    messageList.Add($"The following workflow: {workflowDisplayName} doesn't use the agreed template.");
                }
                else
                {
                    var tryProp = tryCatch.Elements().FirstOrDefault(e => e.Name.LocalName.EndsWith(".Try", StringComparison.OrdinalIgnoreCase));
                    var tryActivity = tryProp?.Elements().FirstOrDefault(e => !XamlActivityHelper.IsPropertyElement(e));

                    var catchesProp = tryCatch.Elements().FirstOrDefault(e => e.Name.LocalName.EndsWith(".Catches", StringComparison.OrdinalIgnoreCase));
                    var catchElements = catchesProp?.Elements().Where(e => e.Name.LocalName.Equals("Catch", StringComparison.OrdinalIgnoreCase)).ToList()
                                        ?? tryCatch.Descendants().Where(e => e.Name.LocalName.Equals("Catch", StringComparison.OrdinalIgnoreCase)).ToList();

                    if (tryActivity == null && catchElements.Count == 0)
                    {
                        messageList.Add($"The following workflow: {workflowDisplayName} have try catch activity but it's empty.");
                        foreach (var msg in messageList)
                        {
                            results.Add(CreateResult(filePath, msg));
                        }
                        return results;
                    }

                    if (catchElements.Count == 0)
                    {
                        messageList.Add($"The following workflow: {workflowDisplayName} have try catch activity but it doesn't have exception handling.");
                    }

                    // Check Try container
                    if (tryActivity != null)
                    {
                        if (tryActivity.Name.LocalName.Equals("Sequence", StringComparison.OrdinalIgnoreCase) ||
                            tryActivity.Name.LocalName.Equals("Flowchart", StringComparison.OrdinalIgnoreCase))
                        {
                            var tryChilds = tryActivity.Elements().Where(e =>
                                !XamlActivityHelper.IsPropertyElement(e) &&
                                !XamlActivityHelper.IsStructural(e)).ToList();

                            if (tryChilds.Count > 0)
                            {
                                if (!IsEndLogActivity(tryChilds[tryChilds.Count - 1]))
                                {
                                    messageList.Add($"The following workflow: {workflowDisplayName} doesn't have end logging activity at end of the workflow or end of the try catch.");
                                }
                            }
                            else
                            {
                                messageList.Add($" Empty sequence The following workflow: {workflowDisplayName} doesn't have end logging activity at end of the workflow or end of the try catch.");
                            }
                        }
                        else
                        {
                            messageList.Add($"The following workflow: {workflowDisplayName} doesn't use the agreed template");
                        }
                    }

                    // Check Exception handling / Catches
                    foreach (var catchEl in catchElements)
                    {
                        var handler = catchEl.Descendants().FirstOrDefault(e =>
                            e != catchEl &&
                            !XamlActivityHelper.IsPropertyElement(e) &&
                            !e.Name.LocalName.Equals("ActivityAction", StringComparison.OrdinalIgnoreCase) &&
                            !e.Name.LocalName.Equals("DelegateInArgument", StringComparison.OrdinalIgnoreCase) &&
                            !e.Name.LocalName.StartsWith("WorkflowViewState", StringComparison.OrdinalIgnoreCase) &&
                            !e.Name.LocalName.StartsWith("ViewStateService", StringComparison.OrdinalIgnoreCase));

                        if (handler == null)
                        {
                            messageList.Add($"The following workflow: {workflowDisplayName} doesn't have end logging activity at the exception handling.");
                        }
                        else if (handler.Name.LocalName.Equals("Sequence", StringComparison.OrdinalIgnoreCase) ||
                                 handler.Name.LocalName.Equals("Flowchart", StringComparison.OrdinalIgnoreCase))
                        {
                            var endLog = handler.DescendantsAndSelf().FirstOrDefault(e => IsEndLogActivity(e));
                            if (endLog == null)
                            {
                                messageList.Add($"The following workflow: {workflowDisplayName} doesn't have end logging activity at the exception handling.");
                            }
                            else
                            {
                                if (!HasExceptionMessage(endLog))
                                {
                                    messageList.Add($"The following workflow: {workflowDisplayName} doesn't have exception.message and exception.source in the IAP end logging activity at the exception handling.");
                                }
                            }
                        }
                        else
                        {
                            if (!IsEndLogActivity(handler))
                            {
                                messageList.Add($"The following workflow: {workflowDisplayName} doesn't have end logging activity at the exception handling.");
                            }
                            else
                            {
                                if (!HasExceptionMessage(handler))
                                {
                                    messageList.Add($"The following workflow: {workflowDisplayName} doesn't have exception.message in the IAP end logging activity at the exception handling.");
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                messageList.Add($"The following workflow: {workflowDisplayName} doesn't use the agreed template.");
            }

            foreach (var msg in messageList)
            {
                results.Add(CreateResult(filePath, msg));
            }

            return results;
        }

        private static bool IsStartLogActivity(XElement el)
        {
            if (el == null) return false;

            string localName = XamlActivityHelper.GetLocalName(el);
            string displayName = XamlActivityHelper.GetDisplayName(el);
            string full = $"{localName} {displayName}".ToLowerInvariant();

            bool hasMonitoring = full.Contains("process_monitoring") || full.Contains("app_monitoring") ||
                                 full.Contains("process monitoring") || full.Contains("app monitoring");
            bool hasStart = full.Contains("start");

            if (hasMonitoring && hasStart)
                return true;

            if (full.Contains("start_log") || full.Contains("start log"))
                return true;

            string? wf = XamlActivityHelper.GetAttribute(el, "WorkflowFileName");
            if (!string.IsNullOrEmpty(wf))
            {
                string wfLower = wf.ToLowerInvariant();
                if ((wfLower.Contains("process_monitoring") || wfLower.Contains("app_monitoring") ||
                     wfLower.Contains("process monitoring") || wfLower.Contains("app monitoring")) &&
                    wfLower.Contains("start"))
                {
                    return true;
                }
                if (wfLower.Contains("start_log") || wfLower.Contains("start log"))
                    return true;
            }

            return false;
        }

        private static bool IsEndLogActivity(XElement el)
        {
            if (el == null) return false;

            string localName = XamlActivityHelper.GetLocalName(el);
            string displayName = XamlActivityHelper.GetDisplayName(el);
            string full = $"{localName} {displayName}".ToLowerInvariant();

            bool hasMonitoring = full.Contains("process_monitoring") || full.Contains("app_monitoring") ||
                                 full.Contains("process monitoring") || full.Contains("app monitoring");
            bool hasEnd = full.Contains("end");

            if (hasMonitoring && hasEnd)
                return true;

            if (full.Contains("end_log") || full.Contains("end log"))
                return true;

            string? wf = XamlActivityHelper.GetAttribute(el, "WorkflowFileName");
            if (!string.IsNullOrEmpty(wf))
            {
                string wfLower = wf.ToLowerInvariant();
                if ((wfLower.Contains("process_monitoring") || wfLower.Contains("app_monitoring") ||
                     wfLower.Contains("process monitoring") || wfLower.Contains("app monitoring")) &&
                    wfLower.Contains("end"))
                {
                    return true;
                }
                if (wfLower.Contains("end_log") || wfLower.Contains("end log"))
                    return true;
            }

            return false;
        }

        private static bool HasExceptionMessage(XElement endLog)
        {
            if (endLog == null) return false;

            // 1. Check direct attributes
            foreach (var attr in endLog.Attributes())
            {
                if (attr.Name.LocalName.Equals("StrExecutionMessage", StringComparison.OrdinalIgnoreCase) ||
                    attr.Name.LocalName.Equals("in_strExecutionMessage", StringComparison.OrdinalIgnoreCase))
                {
                    if (attr.Value.IndexOf("exception.message", StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }
            }

            // 2. Check child / descendant Argument elements
            foreach (var arg in endLog.Descendants())
            {
                if (arg.Name.LocalName.EndsWith("Argument", StringComparison.OrdinalIgnoreCase))
                {
                    var keyAttr = arg.Attributes().FirstOrDefault(a =>
                        a.Name.LocalName.Equals("Key", StringComparison.OrdinalIgnoreCase) ||
                        a.Name.LocalName.Equals("Name", StringComparison.OrdinalIgnoreCase));

                    if (keyAttr != null && (keyAttr.Value.Equals("strExecutionMessage", StringComparison.OrdinalIgnoreCase) ||
                                           keyAttr.Value.Equals("in_strExecutionMessage", StringComparison.OrdinalIgnoreCase)))
                    {
                        if (arg.Value.IndexOf("exception.message", StringComparison.OrdinalIgnoreCase) >= 0)
                            return true;
                    }
                }

                if (arg.Name.LocalName.EndsWith(".StrExecutionMessage", StringComparison.OrdinalIgnoreCase) ||
                    arg.Name.LocalName.EndsWith(".strExecutionMessage", StringComparison.OrdinalIgnoreCase))
                {
                    if (arg.Value.IndexOf("exception.message", StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }
            }

            // 3. Fallback: Any execution message attribute containing exception.message
            foreach (var attr in endLog.Attributes())
            {
                if (attr.Name.LocalName.IndexOf("ExecutionMessage", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    attr.Value.IndexOf("exception.message", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsExempt(string filePath, string workflowDisplayName)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return true;

            string fileName = Path.GetFileName(filePath);
            string normPath = filePath.Replace('\\', '/');

            if (workflowDisplayName.IndexOf("IAP_", StringComparison.OrdinalIgnoreCase) >= 0 ||
                workflowDisplayName.IndexOf("ProcessName_", StringComparison.OrdinalIgnoreCase) >= 0 ||
                workflowDisplayName.IndexOf("Subprocess", StringComparison.OrdinalIgnoreCase) >= 0 ||
                workflowDisplayName.IndexOf("LoaderMain", StringComparison.OrdinalIgnoreCase) >= 0 ||
                workflowDisplayName.IndexOf("WorkerMain", StringComparison.OrdinalIgnoreCase) >= 0 ||
                workflowDisplayName.Equals("Process", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (fileName.IndexOf("IAP_", StringComparison.OrdinalIgnoreCase) >= 0 ||
                fileName.IndexOf("ProcessName_", StringComparison.OrdinalIgnoreCase) >= 0 ||
                fileName.IndexOf("Subprocess", StringComparison.OrdinalIgnoreCase) >= 0 ||
                fileName.IndexOf("LoaderMain", StringComparison.OrdinalIgnoreCase) >= 0 ||
                fileName.IndexOf("WorkerMain", StringComparison.OrdinalIgnoreCase) >= 0 ||
                fileName.Equals("Process.xaml", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (normPath.IndexOf("subprocess", StringComparison.OrdinalIgnoreCase) >= 0 ||
                normPath.IndexOf("/Framework/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            return false;
        }

        private RuleCheckResult CreateResult(string filePath, string message) =>
            new RuleCheckResult
            {
                RuleId = RuleId,
                RuleName = RuleName,
                Level = RuleLevel.Error,
                FilePath = filePath,
                Message = message,
                Recommendation = DefaultRecommendation,
                RequiresUserInteraction = false
            };
    }
}
