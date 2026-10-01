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
    /// VF-038 — Branches Logging (Activity Level)
    /// Validates that all decision branches (If, Switch, FlowDecision, FlowSwitch) and loops
    /// (While, DoWhile, InterruptibleWhile, InterruptibleDoWhile, ForEach, ForEachRow)
    /// start with a logging activity (e.g. Info_Log, Message_Log, Error_Log).
    /// Legacy rule: VodafoneActivitiesCustomRules\BranchesLogMessages.cs
    /// </summary>
    public sealed class BranchesLogMessages : IAnalyzerRule
    {
        public string RuleId => "VF-038";
        public string RuleName => "Branches Logging";
        public string DefaultRecommendation => "Add log message at the beginning of every branch.";
        public bool RequiresUserInteraction => false;

        private static readonly string[] BranchActivityNames = new[]
        {
            "If",
            "InterruptibleWhile",
            "InterruptibleDoWhile",
            "While",
            "DoWhile",
            "ForEach",
            "ForEachRow",
            "Switch",
            "FlowDecision",
            "FlowSwitch"
        };

        private static readonly XNamespace XNs = "http://schemas.microsoft.com/winfx/2006/xaml";

        public IReadOnlyList<RuleCheckResult> Check(string filePath, string content)
        {
            var results = new List<RuleCheckResult>();

            if (string.IsNullOrWhiteSpace(content))
                return results;

            if (!filePath.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
                return results;

            XDocument doc;
            try
            {
                doc = XDocument.Parse(content, LoadOptions.PreserveWhitespace);
            }
            catch
            {
                return results;
            }

            if (doc.Root == null)
                return results;

            var branchActivities = doc.Descendants().Where(IsBranchActivity).ToList();

            foreach (var act in branchActivities)
            {
                string dispName = XamlActivityHelper.GetDisplayName(act);
                string ln = act.Name.LocalName;

                if (ln.Equals("If", StringComparison.OrdinalIgnoreCase))
                {
                    CheckIfActivity(act, dispName, filePath, results);
                }
                else if (ln.IndexOf("While", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    CheckWhileActivity(act, dispName, filePath, results);
                }
                else if (ln.IndexOf("ForEach", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    CheckForEachActivity(act, dispName, filePath, results);
                }
                else if (ln.Equals("Switch", StringComparison.OrdinalIgnoreCase))
                {
                    CheckSwitchActivity(act, dispName, filePath, results);
                }
                else if (ln.Equals("FlowDecision", StringComparison.OrdinalIgnoreCase))
                {
                    CheckFlowDecisionActivity(doc, act, dispName, filePath, results);
                }
                else if (ln.Equals("FlowSwitch", StringComparison.OrdinalIgnoreCase))
                {
                    CheckFlowSwitchActivity(doc, act, dispName, filePath, results);
                }
            }

            return results;
        }

        private static bool IsBranchActivity(XElement el)
        {
            if (XamlActivityHelper.IsPropertyElement(el))
                return false;

            string localName = el.Name.LocalName;
            return BranchActivityNames.Any(name =>
                string.Equals(name, localName, StringComparison.OrdinalIgnoreCase));
        }

        private void CheckIfActivity(XElement ifEl, string dispName, string filePath, List<RuleCheckResult> results)
        {
            var thenProp = ifEl.Elements().FirstOrDefault(e => e.Name.LocalName.EndsWith(".Then", StringComparison.OrdinalIgnoreCase));
            var elseProp = ifEl.Elements().FirstOrDefault(e => e.Name.LocalName.EndsWith(".Else", StringComparison.OrdinalIgnoreCase));

            // Check Then branch
            CheckBranchContainer(thenProp, dispName, "Then", filePath, results);

            // Check Else branch (only if defined in XAML)
            if (elseProp != null)
            {
                CheckBranchContainer(elseProp, dispName, "Else", filePath, results);
            }
        }

        private void CheckWhileActivity(XElement whileEl, string dispName, string filePath, List<RuleCheckResult> results)
        {
            // While body can be in <While.Body> or direct Sequence/activity child
            var bodyContainer = whileEl.Elements().FirstOrDefault(e =>
                e.Name.LocalName.EndsWith(".Body", StringComparison.OrdinalIgnoreCase)) ?? whileEl;

            var firstAct = GetFirstActivity(bodyContainer);
            if (firstAct == null)
            {
                AddResult(results, filePath, string.Format("The following activity: {0} has empty branch/es. should start with Log Activities", dispName));
            }
            else if (!IsLogActivity(firstAct))
            {
                AddResult(results, filePath, string.Format("The following activity: {0} doeasn't start with Log Activity inside its branches.", dispName));
            }
        }

        private void CheckForEachActivity(XElement forEachEl, string dispName, string filePath, List<RuleCheckResult> results)
        {
            var bodyContainer = forEachEl.Elements().FirstOrDefault(e =>
                e.Name.LocalName.EndsWith(".Body", StringComparison.OrdinalIgnoreCase)) ?? forEachEl;

            var firstAct = GetFirstActivity(bodyContainer);
            if (firstAct == null)
            {
                AddResult(results, filePath, string.Format("The following activity: {0} has empty branch/es. should start with Log Activities", dispName));
            }
            else if (!IsLogActivity(firstAct))
            {
                AddResult(results, filePath, string.Format("The following activity: {0} doeasn't start with Log Activity inside its branches.", dispName));
            }
        }

        private void CheckSwitchActivity(XElement switchEl, string dispName, string filePath, List<RuleCheckResult> results)
        {
            var defaultProp = switchEl.Elements().FirstOrDefault(e => e.Name.LocalName.EndsWith(".Default", StringComparison.OrdinalIgnoreCase));
            if (defaultProp != null)
            {
                CheckBranchContainer(defaultProp, dispName, "Default", filePath, results);
            }

            var cases = switchEl.Elements().Where(e =>
                e.Attribute(XNs + "Key") != null || e.Attribute("Key") != null || e.Name.LocalName.Equals("Case", StringComparison.OrdinalIgnoreCase));

            foreach (var c in cases)
            {
                var firstAct = GetFirstActivity(c);
                if (firstAct == null)
                {
                    AddResult(results, filePath, string.Format("The following activity: {0} has empty branch/es. should start with Log Activities", dispName));
                }
                else if (!IsLogActivity(firstAct))
                {
                    AddResult(results, filePath, string.Format("The following activity: {0} doeasn't start with Log Activity inside its branches.", dispName));
                }
            }
        }

        private void CheckFlowDecisionActivity(XDocument doc, XElement flowDecEl, string dispName, string filePath, List<RuleCheckResult> results)
        {
            var trueProp = flowDecEl.Elements().FirstOrDefault(e => e.Name.LocalName.EndsWith(".True", StringComparison.OrdinalIgnoreCase));
            var falseProp = flowDecEl.Elements().FirstOrDefault(e => e.Name.LocalName.EndsWith(".False", StringComparison.OrdinalIgnoreCase));

            CheckFlowBranch(doc, trueProp, dispName, filePath, results);
            CheckFlowBranch(doc, falseProp, dispName, filePath, results);
        }

        private void CheckFlowSwitchActivity(XDocument doc, XElement flowSwitchEl, string dispName, string filePath, List<RuleCheckResult> results)
        {
            var defaultProp = flowSwitchEl.Elements().FirstOrDefault(e => e.Name.LocalName.EndsWith(".Default", StringComparison.OrdinalIgnoreCase));
            if (defaultProp != null)
            {
                CheckFlowBranch(doc, defaultProp, dispName, filePath, results);
            }

            var cases = flowSwitchEl.Elements().Where(e =>
                e.Attribute(XNs + "Key") != null || e.Attribute("Key") != null);

            foreach (var c in cases)
            {
                CheckFlowStepBranch(doc, c, dispName, filePath, results);
            }
        }

        private void CheckFlowBranch(XDocument doc, XElement? branchContainer, string dispName, string filePath, List<RuleCheckResult> results)
        {
            if (branchContainer == null)
            {
                AddResult(results, filePath, string.Format("The following activity: {0} has empty branch/es. should start with Log Activities", dispName));
                return;
            }

            var first = branchContainer.Elements().FirstOrDefault(e => !IsNonActivity(e));
            if (first == null)
            {
                AddResult(results, filePath, string.Format("The following activity: {0} has empty branch/es. should start with Log Activities", dispName));
                return;
            }

            CheckFlowStepBranch(doc, first, dispName, filePath, results);
        }

        private void CheckFlowStepBranch(XDocument doc, XElement targetEl, string dispName, string filePath, List<RuleCheckResult> results)
        {
            // Resolve x:Reference if present
            if (targetEl.Name.LocalName.Equals("Reference", StringComparison.OrdinalIgnoreCase))
            {
                string refName = targetEl.Value.Trim();
                if (!string.IsNullOrEmpty(refName))
                {
                    var resolved = doc.Descendants().FirstOrDefault(e =>
                        string.Equals(e.Attribute(XNs + "Name")?.Value, refName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(e.Attribute("Name")?.Value, refName, StringComparison.OrdinalIgnoreCase));
                    if (resolved != null)
                        targetEl = resolved;
                }
            }

            var act = GetFirstActivity(targetEl);
            if (act == null)
            {
                AddResult(results, filePath, string.Format("The following activity: {0} has empty branch/es. should start with Log Activities", dispName));
            }
            else if (!IsLogActivity(act))
            {
                AddResult(results, filePath, string.Format("The following activity: {0} doeasn't start with Log Activity inside its branches.", dispName));
            }
        }

        private void CheckBranchContainer(XElement? container, string dispName, string branchLabel, string filePath, List<RuleCheckResult> results)
        {
            if (container == null)
            {
                AddResult(results, filePath, string.Format("The following activity: {0} has empty branch/es. should start with Log Activities", dispName));
                return;
            }

            var firstAct = GetFirstActivity(container);
            if (firstAct == null)
            {
                AddResult(results, filePath, string.Format("The following activity: {0} has empty branch/es. should start with Log Activities", dispName));
            }
            else if (!IsLogActivity(firstAct))
            {
                AddResult(results, filePath, string.Format("The following activity: {0} doeasn't start with Log Activity inside its branches.", dispName));
            }
        }

        private static XElement? GetFirstActivity(XElement container)
        {
            if (container == null)
                return null;

            var child = container.Elements().FirstOrDefault(e => !XamlActivityHelper.IsPropertyElement(e) && !IsNonActivity(e));
            if (child == null)
                return null;

            // ActivityAction wrapper (e.g. inside loop bodies)
            if (child.Name.LocalName.Equals("ActivityAction", StringComparison.OrdinalIgnoreCase))
            {
                child = child.Elements().FirstOrDefault(e => !XamlActivityHelper.IsPropertyElement(e) && !IsNonActivity(e));
                if (child == null)
                    return null;
            }

            // FlowStep wrapper
            if (child.Name.LocalName.Equals("FlowStep", StringComparison.OrdinalIgnoreCase))
            {
                var stepAct = child.Elements().FirstOrDefault(e => !XamlActivityHelper.IsPropertyElement(e) && !IsNonActivity(e));
                return stepAct ?? child;
            }

            // Sequence wrapper
            if (child.Name.LocalName.Equals("Sequence", StringComparison.OrdinalIgnoreCase))
            {
                var seqChild = child.Elements().FirstOrDefault(e => !XamlActivityHelper.IsPropertyElement(e) && !IsNonActivity(e));
                return seqChild ?? child; // returns empty sequence itself if no children
            }

            return child;
        }

        private static bool IsLogActivity(XElement? el)
        {
            if (el == null)
                return false;

            string ln = el.Name.LocalName;
            return ln.EndsWith("_Log", StringComparison.OrdinalIgnoreCase) ||
                   ln.EndsWith("_log", StringComparison.OrdinalIgnoreCase) ||
                   ln.IndexOf("LogMessage", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsNonActivity(XElement e)
        {
            string ln = e.Name.LocalName;
            return ln.EndsWith(".Variables", StringComparison.OrdinalIgnoreCase) ||
                   ln.EndsWith(".ViewState", StringComparison.OrdinalIgnoreCase) ||
                   ln.StartsWith("WorkflowViewState", StringComparison.OrdinalIgnoreCase) ||
                   ln.StartsWith("ViewStateService", StringComparison.OrdinalIgnoreCase) ||
                   ln.Equals("Variable", StringComparison.OrdinalIgnoreCase) ||
                   ln.Equals("ActivityAction", StringComparison.OrdinalIgnoreCase);
        }

        private void AddResult(List<RuleCheckResult> results, string filePath, string message)
        {
            if (!results.Any(r => r.FilePath == filePath && r.Message == message))
            {
                results.Add(new RuleCheckResult
                {
                    RuleId = RuleId,
                    RuleName = RuleName,
                    Level = RuleLevel.Warning,
                    Message = message,
                    FilePath = filePath,
                    Recommendation = DefaultRecommendation,
                    RequiresUserInteraction = RequiresUserInteraction
                });
            }
        }
    }
}
