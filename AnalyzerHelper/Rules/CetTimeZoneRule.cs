using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using AnalyzerHelper.Interfaces;
using AnalyzerHelper.Models;

namespace AnalyzerHelper.Rules
{
    /// <summary>
    /// VF-053: In SetTransactionStatus workflows, under Try catch → Work Sequence → Set transaction Flowchart,
    /// "Set Start time" / "Set end time" assigns must use CET conversion expressions (same checks as Studio rule).
    /// Auto-fix updates Assign.Value when those activities exist; missing structure cannot be generated.
    /// Reference: VodafoneWorkFlowsCustomRules/CheckCETTimeZone.cs
    /// </summary>
    public sealed class CetTimeZoneRule : IAnalyzerRuleWithFix
    {
        public string RuleId => "VF-053";
        public string RuleName => "CETTimeZone";
        public string DefaultRecommendation =>
            "Use TimeZoneInfo.ConvertTime with FindSystemTimeZoneById for CET on Set Start time (in_dtmTransactionStartTime) " +
            "and Set end time (System.DateTime.Now). Apply fix to set the standard expression when the Assign activities exist.";
        public bool RequiresUserInteraction => false;

        /// <summary>Substring checks aligned with VodafoneWorkFlowsCustomRules CheckCETTimeZone (case-sensitive Contains).</summary>
        private const string StartExpressionMarker =
            "TimeZoneInfo.ConvertTime(in_dtmTransactionStartTime,TimeZoneInfo.FindSystemTimeZoneById";

        private const string EndExpressionMarker =
            "TimeZoneInfo.ConvertTime(System.DateTime.Now , TimeZoneInfo.FindSystemTimeZoneById";

        private const string StartExpressionFix =
            "[TimeZoneInfo.ConvertTime(in_dtmTransactionStartTime,TimeZoneInfo.FindSystemTimeZoneById(\"Central European Standard Time\"))]";

        private const string EndExpressionFix =
            "[TimeZoneInfo.ConvertTime(System.DateTime.Now , TimeZoneInfo.FindSystemTimeZoneById(\"Central European Standard Time\"))]";

        public IReadOnlyList<RuleCheckResult> Check(string filePath, string content)
        {
            var results = new List<RuleCheckResult>();
            if (string.IsNullOrWhiteSpace(content) || !IsSetTransactionStatusWorkflow(filePath))
                return results;

            if (!TryLoad(content, out XDocument doc))
                return results;

            XElement? main = FindMainSequenceOrFlowchart(doc);
            if (main == null)
                return results;

            bool startCetTimeExists = false;
            bool endCetTimeExists = false;

            foreach (var tryCatch in EnumerateTryCatchBlocks(main))
            {
                if (!TryFindCetFlowchart(tryCatch, out XElement? flowchart) || flowchart == null)
                    continue;

                bool startFound = TryFindCetAssign(flowchart, "Set Start time", out XElement? startAssign);
                bool endFound = TryFindCetAssign(flowchart, "Set end time", out XElement? endAssign);

                if (startFound)
                    startCetTimeExists = true;
                if (endFound)
                    endCetTimeExists = true;

                if (startFound)
                {
                    string? startExpr = GetAssignValueExpression(startAssign!);
                    if (string.IsNullOrEmpty(startExpr) || !startExpr.Contains(StartExpressionMarker, StringComparison.Ordinal))
                    {
                        results.Add(MakeResult(filePath,
                            "Activity 'Set Start time' must set Value using CET conversion (TimeZoneInfo.ConvertTime with in_dtmTransactionStartTime and FindSystemTimeZoneById)."));
                    }
                }

                if (endFound)
                {
                    string? endExpr = GetAssignValueExpression(endAssign!);
                    if (string.IsNullOrEmpty(endExpr) || !endExpr.Contains(EndExpressionMarker, StringComparison.Ordinal))
                    {
                        results.Add(MakeResult(filePath,
                            "Activity 'Set end time' must set Value using CET conversion (TimeZoneInfo.ConvertTime with System.DateTime.Now and FindSystemTimeZoneById)."));
                    }
                }
            }

            if (!startCetTimeExists)
            {
                results.Add(MakeResult(filePath,
                    "The following workflow: Set transaction status don't contain the Set Start time to CET activity"));
            }
            else if (!endCetTimeExists)
            {
                results.Add(MakeResult(filePath,
                    "The following workflow: Set transaction status don't contain the Set End time to CET activity"));
            }

            return results;
        }

        public bool DefineAndFix(string filePath, string content, out string newContent)
        {
            newContent = content;
            if (string.IsNullOrWhiteSpace(content) || !IsSetTransactionStatusWorkflow(filePath))
                return false;

            if (!TryLoad(content, out XDocument doc))
                return false;

            XElement? main = FindMainSequenceOrFlowchart(doc);
            if (main == null)
                return false;

            bool changed = false;
            foreach (var tryCatch in EnumerateTryCatchBlocks(main))
            {
                if (!TryFindCetFlowchart(tryCatch, out XElement? flowchart) || flowchart == null)
                    continue;

                if (TryFindCetAssign(flowchart, "Set Start time", out XElement? startAssign) && startAssign != null)
                {
                    string? startExpr = GetAssignValueExpression(startAssign);
                    if (string.IsNullOrEmpty(startExpr) || !startExpr.Contains(StartExpressionMarker, StringComparison.Ordinal))
                        changed |= EnsureAssignValueExpression(startAssign, StartExpressionFix);
                }

                if (TryFindCetAssign(flowchart, "Set end time", out XElement? endAssign) && endAssign != null)
                {
                    string? endExpr = GetAssignValueExpression(endAssign);
                    if (string.IsNullOrEmpty(endExpr) || !endExpr.Contains(EndExpressionMarker, StringComparison.Ordinal))
                        changed |= EnsureAssignValueExpression(endAssign, EndExpressionFix);
                }
            }

            if (!changed)
                return false;

            newContent = Serialise(doc, content);
            return newContent != content;
        }

