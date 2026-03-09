using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AnalyzerHelper.Interfaces;
using AnalyzerHelper.Models;

namespace AnalyzerHelper.Rules
{
    /// <summary>Checks that the workflow has TryCatch and proper exception handling. Auto-fix: adds TryCatch (No interaction).</summary>
    public sealed class TryCatchRule : IAnalyzerRuleWithFix
    {
        public string RuleId => "VF-012";
        public string RuleName => "TryCatch";
        public string DefaultRecommendation => "Add TryCatch and log exception.source/exception.message. Keep activities inside try scope.";
        public bool RequiresUserInteraction => false;

        public IReadOnlyList<RuleCheckResult> Check(string filePath, string content)
        {
            var results = new List<RuleCheckResult>();
            if (string.IsNullOrWhiteSpace(content)) return results;

            bool hasTryCatch = content.IndexOf("<TryCatch", StringComparison.OrdinalIgnoreCase) >= 0;
            bool hasSequenceOrFlowchart = content.IndexOf("Sequence", StringComparison.OrdinalIgnoreCase) >= 0
                || content.IndexOf("Flowchart", StringComparison.OrdinalIgnoreCase) >= 0;

            if (!hasSequenceOrFlowchart)
                return results;

            if (!hasTryCatch)
            {
                results.Add(new RuleCheckResult
                {
                    RuleId = RuleId,
                    RuleName = RuleName,
                    Level = RuleLevel.Error,
                    Message = "Workflow does not contain a TryCatch activity.",
                    FilePath = filePath,
                    Recommendation = DefaultRecommendation,
                    RequiresUserInteraction = RequiresUserInteraction
                });
                return results;
            }

            if (!Regex.IsMatch(content, "exception\\.(source|message)", RegexOptions.IgnoreCase))
            {
                results.Add(new RuleCheckResult
                {
                    RuleId = RuleId,
                    RuleName = RuleName,
                    Level = RuleLevel.Warning,
                    Message = "TryCatch found but exception logging (exception.source/exception.message) may be missing.",
                    FilePath = filePath,
                    Recommendation = DefaultRecommendation,
                    RequiresUserInteraction = RequiresUserInteraction
                });
            }

            return results;
        }

