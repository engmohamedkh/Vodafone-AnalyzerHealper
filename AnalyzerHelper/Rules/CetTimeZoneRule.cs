using AnalyzerHelper.Interfaces;
using AnalyzerHelper.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace AnalyzerHelper.Rules
{
    /// <summary>
    /// VF-053: Anywhere in the project, if an &lt;Assign&gt; writes to <c>dtmTransactionEndTime</c> or
    /// <c>dtmtransactionStartTime</c> / <c>dtmTransactionStartTime</c> (see &lt;Assign.To&gt;), the
    /// &lt;Assign.Value&gt; expression must use CET via
    /// <c>TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time")</c>.
    /// ClipboardData files also get WorkflowViewStateService.ViewState after Assign.Value when missing.
    /// </summary>
    public sealed class CetTimeZoneRule : IAnalyzerRuleWithFix
    {
        public string RuleId => "VF-053";
        public string RuleName => "CETTimeZone";
        public string DefaultRecommendation =>
            "For Assigns targeting dtmTransactionEndTime or dtmtransactionStartTime / dtmTransactionStartTime, " +
            "set Value to TimeZoneInfo.ConvertTime(..., TimeZoneInfo.FindSystemTimeZoneById(\"Central European Standard Time\")).";
        public bool RequiresUserInteraction => false;

        private static readonly XNamespace XNs = "http://schemas.microsoft.com/winfx/2006/xaml";

        private const string StartExpressionFix =
            "[TimeZoneInfo.ConvertTime(in_dtmTransactionStartTime,TimeZoneInfo.FindSystemTimeZoneById(\"Central European Standard Time\"))]";

        private const string EndExpressionFix =
            "[TimeZoneInfo.ConvertTime(System.DateTime.Now , TimeZoneInfo.FindSystemTimeZoneById(\"Central European Standard Time\"))]";

        private const string ClipboardAssignViewStateFragment =
            "<WorkflowViewStateService.ViewState xmlns=\"http://schemas.microsoft.com/netfx/2009/xaml/activities/presentation\" xmlns:scg=\"clr-namespace:System.Collections.Generic;assembly=System.Private.CoreLib\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"><scg:Dictionary x:TypeArguments=\"x:String, x:Object\"><x:Boolean x:Key=\"IsExpanded\">True</x:Boolean></scg:Dictionary></WorkflowViewStateService.ViewState>";

        private enum DtmAssignKind
        {
            None,
            EndTime,
            StartTime
        }

        public IReadOnlyList<RuleCheckResult> Check(string filePath, string content)
        {
            var results = new List<RuleCheckResult>();
            if (string.IsNullOrWhiteSpace(content) || !TryLoad(content, out XDocument? doc) || doc == null)
                return results;

            foreach (var assign in EnumerateAssigns(doc))
            {
                DtmAssignKind kind = ClassifyDtmAssign(assign);
                if (kind == DtmAssignKind.None)
                    continue;

                string? expr = GetAssignValueExpression(assign);
                if (!ValueHasCetCentralEurope(expr))
                {
                    string target = kind == DtmAssignKind.EndTime ? "dtmTransactionEndTime" : "dtmtransactionStartTime / dtmTransactionStartTime";
                    string expected = kind == DtmAssignKind.EndTime ? EndExpressionFix : StartExpressionFix;
                    results.Add(MakeResult(filePath,
                        $"Assign \"{GetDisplayName(assign)}\" targets {target}; Value must use CET (FindSystemTimeZoneById with Central European Standard Time). Example: {expected}"));
                }
                else if (IsClipboardDataRoot(doc) && !HasClipboardAssignViewState(assign))
                {
                    results.Add(MakeResult(filePath,
                        $"Assign \"{GetDisplayName(assign)}\" is missing WorkflowViewStateService.ViewState after Assign.Value (Studio clipboard layout)."));
                }
            }

            return results;
        }

        public bool DefineAndFix(string filePath, string content, out string newContent)
        {
            newContent = content;
            if (string.IsNullOrWhiteSpace(content) || !TryLoad(content, out XDocument? doc) || doc == null)
                return false;

            bool clipboardDoc = IsClipboardDataRoot(doc);
            bool changed = false;

            foreach (var assign in EnumerateAssigns(doc))
            {
                DtmAssignKind kind = ClassifyDtmAssign(assign);
                if (kind == DtmAssignKind.None)
                    continue;

                string? expr = GetAssignValueExpression(assign);
                if (!ValueHasCetCentralEurope(expr))
                {
                    string fix = kind == DtmAssignKind.EndTime ? EndExpressionFix : StartExpressionFix;
                    changed |= EnsureAssignValueExpression(assign, fix);
                }

                if (clipboardDoc)
                    changed |= EnsureClipboardAssignViewState(assign);
            }

            if (!changed)
                return false;

            newContent = Serialise(doc, content);
            return newContent != content;
        }

        /// <summary>End-time variable (check first so it does not collide with substring rules).</summary>
        private const string TargetEndTimeVar = "dtmTransactionEndTime";

        private static DtmAssignKind ClassifyDtmAssign(XElement assign)
        {
            string blob = GetAssignToTextBlob(assign);
            if (blob.IndexOf(TargetEndTimeVar, StringComparison.OrdinalIgnoreCase) >= 0)
                return DtmAssignKind.EndTime;
            if (blob.IndexOf("dtmtransactionStartTime", StringComparison.OrdinalIgnoreCase) >= 0
                || blob.IndexOf("dtmTransactionStartTime", StringComparison.OrdinalIgnoreCase) >= 0)
                return DtmAssignKind.StartTime;
            return DtmAssignKind.None;
        }

        private static string GetAssignToTextBlob(XElement assign)
        {
            XElement? to = assign.Elements().FirstOrDefault(e => e.Name.LocalName == "Assign.To");
            if (to == null)
                return "";
            // XElement.Value is the concatenation of all text nodes in the subtree.
            return to.Value;
        }

        private static bool ValueHasCetCentralEurope(string? expr)
        {
            if (string.IsNullOrEmpty(expr))
                return false;
            if (expr.IndexOf("FindSystemTimeZoneById", StringComparison.OrdinalIgnoreCase) < 0)
                return false;
            if (expr.IndexOf("Central European Standard Time", StringComparison.OrdinalIgnoreCase) < 0)
                return false;
            return true;
        }

        private static bool IsClipboardDataRoot(XDocument doc)
            => doc.Root != null && doc.Root.Name.LocalName == "ClipboardData";

        private static IEnumerable<XElement> EnumerateAssigns(XDocument doc)
            => doc.Descendants().Where(e => e.Name.LocalName == "Assign");

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

        private static bool TryLoad(string content, out XDocument? doc)
        {
            try
            {
                doc = XDocument.Parse(content, LoadOptions.PreserveWhitespace);
                return true;
            }
            catch
            {
                doc = null;
                return false;
            }
        }

        private static XElement? GetAssignValueContainer(XElement assign)
            => assign.Elements().FirstOrDefault(e => e.Name.LocalName == "Assign.Value");

        private static string? GetAssignValueExpression(XElement assign)
        {
            XElement? valueEl = GetAssignValueContainer(assign);
            if (valueEl == null)
                return null;
            XElement? inArg = valueEl.Elements().FirstOrDefault(e => e.Name.LocalName == "InArgument");
            return inArg?.Value.Trim();
        }

        private static string GetDisplayName(XElement el)
        {
            var a = el.Attributes().FirstOrDefault(x => x.Name.LocalName == "DisplayName");
            return string.IsNullOrEmpty(a?.Value) ? "(no DisplayName)" : a!.Value;
        }

        private static bool HasClipboardAssignViewState(XElement assign)
            => assign.Elements().Any(e => e.Name.LocalName == "WorkflowViewStateService.ViewState");

        private static bool EnsureClipboardAssignViewState(XElement assign)
        {
            if (HasClipboardAssignViewState(assign))
                return false;
            XElement? valueEl = GetAssignValueContainer(assign);
            if (valueEl == null)
                return false;
            try
            {
                var vs = XElement.Parse(ClipboardAssignViewStateFragment, LoadOptions.PreserveWhitespace);
                valueEl.AddAfterSelf(vs);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool EnsureAssignValueExpression(XElement assign, string expressionWithBrackets)
        {
            XNamespace ns = assign.Name.Namespace;
            XElement? valueEl = GetAssignValueContainer(assign);
            if (valueEl == null)
            {
                valueEl = new XElement(ns + "Assign.Value",
                    new XElement(ns + "InArgument",
                        new XAttribute(XNs + "TypeArguments", "s:DateTime"),
                        expressionWithBrackets));
                assign.Add(valueEl);
                return true;
            }

            XElement? inArg = valueEl.Elements().FirstOrDefault(e => e.Name.LocalName == "InArgument");
            if (inArg == null)
            {
                inArg = new XElement(ns + "InArgument",
                    new XAttribute(XNs + "TypeArguments", "s:DateTime"),
                    expressionWithBrackets);
                valueEl.Add(inArg);
                return true;
            }

            var typeAttr = inArg.Attribute(XNs + "TypeArguments") ?? inArg.Attribute("TypeArguments");
            if (typeAttr == null)
                inArg.SetAttributeValue(XNs + "TypeArguments", "s:DateTime");

            if (string.Equals(inArg.Value.Trim(), expressionWithBrackets.Trim(), StringComparison.Ordinal))
                return false;

            inArg.Value = expressionWithBrackets;
            return true;
        }

        private static string Serialise(XDocument doc, string original)
        {
            bool crlf = original.Contains("\r\n");
            bool clipboard = doc.Root != null && doc.Root.Name.LocalName == "ClipboardData";
            var sb = new StringBuilder();
            var settings = new XmlWriterSettings
            {
                OmitXmlDeclaration = true,
                Indent = !clipboard,
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