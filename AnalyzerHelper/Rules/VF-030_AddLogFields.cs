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
    /// VF-030 — AddLogFields
    /// Validates that Main workflows (containing "Entry Sequence" or worker/loader main files)
    /// and SetTransactionStatus workflows have the required AddLogFields activities with
    /// mandatory arguments, correct types, and required values.
    /// </summary>
    public sealed class AddLogFields : IAnalyzerRule
    {
        public string RuleId => "VF-030";
        public string RuleName => "AddLogFields";
        public string DefaultRecommendation => "Checking IAP Add log fileds activity at the beginning of worker or loader with the correct arguments.";
        public bool RequiresUserInteraction => false;

        private static readonly string[] MainMandatoryArgs = { "VOISLM", "projectName", "processIdentifier", "processStage", "function", "PPMID" };
        private static readonly string[] StatusMandatoryArgs = { "executionTime", "Case Duration", "exceptionMessage", "ItemEndTime", "BE_Category", "TransactionStatus" };

        public IReadOnlyList<RuleCheckResult> Check(string filePath, string content)
        {
            var results = new List<RuleCheckResult>();
            if (!XamlActivityHelper.TryParse(content, out var doc) || doc?.Root == null)
                return results;

            string fileName = Path.GetFileName(filePath);
            string workflowName = Path.GetFileNameWithoutExtension(filePath);

            bool isSetTransactionStatus = fileName.IndexOf("SetTransactionStatus", StringComparison.OrdinalIgnoreCase) >= 0;

            bool hasEntrySequence = doc.Descendants().Any(e =>
                e.Attribute("DisplayName")?.Value?.IndexOf("Entry Sequence", StringComparison.OrdinalIgnoreCase) >= 0);

            bool isMainFile = !isSetTransactionStatus && (
                hasEntrySequence ||
                fileName.EndsWith("worker.xaml", StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith("loader.xaml", StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith("loaderworker.xaml", StringComparison.OrdinalIgnoreCase));

            if (!isSetTransactionStatus && !isMainFile)
            {
                // This rule only validates Main workflows and SetTransactionStatus workflows
                return results;
            }

            var addLogFieldActivities = doc.Descendants()
                .Where(e => string.Equals(e.Name.LocalName, "AddLogFields", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (isMainFile)
            {
                ValidateMainWorkflow(filePath, workflowName, fileName, addLogFieldActivities, results);
            }
            else if (isSetTransactionStatus)
            {
                ValidateSetTransactionStatusWorkflow(filePath, workflowName, addLogFieldActivities, results);
            }

            return results;
        }

        private void ValidateMainWorkflow(
            string filePath,
            string workflowName,
            string fileName,
            List<XElement> addLogFieldActivities,
            List<RuleCheckResult> results)
        {
            if (addLogFieldActivities.Count == 0)
            {
                results.Add(CreateErrorResult(filePath,
                    $"The following workflow: {workflowName} does not have the mandatory add log fields activity."));
                return;
            }

            // Find the candidate main AddLogFields activity (the one with the most matching mandatory arguments)
            var mainLogActivity = addLogFieldActivities
                .OrderByDescending(act => GetFieldArguments(act).Count(arg =>
                    MainMandatoryArgs.Contains(GetArgumentKey(arg), StringComparer.OrdinalIgnoreCase)))
                .First();

            var args = GetFieldArguments(mainLogActivity);
            var argDict = new Dictionary<string, XElement>(StringComparer.OrdinalIgnoreCase);
            foreach (var arg in args)
            {
                string key = GetArgumentKey(arg);
                if (!string.IsNullOrEmpty(key) && !argDict.ContainsKey(key))
                {
                    argDict[key] = arg;
                }
            }

            // Check if any mandatory arguments are missing
            var missingArgs = MainMandatoryArgs.Where(req => !argDict.ContainsKey(req)).ToList();
            if (missingArgs.Count > 0)
            {
                results.Add(CreateErrorResult(filePath,
                    $"The following workflow: {workflowName} has empty/missing mandatory arguments in the add log fields activity {string.Join("/", MainMandatoryArgs)}."));
            }

            // Validate each present mandatory argument
            foreach (var req in MainMandatoryArgs)
            {
                if (argDict.TryGetValue(req, out var argElement))
                {
                    string type = GetArgumentType(argElement);
                    if (type.IndexOf("String", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        results.Add(CreateErrorResult(filePath,
                            $"The following workflow: {workflowName} has Wrong argument Type {req}."));
                    }

                    string val = GetArgumentExpressionOrValue(argElement);
                    if (string.IsNullOrWhiteSpace(val))
                    {
                        results.Add(CreateErrorResult(filePath,
                            $"The following workflow: {workflowName} has empty mandatory argument {req}."));
                    }
                    else if (string.Equals(req, "processStage", StringComparison.OrdinalIgnoreCase))
                    {
                        if (fileName.IndexOf("worker", StringComparison.OrdinalIgnoreCase) >= 0 &&
                            val.IndexOf("Worker", StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            results.Add(CreateErrorResult(filePath,
                                "The Main workflow: have empty/wrong processStage value"));
                        }
                    }
                }
            }

            // Check Get Transaction AddLogFields if present
            foreach (var act in addLogFieldActivities)
            {
                if (ReferenceEquals(act, mainLogActivity)) continue;
                var actArgs = GetFieldArguments(act);
                foreach (var arg in actArgs)
                {
                    string key = GetArgumentKey(arg);
                    if (key.IndexOf("TransactionKey", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        string val = GetArgumentExpressionOrValue(arg);
                        if (val.IndexOf("qitransactionItem.ItemKey.ToString", StringComparison.OrdinalIgnoreCase) < 0 &&
                            val.IndexOf("TransactionItem.ItemKey.ToString", StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            results.Add(CreateErrorResult(filePath,
                                $"The following workflow: Get Transaction has empty/wrong mandatory argument value: {key}"));
                        }
                    }
                }
            }
        }

        private void ValidateSetTransactionStatusWorkflow(
            string filePath,
            string workflowName,
            List<XElement> addLogFieldActivities,
            List<RuleCheckResult> results)
        {
            if (addLogFieldActivities.Count < 3)
            {
                results.Add(CreateErrorResult(filePath,
                    $"The following workflow: {workflowName} has only {addLogFieldActivities.Count} out of 3 add log fields activities"));
            }

            foreach (var activity in addLogFieldActivities)
            {
                string actDisplayName = XamlActivityHelper.GetDisplayName(activity);
                var args = GetFieldArguments(activity);

                if (args.Count == 0 || args.Count < StatusMandatoryArgs.Length)
                {
                    results.Add(CreateErrorResult(filePath,
                        $"The following workflow: {workflowName} has '{actDisplayName}' Activity that has empty/missing mandatory arguments '{string.Join("/", StatusMandatoryArgs)}'."));
                    continue;
                }

                int fieldsCounter = 0;
                foreach (var arg in args)
                {
                    string key = GetArgumentKey(arg);
                    if (StatusMandatoryArgs.Contains(key, StringComparer.OrdinalIgnoreCase))
                    {
                        fieldsCounter++;
                        string type = GetArgumentType(arg);
                        string val = GetArgumentExpressionOrValue(arg);

                        if (IsArgumentCompletelyEmpty(arg))
                        {
                            results.Add(CreateErrorResult(filePath,
                                $"The following workflow: {workflowName} has '{actDisplayName}' Activity that has empty mandatory argument '{key}'."));
                        }

                        if (key.IndexOf("TransactionStatus", StringComparison.OrdinalIgnoreCase) >= 0 &&
                            !string.IsNullOrWhiteSpace(val) &&
                            val.IndexOf("strAnalyticsMessage", StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            results.Add(CreateErrorResult(filePath,
                                "The settransaction status workflow: have empty/wrong TransactionStatus value"));
                        }

                        if (key.IndexOf("BE_Category", StringComparison.OrdinalIgnoreCase) >= 0 &&
                            !string.IsNullOrWhiteSpace(val) &&
                            val.IndexOf("BE_Category", StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            results.Add(CreateErrorResult(filePath,
                                "The settransaction status workflow: have empty/wrong BE_Category value"));
                        }

                        if (string.Equals(key, "ItemEndTime", StringComparison.OrdinalIgnoreCase) &&
                            !string.IsNullOrWhiteSpace(val) &&
                            val.IndexOf("MMM d,yyyy @ HH:mm:ss", StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            results.Add(CreateErrorResult(filePath,
                                $"The following workflow: {workflowName} has '{actDisplayName}' Activity that has empty/wrong mandatory argument '{key}'."));
                        }

                        if (string.Equals(key, "executionTime", StringComparison.OrdinalIgnoreCase) &&
                            type.IndexOf("Double", StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            results.Add(CreateErrorResult(filePath,
                                $"The following workflow: {workflowName} has '{actDisplayName}' Activity that has Wrong argument Type '{key}'."));
                        }
                        else if ((string.Equals(key, "exceptionMessage", StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(key, "Case Duration", StringComparison.OrdinalIgnoreCase)) &&
                                 type.IndexOf("String", StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            results.Add(CreateErrorResult(filePath,
                                $"The following workflow: {workflowName} has '{actDisplayName}' Activity that has Wrong argument Type '{key}'."));
                        }
                    }
                }

                if (fieldsCounter < 3)
                {
                    results.Add(CreateErrorResult(filePath,
                        $"The following Workflow {workflowName}: has '{actDisplayName}' activity that is missing one or more of the Add log fields Arguments, please refer to the latest framework template"));
                }
            }
        }

        private static List<XElement> GetFieldArguments(XElement addLogFieldsElement)
        {
            var fieldsContainer = addLogFieldsElement.Elements().FirstOrDefault(e =>
                e.Name.LocalName.EndsWith(".Fields", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(e.Name.LocalName, "Fields", StringComparison.OrdinalIgnoreCase));

            var searchContainer = fieldsContainer ?? addLogFieldsElement;

            return searchContainer.DescendantsAndSelf()
                .Where(el => el.Name.LocalName.EndsWith("Argument", StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        private static string GetArgumentKey(XElement argElem)
        {
            var keyAttr = argElem.Attributes().FirstOrDefault(a => string.Equals(a.Name.LocalName, "Key", StringComparison.OrdinalIgnoreCase));
            return keyAttr?.Value ?? string.Empty;
        }

        private static string GetArgumentType(XElement argElem)
        {
            var typeAttr = argElem.Attributes().FirstOrDefault(a => string.Equals(a.Name.LocalName, "TypeArguments", StringComparison.OrdinalIgnoreCase));
            return typeAttr?.Value ?? string.Empty;
        }

        private static bool IsArgumentCompletelyEmpty(XElement argElem)
        {
            if (!argElem.HasElements)
            {
                return string.IsNullOrWhiteSpace(argElem.Value);
            }

            var literal = argElem.Descendants().FirstOrDefault(e => string.Equals(e.Name.LocalName, "Literal", StringComparison.OrdinalIgnoreCase));
            if (literal != null)
            {
                // A Literal tag is present
                return false;
            }

            var exprNode = argElem.Descendants().FirstOrDefault(e =>
                e.Name.LocalName.EndsWith("Value", StringComparison.OrdinalIgnoreCase) ||
                e.Name.LocalName.EndsWith("Reference", StringComparison.OrdinalIgnoreCase));
            if (exprNode != null)
            {
                var exprAttr = exprNode.Attributes().FirstOrDefault(a => string.Equals(a.Name.LocalName, "ExpressionText", StringComparison.OrdinalIgnoreCase));
                if (exprAttr != null)
                {
                    return string.IsNullOrWhiteSpace(exprAttr.Value);
                }
                return string.IsNullOrWhiteSpace(exprNode.Value);
            }

            return string.IsNullOrWhiteSpace(argElem.Value);
        }

        private static string GetArgumentExpressionOrValue(XElement argElem)
        {
            var exprNode = argElem.Descendants().FirstOrDefault(e =>
                e.Name.LocalName.EndsWith("Value", StringComparison.OrdinalIgnoreCase) ||
                e.Name.LocalName.EndsWith("Reference", StringComparison.OrdinalIgnoreCase));
            if (exprNode != null)
            {
                var exprAttr = exprNode.Attributes().FirstOrDefault(a => string.Equals(a.Name.LocalName, "ExpressionText", StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(exprAttr?.Value)) return exprAttr!.Value;
                if (!string.IsNullOrWhiteSpace(exprNode.Value)) return exprNode.Value;
            }

            var literal = argElem.Descendants().FirstOrDefault(e => string.Equals(e.Name.LocalName, "Literal", StringComparison.OrdinalIgnoreCase));
            if (literal != null)
            {
                var valAttr = literal.Attributes().FirstOrDefault(a => string.Equals(a.Name.LocalName, "Value", StringComparison.OrdinalIgnoreCase));
                if (valAttr != null) return valAttr.Value;
                return literal.Value;
            }

            return argElem.Value?.Trim() ?? string.Empty;
        }

        private RuleCheckResult CreateErrorResult(string filePath, string message) =>
            new RuleCheckResult
            {
                RuleId = RuleId,
                RuleName = RuleName,
                Level = RuleLevel.Error,
                Message = message,
                FilePath = filePath,
                Recommendation = "Please add the mandatory add log fields activity with the mandatory arguments",
                RequiresUserInteraction = false
            };
    }
}