        public bool DefineAndFix(string filePath, string content, out string newContent)
        {
            newContent = content;
            if (string.IsNullOrWhiteSpace(content))
                return false;

            bool hasTryCatch = content.IndexOf("<TryCatch", StringComparison.OrdinalIgnoreCase) >= 0;
            bool hasExceptionLogging = Regex.IsMatch(content, "exception\\.(source|message)", RegexOptions.IgnoreCase);

            // Case 2: TryCatch exists but exception logging is missing — add Log to first Catch
            if (hasTryCatch && !hasExceptionLogging)
            {
                const string logActivity = @"<ui:Log Message=""[exception.source] [exception.message]"" DisplayName=""Log"" />";
                // Find first <Catch ...> (any attributes) and insert Log right after the opening tag
                var catchOpenMatch = Regex.Match(content, @"<Catch\s[^>]*>", RegexOptions.IgnoreCase);
                if (catchOpenMatch.Success)
                {
                    int insertAt = catchOpenMatch.Index + catchOpenMatch.Length;
                    newContent = content.Substring(0, insertAt) + logActivity + content.Substring(insertAt);
                    if (newContent.IndexOf("xmlns:ui=", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        int actIdx = newContent.IndexOf("<Activity ", StringComparison.OrdinalIgnoreCase);
                        if (actIdx >= 0)
                            newContent = newContent.Substring(0, actIdx) + "<Activity xmlns:ui=\"http://schemas.uipath.com/workflow/activities\" " + newContent.Substring(actIdx + "<Activity ".Length);
                    }
                    return true;
                }
                return false;
            }

            // Case 1: No TryCatch — wrap root Sequence/Flowchart in TryCatch
            if (hasTryCatch)
                return false;

            bool hasSequenceOrFlowchart = content.IndexOf("Sequence", StringComparison.OrdinalIgnoreCase) >= 0
                || content.IndexOf("Flowchart", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!hasSequenceOrFlowchart)
                return false;

            var (tagName, startIdx, endIdx, isSelfClosing) = FindRootActivity(content);
            if (tagName == null || startIdx < 0 || endIdx <= startIdx) return false;

            string inner = content.Substring(startIdx, endIdx - startIdx);

            if (isSelfClosing)
            {
                inner = inner.Substring(0, inner.Length - 2);
                inner = inner + $"></{tagName}>";
            }

            const string catchBlock = @"<Catch ExceptionType=""System.Exception"" DisplayName=""Catch""><ui:Log Message=""[exception.source] [exception.message]"" DisplayName=""Log"" /></Catch>";
            string wrapped = $"<TryCatch><TryCatch.Try>{inner}</TryCatch.Try><TryCatch.Catches>{catchBlock}</TryCatch.Catches></TryCatch>";
            newContent = content.Substring(0, startIdx) + wrapped + content.Substring(endIdx);

            if (newContent.IndexOf("xmlns:ui=", StringComparison.OrdinalIgnoreCase) < 0)
            {
                int actIdx = newContent.IndexOf("<Activity ", StringComparison.OrdinalIgnoreCase);
                if (actIdx >= 0)
                    newContent = newContent.Substring(0, actIdx) + "<Activity xmlns:ui=\"http://schemas.uipath.com/workflow/activities\" " + newContent.Substring(actIdx + "<Activity ".Length);
            }
            return true;
        }

        private static (string? tagName, int startIdx, int endIdx, bool isSelfClosing) FindRootActivity(string content)
        {
            // Find the Activity tag to locate root-level children
            int activityStart = content.IndexOf("<Activity", StringComparison.OrdinalIgnoreCase);
            if (activityStart < 0) return (null, -1, -1, false);
            
            // Find where Activity tag ends (either > or />)
            int activityTagEnd = content.IndexOf('>', activityStart);
            if (activityTagEnd < 0) return (null, -1, -1, false);
            
            // Start searching after the Activity opening tag
            int searchStart = activityTagEnd + 1;

            foreach (var tag in new[] { "Sequence", "Flowchart" })
            {
                // Find the first occurrence of the tag after Activity opens
                int start = content.IndexOf("<" + tag, searchStart, StringComparison.OrdinalIgnoreCase);
                if (start < 0) continue;

                // Verify this is actually a tag start (not part of another tag name or attribute)
                // Check that it's followed by whitespace, >, or / (for self-closing)
                if (start + ("<" + tag).Length < content.Length)
                {
                    char nextChar = content[start + ("<" + tag).Length];
                    if (nextChar != ' ' && nextChar != '>' && nextChar != '/' && nextChar != '\r' && nextChar != '\n' && nextChar != '\t')
                        continue;
                }

                // Check if this is a self-closing tag (e.g., <Sequence DisplayName="Main" />)
                int tagEnd = start + ("<" + tag).Length;
                // Find the end of the tag (either > or />)
                int greaterThanIdx = content.IndexOf('>', tagEnd);
                if (greaterThanIdx < 0) continue;

                // Check if it's self-closing
                if (greaterThanIdx > 0 && content[greaterThanIdx - 1] == '/')
                {
                    // Self-closing tag: <Sequence ... />
                    int end = greaterThanIdx + 1;
                    return (tag, start, end, true);
                }

                // Full tag: <Sequence>...</Sequence>
                // Find the matching closing tag by tracking depth. Only count real element opens
                // (ignore <Sequence.Variables>, <Sequence.Something>, etc.).
                int depth = 1;
                int i = greaterThanIdx + 1;
                string openTag = "<" + tag;
                string closeTag = "</" + tag + ">";
                bool IsRealOpenTag(int idx)
                {
                    if (idx + openTag.Length >= content.Length) return false;
                    char c = content[idx + openTag.Length];
                    return c == ' ' || c == '>' || c == '/' || c == '\r' || c == '\n' || c == '\t';
                }

                while (i < content.Length)
                {
                    int nextOpen = content.IndexOf(openTag, i, StringComparison.OrdinalIgnoreCase);
                    while (nextOpen >= 0 && !IsRealOpenTag(nextOpen))
                    {
                        nextOpen = content.IndexOf(openTag, nextOpen + 1, StringComparison.OrdinalIgnoreCase);
                    }
                    int nextClose = content.IndexOf(closeTag, i, StringComparison.OrdinalIgnoreCase);

                    if (nextClose < 0) break;

                    if (nextOpen >= 0 && nextOpen < nextClose)
                    {
                        depth++;
                        i = nextOpen + openTag.Length;
                    }
                    else
                    {
                        depth--;
                        if (depth == 0)
                        {
                            int end = nextClose + closeTag.Length;
                            return (tag, start, end, false);
                        }
                        i = nextClose + closeTag.Length;
                    }
                }
            }
            return (null, -1, -1, false);
        }
    }
}
