using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using AnalyzerHelper.Interfaces;
using AnalyzerHelper.Models;

namespace AnalyzerHelper.Rules
{
    /// <summary>
    /// Validates Catch has exception logging (Error_Log) and Rethrow.
    /// Auto-fix: replaces existing Info_Log with Error_Log, or adds Error_Log if no log exists.
    /// Adds Rethrow before the Sequence closes when missing.
    /// </summary>
    public sealed class TryCatchRule : IAnalyzerRuleWithFix
    {
        public string RuleId => "VF-012";
        public string RuleName => "Missing Catch Block Actions";
        public string DefaultRecommendation => "Add i:Error_Log with exception details in StrMessage and a Rethrow in each Catch handler.";
        public bool RequiresUserInteraction => false;

        /// <summary>Error_Log template for IAP; StrMessage logs source and message for the Catch delegate argument.</summary>
        private const string ErrorLogActivity =
            @"<i:Error_Log StrMessage=""[exception.Source + &quot; &quot; + exception.Message]"" DisplayName=""Error Log"" sap:VirtualizedContainerService.HintSize=""200,25"" sap2010:WorkflowViewState.IdRef=""Error_Log_VF012"" StrTag=""error"" />";

        private const string RethrowActivity = @"<Rethrow DisplayName=""Rethrow"" />";

