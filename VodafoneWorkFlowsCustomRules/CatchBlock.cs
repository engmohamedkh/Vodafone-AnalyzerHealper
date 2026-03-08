using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UiPath.Studio.Activities.Api;
using UiPath.Studio.Activities.Api.Analyzer;
using UiPath.Studio.Activities.Api.Analyzer.Rules;
using UiPath.Studio.Analyzer.Models;

namespace AnalyzerNamespace
{
    public class ActivityCounterRule : IRegisterAnalyzerConfiguration
    {
        string CustomRuleID = "VF-054";
        string CustomRuleName = "Catch Block";
        string AnalyzerFeature = "WorkflowAnalyzerV4";
        int ActivityCount = 0;

        public void Initialize(IAnalyzerConfigurationService configService)
        {
            if (!configService.HasFeature(AnalyzerFeature))
                return;

            var rule = new Rule<IWorkflowModel>(CustomRuleName, CustomRuleID, EvaluateWorkflow)
            {
                ErrorLevel = TraceLevel.Warning,
                RecommendationMessage = $"Only Assign and Error Log activities are allowed."
            };

            configService.AddRule<IWorkflowModel>(rule);
        }

        private void CountAllActivities(IActivityModel node)
        {
            ActivityCount++;
            foreach (var child in node.Children)
            {
                CountAllActivities(child);
            }
        }

        private InspectionResult EvaluateWorkflow(IWorkflowModel workflow, Rule rule)
        {
            var issues = new List<string>();
            ActivityCount = 0;

            if (workflow.RelativePath.ToLower().Contains("subprocess"))
            {
                foreach (var child in workflow.Root.Children)
                {
                    CountAllActivities(child);
                }

                string filePath = Path.Combine(workflow.Project.Directory.ToString(), workflow.RelativePath);
                if (File.Exists(filePath))
                {
                    string[] lines = File.ReadAllLines(filePath);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        if (lines[i].Contains("<Catch"))
                        {
                            int start = i;
                            int end = Array.FindIndex(lines, start + 1, line => line.Contains("</Catch>"));
                            if (end > start)
                            {
                                var catchLines = lines.Skip(start).Take(end - start + 1).ToList();
                                string catchContent = string.Join(Environment.NewLine, catchLines).ToLower();

                                bool isMatch = Regex.IsMatch(filePath, @"AE01.*Recovery.xaml$") || filePath.EndsWith("ControlApplications.xaml");
                                string[] allowedTags = isMatch
                                    ? new[] { "<assign", "<error_log", "<invokeworkflowfile", "<ui:invokeworkflowfile" }
                                    : new[] { "<assign", "<error_log" };

                                var hasInvalidActivities = catchLines.Any(line =>
                                {
                                    var trimmed = line.Trim().ToLower();
                                    if (string.IsNullOrWhiteSpace(trimmed)) return false;
                                    if (trimmed.StartsWith("</")) return false;
                                    if (trimmed.StartsWith("<sap:") || trimmed.StartsWith("<x:") || trimmed.StartsWith("<scg:") ||
                                        trimmed.StartsWith("<catch") || trimmed.StartsWith("<activityaction") ||
                                        trimmed.StartsWith("<delegateinargument") || trimmed.StartsWith("<sequence") ||
                                        trimmed.StartsWith("<assign.to>") || trimmed.StartsWith("<assign.value>") ||
                                        trimmed.StartsWith("<outargument") || trimmed.StartsWith("<inargument") ||
                                        trimmed.StartsWith("<ui:invokeworkflowfile.arguments>") ||
                                        trimmed.StartsWith("<i:process_monitoring___subprocess___end"))
                                        return false;

                                    return trimmed.StartsWith("<") && allowedTags.All(tag => !trimmed.StartsWith(tag));
                                });

                                if (hasInvalidActivities)
                                {
                                    issues.Add($"Workflow '{workflow.DisplayName}' catch block includes some disallowed activities.");
                                }

                                i = end; // Skip to end of current catch block
                            }
                        }
                    }
                }
            }

            if (issues.Count > 0)
            {
                return new InspectionResult()
                {
                    HasErrors = true,
                    Messages = issues.Distinct().ToList(),
                    RecommendationMessage = $"Only Assign and Error Log activities are allowed.",
                    ErrorLevel = rule.DefaultErrorLevel
                };
            }

            return new InspectionResult() { HasErrors = false };
        }
    }
}
