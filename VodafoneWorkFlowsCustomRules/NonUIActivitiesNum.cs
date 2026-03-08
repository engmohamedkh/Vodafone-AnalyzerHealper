using UiPath.Studio.Activities.Api;
using UiPath.Studio.Activities.Api.Analyzer;
using UiPath.Studio.Activities.Api.Analyzer.Rules;
using UiPath.Studio.Analyzer.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace ActivitiesNumberSpace
{
    public class ActivitiesNumber : IRegisterAnalyzerConfiguration
    {
        // Configs
        string RuleID = "VF-040";
        string RuleName = "Activities Number";
        string WFAnalyzerVersion = "WorkflowAnalyzerV4";
        int Threshold = 100;
        int Total = 0; // This should ideally be reset for each workflow analysis to prevent cumulative counting across multiple workflows.

        public void Initialize(IAnalyzerConfigurationService workflowAnalyzerConfigService)
        {
            if (!workflowAnalyzerConfigService.HasFeature(WFAnalyzerVersion))
                return;

            var newRule = new Rule<IWorkflowModel>(RuleName, RuleID, NonUIActivitiesNo)
            {
                ErrorLevel = TraceLevel.Warning,
                RecommendationMessage = $"Checking that the activities number didn't exceed the threshold. Max number is '{Threshold}' activities in logic/Subprocess files."
            };

            workflowAnalyzerConfigService.AddRule<IWorkflowModel>(newRule);
        }

        private void CountActivities(IActivityModel activity)
        {
            // This method counts all activities, including containers like Sequence, Flowchart, etc.
            // If you only want to count "actionable" activities (e.g., Assign, Invoke),
            // you'd need to add a type check here. For now, it counts all nodes.
            Total++;
            foreach (var child in activity.Children)
            {
                CountActivities(child);
            }
        }

        private InspectionResult NonUIActivitiesNo(IWorkflowModel currentWorkflow, Rule rule)
        {
            var messageList = new List<string>();

            // Reset Total for each workflow being analyzed
            Total = 0;

            // Count activities in logic or subprocess workflows
            if (currentWorkflow.RelativePath.ToLower().Contains("logic") || currentWorkflow.RelativePath.ToLower().Contains("subprocess"))
            {
                foreach (var child in currentWorkflow.Root.Children)
                {
                    CountActivities(child);
                }

                if (Total > Threshold)
                {
                    messageList.Add($"The following Workflow: {currentWorkflow.DisplayName} contains {Total} activities. Please consider splitting it. Max allowed is {Threshold}.");
                }
            }

            // Amr Ewies - Check for allowed activities in Catch blocks within subprocesses
            /*
            if (currentWorkflow.RelativePath.ToLower().Contains("subprocess"))
            {
                string xamlPath = Path.Combine(currentWorkflow.Project.Directory.ToString(), currentWorkflow.RelativePath);
                if (File.Exists(xamlPath))
                {
                    string[] xamlLines = File.ReadAllLines(xamlPath);
                    int catchStart = Array.FindIndex(xamlLines, line => line.Contains("<Catch") && line.Contains("x:TypeArguments=\"s:Exception\""));
                    int catchEnd = Array.FindIndex(xamlLines, catchStart + 1, line => line.Contains("</Catch>"));

                    if (catchStart >= 0 && catchEnd > catchStart)
                    {
                        var catchBlockLines = xamlLines.Skip(catchStart).Take(catchEnd - catchStart + 1).ToList();
                        string catchBlockContent = string.Join(Environment.NewLine, catchBlockLines).ToLower(); // Used for message content

                        // Define allowed functional activities within Catch blocks.
                        // Added "ui:invokeworkflowfile" to explicitly allow it.
                        var allowedFunctionalActivities = new[] { "<assign", "<error_log", "<invokeworkflowfile", "<ui:invokeworkflowfile" };

                        var hasOtherActivities = catchBlockLines.Any(line =>
                        {
                            var trimmed = line.Trim().ToLower();

                            // 1. Skip empty lines or lines consisting only of whitespace
                            if (string.IsNullOrWhiteSpace(trimmed))
                                return false;

                            // 2. Skip XML closing tags (e.g., </sequence>, </assign>)
                            if (trimmed.StartsWith("</"))
                                return false;

                            // 3. Skip common structural/metadata/argument tags that are NOT functional activities.
                            //    These are expected in a well-formed XAML for a Catch block.
                            //    Added "i:process_monitoring___subprocess___end" to the skip list, assuming it's a custom logging activity.
                            if (trimmed.StartsWith("<sap:") ||          // UiPath specific metadata
                                trimmed.StartsWith("<x:") ||           // XAML namespace specific metadata
                                trimmed.StartsWith("<scg:") ||         // System.Collections.Generic (for Dictionary etc.)
                                trimmed.StartsWith("<catch") ||         // The <Catch> tag itself (opening)
                                trimmed.StartsWith("<activityaction") || // Wrapper for the exception argument
                                trimmed.StartsWith("<delegateinargument") || // The actual exception argument definition
                                trimmed.StartsWith("<sequence") ||      // Sequence container
                                trimmed.StartsWith("<assign.to>") ||   // Assign activity specific internal tags
                                trimmed.StartsWith("<assign.value>") || // Assign activity specific internal tags
                                trimmed.StartsWith("<outargument") ||   // Argument definitions
                                trimmed.StartsWith("<inargument") ||    // Argument definitions
                                trimmed.StartsWith("<ui:invokeworkflowfile.arguments>") || // Arguments section for InvokeWorkflowFile
                                trimmed.StartsWith("<i:process_monitoring___subprocess___end") // Custom activity you want to allow/skip
                            )
                                return false;

                            // 4. If the line starts with an opening XML tag ('<') AND it is NOT one of the explicitly allowed functional activities,
                            //    then it's considered an "other activity" that violates the rule.
                            return trimmed.StartsWith("<") && allowedFunctionalActivities.All(tag => !trimmed.StartsWith(tag));
                        });

                        if (hasOtherActivities)
                        {
                            messageList.Add($"The following workflow: {currentWorkflow.DisplayName} contains activities in a Catch block that are not allowed. Only Assign - Error Log - Invokeworkflowfile");
                        }
                    }
                }
            }
            */

            // Determine the final inspection result
            if (messageList.Count > 0)
            {
                return new InspectionResult()
                {
                    HasErrors = true, // Set to true if any message is added, indicating a warning/error condition
                    Messages = messageList.Distinct().ToList(),
                    RecommendationMessage = $"Number of activities is too high or unauthorized activity found in Catch block. Max allowed activities: {Threshold}.",
                    ErrorLevel = rule.DefaultErrorLevel
                };
            }

            return new InspectionResult() { HasErrors = false };
        }
    }
}