        /// <summary>Matches any Info_Log in Catch blocks.</summary>
        private static readonly Regex CatchHasInfoLog = new Regex(
            @"<i:Info_Log\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>Matches any Error_Log or ui:Log in Catch blocks.</summary>
        private static readonly Regex CatchHasErrorLog = new Regex(
            @"<ui:Log\b|<i:Error_Log\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>Matches the full Info_Log tag for in-place replacement.</summary>
        private static readonly Regex InfoLogFullTag = new Regex(
            @"<i:Info_Log\b[^>]*?(?:/>|>[\s\S]*?</i:Info_Log>)",
            RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

        // =====================================================================
        //  Check  (report only)
        // =====================================================================

        public IReadOnlyList<RuleCheckResult> Check(string filePath, string content)
        {
            var results = new List<RuleCheckResult>();
            if (string.IsNullOrWhiteSpace(content)) return results;

            if (content.IndexOf("<TryCatch", StringComparison.OrdinalIgnoreCase) < 0)
                return results;

            var catchBlocks = FindCatchBlocks(content);
            foreach (var (catchStart, bodyStart, catchCloseTagStart, _) in catchBlocks)
            {
                string body = content.Substring(bodyStart, catchCloseTagStart - bodyStart);
                bool hasErrorLog = CatchHasErrorLog.IsMatch(body);
                bool hasInfoLog = CatchHasInfoLog.IsMatch(body);
                bool hasRethrow = body.IndexOf("<Rethrow", StringComparison.OrdinalIgnoreCase) >= 0;

                // If Error_Log (or ui:Log) and Rethrow both exist, nothing to report
                if (hasErrorLog && hasRethrow) continue;

                var issues = new List<string>();
                string recommendation;

                if (hasInfoLog && !hasErrorLog)
                {
                    // Info_Log exists but should be Error_Log
                    issues.Add("Error log (Info_Log found, should be replaced with Error_Log)");
                    if (!hasRethrow) issues.Add("Rethrow");
                    recommendation = !hasRethrow
                        ? "Replace the existing Info_Log with Error_Log (retaining its message properties) and add a Rethrow in the Catch handler."
                        : "Replace the existing Info_Log with Error_Log (retaining its message properties) in the Catch handler. Error conditions should use Error_Log.";
                }
                else if (!hasErrorLog && !hasInfoLog)
                {
                    // No log at all
                    issues.Add("Error log");
                    if (!hasRethrow) issues.Add("Rethrow");
                    recommendation = DefaultRecommendation;
                }
                else
                {
                    // Has Error_Log but missing Rethrow
                    issues.Add("Rethrow");
                    recommendation = "Add a Rethrow activity at the end of the Catch handler to propagate the exception.";
                }

                results.Add(new RuleCheckResult
                {
                    RuleId = RuleId,
                    RuleName = RuleName,
                    Level = RuleLevel.Error,
                    Message = $"Catch block is missing {string.Join(" and ", issues)}.",
                    FilePath = filePath,
                    Recommendation = recommendation,
                    RequiresUserInteraction = RequiresUserInteraction
                });
            }

            return results;
        }

        // =====================================================================
        //  DefineAndFix  (detect + fix)
        // =====================================================================

        public bool DefineAndFix(string filePath, string content, out string newContent)
        {
            newContent = content;
            if (string.IsNullOrWhiteSpace(content) || content.IndexOf("<TryCatch", StringComparison.OrdinalIgnoreCase) < 0)
                return false;

            var catchBlocks = FindCatchBlocks(content);
            if (catchBlocks.Count == 0) return false;

            bool changed = false;
            int offset = 0;
            foreach (var (_, bodyStart, catchCloseTagStart, _) in catchBlocks)
            {
                int adjBodyStart = bodyStart + offset;
                int adjCatchClose = catchCloseTagStart + offset;
                string body = newContent.Substring(adjBodyStart, adjCatchClose - adjBodyStart);
                bool hasErrorLog = CatchHasErrorLog.IsMatch(body);
                bool hasInfoLog = CatchHasInfoLog.IsMatch(body);
                bool hasRethrow = body.IndexOf("<Rethrow", StringComparison.OrdinalIgnoreCase) >= 0;

                if (hasErrorLog && hasRethrow) continue;

                int addedThisCatch = 0;

                // Case 1: Info_Log exists -- replace it in-place with Error_Log
                if (hasInfoLog && !hasErrorLog)
                {
                    string before = newContent.Substring(0, adjBodyStart);
                    string catchBody = newContent.Substring(adjBodyStart, adjCatchClose - adjBodyStart);
                    string after = newContent.Substring(adjCatchClose);

                    string fixedBody = ReplaceInfoLogWithErrorLog(catchBody);
                    int delta = fixedBody.Length - catchBody.Length;
                    newContent = before + fixedBody + after;
                    addedThisCatch += delta;
                    changed = true;
                }
                // Case 2: No log at all -- insert Error_Log
                else if (!hasErrorLog)
                {
                    int adjCatchCloseForInsert = adjCatchClose + addedThisCatch;
                    if (TryGetCatchSequenceInsertPoints(newContent, adjBodyStart, adjCatchCloseForInsert, out int logInsertIndex, out int _))
                    {
                        string indentL = GetIndentationBefore(newContent, logInsertIndex);
                        string logPart = Environment.NewLine + indentL + ErrorLogActivity;
                        newContent = newContent.Substring(0, logInsertIndex) + logPart + newContent.Substring(logInsertIndex);
                        addedThisCatch += logPart.Length;
                        changed = true;
                    }
                }

                // Missing Rethrow -- insert at end of Sequence (re-read positions after body shift)
                if (!hasRethrow)
                {
                    int adjCatchClose2 = catchCloseTagStart + offset + addedThisCatch;
                    int adjBodyStart2 = bodyStart + offset;
                    if (TryGetCatchSequenceInsertPoints(newContent, adjBodyStart2, adjCatchClose2, out int _, out int rethrowIdx))
                    {
                        string indentR = GetIndentationBefore(newContent, rethrowIdx);
                        string rethrowPart = Environment.NewLine + indentR + RethrowActivity;
                        newContent = newContent.Substring(0, rethrowIdx) + rethrowPart + newContent.Substring(rethrowIdx);
                        addedThisCatch += rethrowPart.Length;
                        changed = true;
                    }
                }

                offset += addedThisCatch;
            }

            return changed;
        }
        /// <summary>Returns (catchStart, bodyStart, catchCloseTagStart, indexAfterCatchTag) for each Catch block.</summary>
        private static List<(int catchStart, int bodyStart, int catchCloseTagStart, int afterCatchEnd)> FindCatchBlocks(string content)
        {
            var list = new List<(int, int, int, int)>();
            int i = 0;
            while (i < content.Length)
            {
                int catchOpen = content.IndexOf("<Catch", i, StringComparison.OrdinalIgnoreCase);
                if (catchOpen < 0) break;
                int afterCatch = catchOpen + 6;
                if (afterCatch >= content.Length) break;
                char c = content[afterCatch];
                if (c != ' ' && c != '>')
                {
                    i = afterCatch;
                    continue;
                }
                int tagEnd = content.IndexOf('>', catchOpen);
                if (tagEnd < 0) break;
                int bodyStart = tagEnd + 1;
                int catchClose = content.IndexOf("</Catch>", bodyStart, StringComparison.OrdinalIgnoreCase);
                if (catchClose < 0) break;
                list.Add((catchOpen, bodyStart, catchClose, catchClose + "</Catch>".Length));
                i = catchClose + 1;
            }
            return list;
        }

        /// <summary>logInsertIndex = split index after ViewState (or after &lt;Sequence&gt; open); rethrowInsertIndex = start of closing &lt;/Sequence&gt;.</summary>
        private static bool TryGetCatchSequenceInsertPoints(string content, int bodyStart, int catchCloseTagStart, out int logInsertIndex, out int rethrowInsertIndex)
        {
            logInsertIndex = -1;
            rethrowInsertIndex = -1;
            if (catchCloseTagStart > content.Length || bodyStart >= catchCloseTagStart)
                return false;

            int aaOpen = content.IndexOf("<ActivityAction", bodyStart, StringComparison.OrdinalIgnoreCase);
            if (aaOpen < 0 || aaOpen >= catchCloseTagStart)
                return false;

            int aaClose = content.LastIndexOf("</ActivityAction>", catchCloseTagStart, StringComparison.OrdinalIgnoreCase);
            if (aaClose < aaOpen)
                return false;

            int argClose = content.IndexOf("</ActivityAction.Argument>", aaOpen, StringComparison.OrdinalIgnoreCase);
            int searchFrom = aaOpen;
            if (argClose >= 0 && argClose < aaClose)
                searchFrom = argClose + "</ActivityAction.Argument>".Length;

            int seqOpen = content.IndexOf("<Sequence", searchFrom, StringComparison.OrdinalIgnoreCase);
            if (seqOpen < 0 || seqOpen >= aaClose)
                return false;

            rethrowInsertIndex = FindMatchingSequenceClose(content, seqOpen, aaClose);
            if (rethrowInsertIndex < 0)
                return false;

            logInsertIndex = FindSequenceBodyInsertStart(content, seqOpen, rethrowInsertIndex);
            return logInsertIndex >= 0;
        }

        /// <summary>First content position inside the handler Sequence: after optional leading WorkflowViewState, else right after the opening Sequence tag.</summary>
        private static int FindSequenceBodyInsertStart(string content, int seqOpen, int seqCloseIndex)
        {
            int openTagEnd = content.IndexOf('>', seqOpen);
            if (openTagEnd < 0 || openTagEnd >= seqCloseIndex)
                return -1;
            int innerStart = openTagEnd + 1;
            const string viewStateClose = "</sap:WorkflowViewStateService.ViewState>";
            int vsEnd = content.IndexOf(viewStateClose, innerStart, StringComparison.OrdinalIgnoreCase);
            if (vsEnd >= 0 && vsEnd < seqCloseIndex)
                return vsEnd + viewStateClose.Length;
            return innerStart;
        }

        private static int FindMatchingSequenceClose(string content, int sequenceOpen, int limit)
        {
            int depth = 1;
            int i = sequenceOpen + 1;
            while (i < limit && depth > 0)
            {
                int nextOpen = content.IndexOf("<Sequence", i, StringComparison.OrdinalIgnoreCase);
                int nextClose = content.IndexOf("</Sequence>", i, StringComparison.OrdinalIgnoreCase);
                if (nextOpen >= limit)
                    nextOpen = -1;
                if (nextClose < 0 || nextClose >= limit)
                    return -1;
                if (nextOpen >= 0 && nextOpen < nextClose)
                {
                    depth++;
                    i = nextOpen + 1;
                }
                else
                {
                    depth--;
                    if (depth == 0)
                        return nextClose;
                    i = nextClose + "</Sequence>".Length;
                }
            }
            return -1;
        }

        /// <summary>Replaces i:Info_Log tags with i:Error_Log, preserving StrMessage and other attributes.</summary>
        private static string ReplaceInfoLogWithErrorLog(string body)
        {
            return InfoLogFullTag.Replace(body, m =>
            {
                string tag = m.Value;
                tag = Regex.Replace(tag, @"<i:Info_Log\b", "<i:Error_Log", RegexOptions.IgnoreCase);
                tag = Regex.Replace(tag, @"</i:Info_Log>", "</i:Error_Log>", RegexOptions.IgnoreCase);
                tag = Regex.Replace(tag, @"DisplayName=""[^""]*Info[^""]*Log[^""]*""", @"DisplayName=""Error Log""", RegexOptions.IgnoreCase);
                tag = Regex.Replace(tag, @"StrTag=""info""", @"StrTag=""error""", RegexOptions.IgnoreCase);
                tag = Regex.Replace(tag, @"Info_Log_", "Error_Log_", RegexOptions.IgnoreCase);
                return tag;
            });
        }

        private static int GetLineNumber(string content, int index)
        {
            int line = 1;
            int end = Math.Min(index, content.Length);
            for (int j = 0; j < end; j++)
            {
                if (content[j] == '\n')
                    line++;
            }
            return line;
        }

        private static string GetIndentationBefore(string content, int insertIndex)
        {
            int lastNl = content.LastIndexOf('\n', Math.Max(0, insertIndex - 1));
            int lineStart = lastNl < 0 ? 0 : lastNl + 1;
            var sb = new StringBuilder();
            for (int j = lineStart; j < insertIndex && j < content.Length; j++)
            {
                char ch = content[j];
                if (ch == ' ' || ch == '\t')
                    sb.Append(ch);
                else
                    break;
            }
            if (sb.Length == 0)
                sb.Append("              ");
            return sb.ToString();
        }
    }
}
