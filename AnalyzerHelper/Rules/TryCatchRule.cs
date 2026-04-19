using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using AnalyzerHelper.Interfaces;
using AnalyzerHelper.Models;

namespace AnalyzerHelper.Rules
{
    /// <summary>Validates Catch has exception logging and Rethrow. Auto-fix: adds i:Info_Log (IAP) after the handler Sequence opening, Rethrow before the Sequence closes.</summary>
    public sealed class TryCatchRule : IAnalyzerRuleWithFix
    {
        public string RuleId => "VF-012";
        public string RuleName => "TryCatch";
        public string DefaultRecommendation => "Add i:Info_Log with exception details in StrMessage and a Rethrow in each Catch handler.";
        public bool RequiresUserInteraction => false;

        /// <summary>Matches studio style for IAP Info_Log; StrMessage logs source and message for the Catch delegate argument.</summary>
        private const string InfoLogActivity =
            @"<i:Info_Log StrMessage=""[exception.Source + &quot; &quot; + exception.Message]"" DisplayName=""Info Log"" sap:VirtualizedContainerService.HintSize=""200,25"" sap2010:WorkflowViewState.IdRef=""Info_Log_VF012"" StrTag=""info"" />";

        private const string RethrowActivity = @"<Rethrow DisplayName=""Rethrow"" />";

        /// <summary>Do not treat arbitrary attributes (e.g. Process_Monitoring StrExecutionMessage) as the required exception log — only dedicated log activities.</summary>
        private static readonly Regex CatchHasExplicitExceptionLog = new Regex(
            @"<ui:Log\b[^>]*\bMessage\s*=\s*""[^""]*exception\.(source|message)[^""]*""" +
            @"|<i:Info_Log\b[^>]*\bStrMessage\s*=\s*""[^""]*exception\.(source|message)[^""]*""" +
            @"|<i:Error_Log\b[^>]*\bStrMessage\s*=\s*""[^""]*exception\.(source|message)[^""]*""",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

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
                bool hasInfoLog = CatchHasExplicitExceptionLog.IsMatch(body);
                bool hasRethrow = body.IndexOf("<Rethrow", StringComparison.OrdinalIgnoreCase) >= 0;

                if (!hasInfoLog || !hasRethrow)
                {
                    int line = GetLineNumber(content, catchStart);
                    var missing = new List<string>();
                    if (!hasInfoLog) missing.Add("Info log");
                    if (!hasRethrow) missing.Add("Rethrow");
                    results.Add(new RuleCheckResult
                    {
                        RuleId = RuleId,
                        RuleName = RuleName,
                        Level = RuleLevel.Error,
                        Message = $"Line {line}: Missing {string.Join(", ", missing)}.",
                        FilePath = filePath,
                        Recommendation = DefaultRecommendation,
                        RequiresUserInteraction = RequiresUserInteraction
                    });
                }
            }

            return results;
        }

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
                bool hasInfoLog = CatchHasExplicitExceptionLog.IsMatch(body);
                bool hasRethrow = body.IndexOf("<Rethrow", StringComparison.OrdinalIgnoreCase) >= 0;

                if (hasInfoLog && hasRethrow) continue;

                if (!TryGetCatchSequenceInsertPoints(newContent, adjBodyStart, adjCatchClose, out int logInsertIndex, out int rethrowInsertIndex))
                    continue;

                int addedThisCatch = 0;
                // Insert end (Rethrow) first when both needed so start index stays valid.
                if (!hasRethrow)
                {
                    string indentR = GetIndentationBefore(newContent, rethrowInsertIndex);
                    string rethrowPart = Environment.NewLine + indentR + RethrowActivity;
                    newContent = newContent.Substring(0, rethrowInsertIndex) + rethrowPart + newContent.Substring(rethrowInsertIndex);
                    addedThisCatch += rethrowPart.Length;
                }

                if (!hasInfoLog)
                {
                    string indentL = GetIndentationBefore(newContent, logInsertIndex);
                    string logPart = Environment.NewLine + indentL + InfoLogActivity;
                    newContent = newContent.Substring(0, logInsertIndex) + logPart + newContent.Substring(logInsertIndex);
                    addedThisCatch += logPart.Length;
                }

                offset += addedThisCatch;
                changed = true;
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