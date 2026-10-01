using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using AnalyzerHelper.Interfaces;
using AnalyzerHelper.Models;

namespace AnalyzerHelper.Rules
{
    /// <summary>
    /// VF-031 — Checks the displayed default name for each activity.
    /// Flags activities retaining default UiPath activity display names as Info findings.
    /// </summary>
    public sealed class DefaultDisplayedName : IAnalyzerRule
    {
        private static readonly HashSet<string> DefaultDisplayedNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "Sequence",
            "Click",
            "Get Text",
            "Type Into",
            "Activate",
            "Find Element",
            "Maximize Window",
            "Do While",
            "For Each",
            "If",
            "Switch",
            "While",
            "Flowchart",
            "Flow Switch",
            "Flow Decision"
        };

        private static readonly Dictionary<string, string> TagToDefaultDisplayName = new(StringComparer.OrdinalIgnoreCase)
        {
            { "Sequence", "Sequence" },
            { "If", "If" },
            { "While", "While" },
            { "InterruptibleWhile", "While" },
            { "DoWhile", "Do While" },
            { "InterruptibleDoWhile", "Do While" },
            { "ForEach", "For Each" },
            { "InterruptibleForEach", "For Each" },
            { "Switch", "Switch" },
            { "Flowchart", "Flowchart" },
            { "FlowDecision", "Flow Decision" },
            { "FlowSwitch", "Flow Switch" },
            { "Click", "Click" },
            { "GetValue", "Get Text" },
            { "GetText", "Get Text" },
            { "TypeInto", "Type Into" },
            { "Activate", "Activate" },
            { "WaitUiElementAppear", "Find Element" },
            { "FindElement", "Find Element" },
            { "MaximizeWindow", "Maximize Window" }
        };

        private static readonly HashSet<string> NonActivityLocalNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "Activity", "Variable", "VariableList", "InArgument", "OutArgument", "InOutArgument",
            "VisualBasicSettings", "Collection", "Dictionary", "ViewStateDictionary", "Array",
            "Property", "TextExpression", "Literal", "VisualBasicValue", "VisualBasicReference",
            "Reference", "Null", "xNull", "AssemblyReference", "ActivityAction", "ActivityFunc",
            "DelegateInArgument", "DelegateOutArgument", "FlowStep"
        };

        public string RuleId => "VF-031";
        public string RuleName => "DefaultDisplayedName";
        public string DefaultRecommendation => "Please change the default dispalyed name to a descriptive name";
        public bool RequiresUserInteraction => false;

        public IReadOnlyList<RuleCheckResult> Check(string filePath, string content)
        {
            var results = new List<RuleCheckResult>();
            if (!XamlActivityHelper.TryParse(content, out var doc) || doc?.Root == null) return results;

            foreach (var el in doc.Root.Descendants())
            {
                if (el == doc.Root) continue;

                // Skip property wrapper elements (e.g. Sequence.Variables, If.Then)
                string localName = el.Name.LocalName;
                if (localName.Contains('.')) continue;

                // Skip structural non-activity definitions
                if (NonActivityLocalNames.Contains(localName)) continue;

                string? displayNameAttr = el.Attributes().FirstOrDefault(a =>
                    string.Equals(a.Name.LocalName, "DisplayName", StringComparison.OrdinalIgnoreCase))?.Value;

                string? matchedDefaultName = null;

                if (!string.IsNullOrWhiteSpace(displayNameAttr))
                {
                    string trimmedName = displayNameAttr.Trim();
                    if (DefaultDisplayedNames.Contains(trimmedName))
                    {
                        matchedDefaultName = trimmedName;
                    }
                }
                else if (TagToDefaultDisplayName.TryGetValue(localName, out var defaultName))
                {
                    matchedDefaultName = defaultName;
                }

                if (matchedDefaultName != null)
                {
                    results.Add(new RuleCheckResult
                    {
                        RuleId = RuleId,
                        RuleName = RuleName,
                        Level = RuleLevel.Info,
                        Message = $"The following activity: {matchedDefaultName} has a default displayed name.",
                        FilePath = filePath,
                        Recommendation = DefaultRecommendation
                    });
                }
            }

            return results;
        }
    }
}
