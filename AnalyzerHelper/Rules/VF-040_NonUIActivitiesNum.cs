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
    /// VF-040 — Checks that the number of activities in Logic and Subprocess workflows
    /// does not exceed the threshold (max 100 activities).
    /// </summary>
    public sealed class NonUIActivitiesNum : IAnalyzerRule
    {
        private const int Threshold = 100;

        private static readonly HashSet<string> NonActivityLocalNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "Activity", "Variable", "VariableList", "InArgument", "OutArgument", "InOutArgument",
            "VisualBasicSettings", "Collection", "Dictionary", "ViewStateDictionary", "Array",
            "Property", "TextExpression", "Literal", "VisualBasicValue", "VisualBasicReference",
            "Reference", "Null", "xNull", "AssemblyReference", "ActivityAction", "ActivityFunc",
            "DelegateInArgument", "DelegateOutArgument", "FlowStep", "Members"
        };

        public string RuleId => "VF-040";
        public string RuleName => "Activities Number";
        public string DefaultRecommendation =>
            $"Checking that the activities number didn't exceed the threshold. Max number is '{Threshold}' activities in logic/Subprocess files.";
        public bool RequiresUserInteraction => false;

        public IReadOnlyList<RuleCheckResult> Check(string filePath, string content)
        {
            var results = new List<RuleCheckResult>();

            if (string.IsNullOrWhiteSpace(content))
                return results;

            if (!filePath.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
                return results;

            if (!IsInLogicOrSubprocessFolder(filePath))
                return results;

            if (!XamlActivityHelper.TryParse(content, out var doc) || doc?.Root == null)
                return results;

            int totalActivities = CountActivities(doc.Root);

            if (totalActivities > Threshold)
            {
                string workflowDisplayName = Path.GetFileNameWithoutExtension(filePath);
                results.Add(new RuleCheckResult
                {
                    RuleId = RuleId,
                    RuleName = RuleName,
                    Level = RuleLevel.Warning,
                    FilePath = filePath,
                    Message = $"The following Workflow: {workflowDisplayName} contains {totalActivities} activities. Please consider splitting it. Max allowed is {Threshold}.",
                    Recommendation = DefaultRecommendation,
                    RequiresUserInteraction = false
                });
            }

            return results;
        }

        private static int CountActivities(XElement root)
        {
            int count = 0;

            foreach (var el in root.Descendants())
            {
                if (el == root) continue;

                // Skip property wrapper elements (e.g. Sequence.Variables, If.Then, TryCatch.Try)
                string localName = el.Name.LocalName;
                if (localName.Contains('.')) continue;

                // Skip UiPath metadata namespaces (sap, sap2010, xaml presentation)
                string ns = el.Name.NamespaceName;
                if (ns.Contains("presentation") || ns.Contains("markup-compatibility"))
                    continue;

                // Skip structural non-activity elements
                if (NonActivityLocalNames.Contains(localName))
                    continue;

                count++;
            }

            return count;
        }

        private static bool IsInLogicOrSubprocessFolder(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return false;

            string normalized = filePath.Replace('\\', '/');
            var segments = normalized.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < segments.Length - 1; i++)
            {
                string seg = segments[i];
                if (seg.IndexOf("logic", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    seg.IndexOf("subprocess", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