        private RuleCheckResult MakeResult(string filePath, string message) => new RuleCheckResult
        {
            RuleId = RuleId,
            RuleName = RuleName,
            Level = RuleLevel.Error,
            Message = message,
            FilePath = filePath,
            Recommendation = DefaultRecommendation,
            RequiresUserInteraction = RequiresUserInteraction
        };

        private static bool IsSetTransactionStatusWorkflow(string filePath)
        {
            string name = Path.GetFileName(filePath);
            if (string.IsNullOrEmpty(name))
                return false;
            string last = name.Split('_').Last();
            return last.Equals("settransactionstatus.xaml", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryLoad(string content, out XDocument doc)
        {
            try
            {
                doc = XDocument.Parse(content, LoadOptions.PreserveWhitespace);
                return true;
            }
            catch
            {
                doc = null!;
                return false;
            }
        }

        private static XElement? FindMainSequenceOrFlowchart(XDocument doc)
        {
            return doc.Root?.Elements()
                .FirstOrDefault(e =>
                    e.Name.LocalName == "Sequence" ||
                    e.Name.LocalName == "Flowchart");
        }

        private static IEnumerable<XElement> EnumerateTryCatchBlocks(XElement main)
        {
            foreach (var child in main.Elements())
            {
                if (child.Name.LocalName != "TryCatch")
                    continue;
                string dn = GetDisplayName(child);
                if (dn.IndexOf("try catch", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                yield return child;
            }
        }

        private static bool TryFindCetFlowchart(XElement tryCatch, out XElement? flowchart)
        {
            flowchart = null;
            XElement? tryBody = tryCatch.Elements().FirstOrDefault(e => e.Name.LocalName == "TryCatch.Try");
            if (tryBody == null)
                return false;

            foreach (var seq in tryBody.Descendants().Where(e => e.Name.LocalName == "Sequence"))
            {
                if (GetDisplayName(seq).IndexOf("work sequence", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                var fc = seq.Descendants().FirstOrDefault(e =>
                    e.Name.LocalName == "Flowchart" &&
                    GetDisplayName(e).IndexOf("set transaction flowchart", StringComparison.OrdinalIgnoreCase) >= 0);
                if (fc != null)
                {
                    flowchart = fc;
                    return true;
                }
            }

            return false;
        }

        private static bool TryFindCetAssign(XElement flowchart, string displayNameContains, out XElement? assign)
        {
            assign = flowchart.Descendants()
                .FirstOrDefault(e =>
                    e.Name.LocalName == "Assign" &&
                    GetDisplayName(e).IndexOf(displayNameContains, StringComparison.OrdinalIgnoreCase) >= 0);
            return assign != null;
        }

        private static string GetDisplayName(XElement el)
        {
            var a = el.Attributes().FirstOrDefault(x => x.Name.LocalName == "DisplayName");
            return a?.Value ?? "";
        }

        private static XElement? GetAssignValueContainer(XElement assign)
        {
            return assign.Elements().FirstOrDefault(e => e.Name.LocalName == "Assign.Value");
        }

        private static string? GetAssignValueExpression(XElement assign)
        {
            XElement? valueEl = GetAssignValueContainer(assign);
            if (valueEl == null)
                return null;
            XElement? inArg = valueEl.Elements().FirstOrDefault(e => e.Name.LocalName == "InArgument");
            return inArg?.Value.Trim();
        }

        private static bool EnsureAssignValueExpression(XElement assign, string expressionWithBrackets)
        {
            XNamespace ns = assign.Name.Namespace;
            XNamespace xNs = "http://schemas.microsoft.com/winfx/2006/xaml";
            XElement? valueEl = GetAssignValueContainer(assign);
            if (valueEl == null)
            {
                valueEl = new XElement(ns + "Assign.Value",
                    new XElement(ns + "InArgument",
                        new XAttribute(xNs + "TypeArguments", "x:DateTime"),
                        expressionWithBrackets));
                assign.Add(valueEl);
                return true;
            }

            XElement? inArg = valueEl.Elements().FirstOrDefault(e => e.Name.LocalName == "InArgument");
            if (inArg == null)
            {
                inArg = new XElement(ns + "InArgument",
                    new XAttribute(xNs + "TypeArguments", "x:DateTime"),
                    expressionWithBrackets);
                valueEl.Add(inArg);
                return true;
            }

            if (string.Equals(inArg.Value.Trim(), expressionWithBrackets.Trim(), StringComparison.Ordinal))
                return false;

            inArg.Value = expressionWithBrackets;
            return true;
        }

        private static string Serialise(XDocument doc, string original)
        {
            bool crlf = original.Contains("\r\n");
            var sb = new StringBuilder();
            var settings = new XmlWriterSettings
            {
                OmitXmlDeclaration = true,
                Indent = true,
                IndentChars = "  ",
                NewLineChars = crlf ? "\r\n" : "\n",
                NewLineHandling = NewLineHandling.Replace,
            };
            using (var sw = new StringWriter(sb))
            using (var xw = XmlWriter.Create(sw, settings))
                doc.Save(xw);
            string body = sb.ToString();
            if (original.TrimStart().StartsWith("<?xml", StringComparison.Ordinal))
            {
                int end = original.IndexOf("?>", StringComparison.Ordinal) + 2;
                if (end >= 2)
                    body = original.Substring(0, end) + (crlf ? "\r\n" : "\n") + body;
            }
            return body;
        }
    }
}
