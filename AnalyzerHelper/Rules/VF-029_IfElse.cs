using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using AnalyzerHelper.Interfaces;
using AnalyzerHelper.Models;

namespace AnalyzerHelper.Rules
{
    /// <summary>
    /// VF-029 — checks If activities to verify that both the Then and Else branches contain activities.
    /// Flags a warning if either Then or Else is missing, empty, or contains an empty container.
    /// </summary>
    public sealed class IfElse : IAnalyzerRule
    {
        public string RuleId => "VF-029";
        public string RuleName => "IfElseCheck";
        public string DefaultRecommendation => "Add at least log message.";
        public bool RequiresUserInteraction => false;

        public IReadOnlyList<RuleCheckResult> Check(string filePath, string content)
        {
            var results = new List<RuleCheckResult>();
            if (!XamlActivityHelper.TryParse(content, out var doc) || doc == null || doc.Root == null) return results;

            foreach (var el in doc.Root.DescendantsAndSelf())
            {
                // Only inspect <If> activities
                if (!string.Equals(XamlActivityHelper.GetLocalName(el), "If", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Find <If.Then> and <If.Else> property containers
                var thenBranch = el.Elements().FirstOrDefault(e =>
                    string.Equals(e.Name.LocalName, "If.Then", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(e.Name.LocalName, "Then", StringComparison.OrdinalIgnoreCase));

                var elseBranch = el.Elements().FirstOrDefault(e =>
                    string.Equals(e.Name.LocalName, "If.Else", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(e.Name.LocalName, "Else", StringComparison.OrdinalIgnoreCase));

                bool hasThenActivities = HasActivities(thenBranch);
                bool hasElseActivities = HasActivities(elseBranch);

                // Both Then and Else must have executable activities
                if (!hasThenActivities || !hasElseActivities)
                {
                    string display = XamlActivityHelper.GetDisplayName(el);
                    if (string.Equals(display, "If", StringComparison.OrdinalIgnoreCase))
                    {
                        var idRef = el.Attributes().FirstOrDefault(a =>
                            string.Equals(a.Name.LocalName, "IdRef", StringComparison.OrdinalIgnoreCase))?.Value;
                        if (!string.IsNullOrWhiteSpace(idRef))
                        {
                            display = idRef;
                        }
                    }

                    string emptyDetails = (!hasThenActivities && !hasElseActivities)
                        ? "both Then and Else parts are empty"
                        : (!hasThenActivities ? "Then part is empty" : "Else part is empty");

                    results.Add(new RuleCheckResult
                    {
                        RuleId = RuleId,
                        RuleName = RuleName,
                        Level = RuleLevel.Warning,
                        Message = $"The following activity: {display} have empty if/else activity ({emptyDetails}).",
                        FilePath = filePath,
                        Recommendation = DefaultRecommendation
                    });
                }
            }

            return results;
        }

        /// <summary>
        /// Checks whether a Then or Else branch element contains at least one executable activity.
        /// Ignores ViewState metadata (dictionaries, layout booleans), property elements, variable/argument wrappers,
        /// and empty containers (like Sequences or Flowcharts that have no inner activities).
        /// </summary>
        private static bool HasActivities(XElement? branchElement)
        {
            if (branchElement == null) return false;

            return branchElement.Descendants().Any(IsExecutableActivity);
        }

        private static bool IsExecutableActivity(XElement el)
        {
            // 1. Must not be a property element (e.g. If.Then, Sequence.Variables, ViewStateService.ViewState)
            if (XamlActivityHelper.IsPropertyElement(el))
                return false;

            // 2. Must not be part of ViewState metadata (e.g. sap:WorkflowViewStateService.ViewState, Dictionary, Boolean)
            if (IsUnderViewState(el))
                return false;

            // 3. Must not be a variable or argument declaration
            string ln = XamlActivityHelper.GetLocalName(el);
            if (string.Equals(ln, "Variable", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ln, "VariableList", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ln, "InArgument", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ln, "OutArgument", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ln, "InOutArgument", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // 4. Must not be a structural container itself (Sequence, Flowchart, StateMachine)
            if (string.Equals(ln, "Sequence", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ln, "Flowchart", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ln, "StateMachine", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // 5. Must not be other structural/primitive XAML wrappers (Dictionary, Literal, etc.)
            if (XamlActivityHelper.IsStructural(el))
                return false;

            return true;
        }

        private static bool IsUnderViewState(XElement e)
        {
            for (XElement? cur = e; cur != null; cur = cur.Parent)
            {
                string ln = cur.Name.LocalName;
                if (ln.IndexOf("ViewState", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    cur.Name.NamespaceName.IndexOf("presentation", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
