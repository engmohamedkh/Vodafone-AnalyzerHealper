using UiPath.Studio.Activities.Api;
using UiPath.Studio.Activities.Api.Analyzer;
using UiPath.Studio.Activities.Api.Analyzer.Rules;
using UiPath.Studio.Analyzer.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace AnalyzerNamespace
{
    public class RethrowRule : IRegisterAnalyzerConfiguration
    {
        string CustomRuleID = "VF-055";
        string CustomRuleName = "Rethrow Rule";
        string AnalyzerFeature = "WorkflowAnalyzerV4";
       
        int ActivityCount = 0;

        public void Initialize(IAnalyzerConfigurationService configService)
        {
            if (!configService.HasFeature(AnalyzerFeature))
                return;

            var rule = new Rule<IWorkflowModel>(CustomRuleName, CustomRuleID, EvaluateWorkflow)
            {
                ErrorLevel = TraceLevel.Error,
                RecommendationMessage = $"There should be Rethrow activity in the catch block of Automation/Logical component"
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
            var filePath = Path.Combine(workflow.Project.Directory.ToString(), workflow.RelativePath);

            bool isTargetedWorkflow = workflow.RelativePath.ToLower().Contains("logic") ||
                                      filePath.ToLower().Contains("\\navigate\\") ||
                                      filePath.ToLower().Contains("\\other\\") ||
                                      filePath.ToLower().Contains("\\read\\") ||
                                      filePath.ToLower().Contains("\\write\\");

            if (isTargetedWorkflow)
            {
                foreach (var child in workflow.Root.Children)
                {
                    CountAllActivities(child);
                }

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

                                if (!catchContent.Contains("rethrow"))
                                {
                                    issues.Add($"Workflow '{workflow.DisplayName}' Catch block doesn't have Rethrow");
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
                    RecommendationMessage = $"There should be Rethrow activity in the catch block of Automation/Logical component.",
                    ErrorLevel = rule.DefaultErrorLevel
                };
            }

            return new InspectionResult() { HasErrors = false };
        }
    }
}
