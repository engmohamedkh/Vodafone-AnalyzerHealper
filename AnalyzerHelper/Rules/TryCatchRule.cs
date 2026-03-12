using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AnalyzerHelper.Interfaces;
using AnalyzerHelper.Models;

namespace AnalyzerHelper.Rules
{
    /// <summary>Validates that each Catch block contains an info-message log (exception logging) and Rethrow. Auto-fix: adds missing Log and/or Rethrow.</summary>
    public sealed class TryCatchRule : IAnalyzerRuleWithFix
    {
        public string RuleId => "VF-012";
        public string RuleName => "TryCatch";
        public string DefaultRecommendation => "Each Catch must contain an info-message log (e.g. exception.source/exception.message) and a Rethrow activity.";
        public bool RequiresUserInteraction => false;

        private const string LogActivity = @"<ui:Log Message=""[exception.source] [exception.message]"" DisplayName=""Log"" />";
        private const string RethrowActivity = @"<Rethrow DisplayName=""Rethrow"" />";

        public IReadOnlyList<RuleCheckResult> Check(string filePath, string content)
        {
            var results = new List<RuleCheckResult>();
            if (string.IsNullOrWhiteSpace(content)) return results;

            if (content.IndexOf("<TryCatch", StringComparison.OrdinalIgnoreCase) < 0)
                return results;

            var catchBlocks = FindCatchBlocks(content);
            foreach (var (start, bodyStart, bodyEnd, end) in catchBlocks)
            {
                string body = content.Substring(bodyStart, bodyEnd - bodyStart);
                bool hasInfoLog = Regex.IsMatch(body, "exception\\.(source|message)", RegexOptions.IgnoreCase);
                bool hasRethrow = body.IndexOf("<Rethrow", StringComparison.OrdinalIgnoreCase) >= 0;

                if (!hasInfoLog || !hasRethrow)
                {
                    var missing = new List<string>();
                    if (!hasInfoLog) missing.Add("info-message log (exception.source/exception.message)");
                    if (!hasRethrow) missing.Add("Rethrow");
                    results.Add(new RuleCheckResult
                    {
                        RuleId = RuleId,
                        RuleName = RuleName,
                        Level = RuleLevel.Error,
                        Message = "Catch block must contain info-message log and Rethrow. Missing: " + string.Join(", ", missing) + ".",
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
            foreach (var (start, bodyStart, bodyEnd, end) in catchBlocks)
            {
                int adjBodyStart = bodyStart + offset;
                int adjBodyEnd = bodyEnd + offset;
                int adjEnd = end + offset;
                string body = newContent.Substring(adjBodyStart, adjBodyEnd - adjBodyStart);
                bool hasInfoLog = Regex.IsMatch(body, "exception\\.(source|message)", RegexOptions.IgnoreCase);
                bool hasRethrow = body.IndexOf("<Rethrow", StringComparison.OrdinalIgnoreCase) >= 0;

                string toInsertBeforeEnd = "";
                if (!hasInfoLog) toInsertBeforeEnd += LogActivity;
                if (!hasRethrow) toInsertBeforeEnd += RethrowActivity;
                if (toInsertBeforeEnd.Length == 0) continue;

                newContent = newContent.Substring(0, adjBodyEnd) + toInsertBeforeEnd + newContent.Substring(adjBodyEnd);
                offset += toInsertBeforeEnd.Length;
                changed = true;
            }

            if (changed && newContent.IndexOf("xmlns:ui=", StringComparison.OrdinalIgnoreCase) < 0)
            {
                int actIdx = newContent.IndexOf("<Activity ", StringComparison.OrdinalIgnoreCase);
                if (actIdx >= 0)
                    newContent = newContent.Substring(0, actIdx) + "<Activity xmlns:ui=\"http://schemas.uipath.com/workflow/activities\" " + newContent.Substring(actIdx + "<Activity ".Length);
            }
            return changed;
        }

        /// <summary>Returns (catchStart, bodyStart, bodyEnd, catchEnd) for each Catch block.</summary>
        private static List<(int catchStart, int bodyStart, int bodyEnd, int catchEnd)> FindCatchBlocks(string content)
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
    }
}
