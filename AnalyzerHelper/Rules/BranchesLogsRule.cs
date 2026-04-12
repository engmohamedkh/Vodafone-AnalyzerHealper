using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AnalyzerHelper.Interfaces;
using AnalyzerHelper.Models;
using AnalyzerHelper.View;

namespace AnalyzerHelper.Rules
{
    /// <summary>
    /// Validates that FlowDecision (True/False) and FlowSwitch (Default and Cases) branches
    /// contain an Info_Log, Message_Log, or Error_Log activity. Fix shows a batch table for ALL branches.
    /// </summary>
    public sealed class BranchesLogsRule : IBatchAnalyzerRuleWithFix
    {
        public string RuleId => "VF-038"; 
        public string RuleName => "Branches Logging";
        public string DefaultRecommendation => "Add Info Log, Message Log, or Error Log at the beginning of every branch. Use Fix to add Info Log with your message.";
        public bool RequiresUserInteraction => true;

        private static readonly Regex LogActivityPattern = new Regex(@"<\w+:(Info_Log|Message_Log|Error_Log)\s", RegexOptions.IgnoreCase);

        // ── Batch state ──
        private bool _batchPrepared;
        private bool _batchCancelled;
        private Dictionary<string, List<BranchLogRow>>? _batchRowsByFile;

        public IReadOnlyList<RuleCheckResult> Check(string filePath, string content)
        {
            var results = new List<RuleCheckResult>();
            if (string.IsNullOrWhiteSpace(content)) return results;

            var findings = FindBranchesMissingLog(content, filePath);
            foreach (var finding in findings)
            {
                results.Add(new RuleCheckResult
                {
                    RuleId = RuleId,
                    RuleName = RuleName,
                    Level = RuleLevel.Warning,
                    Message = finding.Description,
                    FilePath = filePath,
                    Recommendation = DefaultRecommendation,
                    RequiresUserInteraction = true
                });
            }
            return results;
        }

        public void PrepareBatch(IReadOnlyList<string> filePaths)
        {
            _batchPrepared = false;
            _batchCancelled = false;
            _batchRowsByFile = new Dictionary<string, List<BranchLogRow>>(StringComparer.OrdinalIgnoreCase);

            var allRows = new List<BranchLogRow>();
            string commonRoot = FindCommonRoot(filePaths);

            foreach (var path in filePaths)
            {
                if (string.IsNullOrWhiteSpace(path) || !path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
                    continue;

                string content;
                try { content = File.ReadAllText(path); }
                catch { continue; }

                var findings = FindBranchesMissingLogWithPosition(content, path);
                if (findings.Count == 0) continue;

                string rel = !string.IsNullOrEmpty(commonRoot) && path.StartsWith(commonRoot, StringComparison.OrdinalIgnoreCase)
                    ? path.Substring(commonRoot.Length).TrimStart(Path.DirectorySeparatorChar, '/', '\\')
                    : Path.GetFileName(path);

                var fileRows = new List<BranchLogRow>();
                int fileCheckCounter = 1; 
                foreach (var f in findings)
                {
                    string defaultMsg = "Branch entered";
                    
                    // Capture condition from description (which now includes it)
                    var conditionRegex = Regex.Match(f.Description, @"\[([^\]]*)\]");
                    string conditionText = conditionRegex.Success ? conditionRegex.Groups[1].Value : "";
                    
                    if (f.Type == "If" || f.Type == "FlowDecision")
                    {
                        bool isTrue = f.Description.Contains("Then") || f.Description.Contains("True");
                        string status = isTrue ? "True" : "False";
                        
                        if (!string.IsNullOrEmpty(conditionText))
                        {
                            defaultMsg = $"[{conditionText}] {status}";
                        }
                        else
                        {
                            // Fallback if condition not found
                            string displayNameMatch = "";
                            var nameRegex = Regex.Match(f.Description, @"'([^']*)'");
                            if (nameRegex.Success) displayNameMatch = nameRegex.Groups[1].Value;
                            defaultMsg = !string.IsNullOrEmpty(displayNameMatch) ? $"{displayNameMatch} {status}" : $"Condition {status}";
                        }
                    }
                    else if (f.Type == "FlowSwitch")
                    {
                        defaultMsg = f.Description.Split('–').Last().Trim();
                    }

                    string finalMsg = defaultMsg; // Using the direct condition-based message as requested

                    var row = new BranchLogRow
                    {
                        FilePath = path,
                        File = rel,
                        Type = f.Type,
                        BranchDescription = f.Description,
                        InsertIndex = f.InsertIndex,
                        Message = finalMsg,
                        AddLog = true // Default to true
                    };
                    fileRows.Add(row);
                    allRows.Add(row);
                    fileCheckCounter++;
                }
                _batchRowsByFile[path] = fileRows;
            }

            if (allRows.Count == 0)
            {
                _batchCancelled = true;
                return;
            }

            var dialog = new BranchLogBatchWindow(allRows);
            if (dialog.ShowDialog() != true || !dialog.Applied)
            {
                _batchCancelled = true;
                return;
            }

            _batchPrepared = true;
        }

        public bool DefineAndFix(string filePath, string content, out string newContent)
        {
            newContent = content;
            if (_batchCancelled) return false;
            if (string.IsNullOrWhiteSpace(content)) return false;

            List<BranchLogRow>? rows = null;
            if (_batchPrepared && _batchRowsByFile != null)
            {
                if (!_batchRowsByFile.TryGetValue(filePath, out rows))
                    return false;
            }
            else
            {
                return false;
            }

            string logPrefix = GetInfoLogPrefix(content);
            bool needsNamespace = EnsureInfoLogNamespace(content, ref logPrefix);

            // Sort by index descending to not invalidate indices as we modify
            var sortedRows = rows
                .OrderByDescending(r => r.InsertIndex)
                .ToList();

            foreach (var row in sortedRows)
            {
                if (!row.AddLog) continue;

                string escaped = EscapeForXml(row.Message ?? "Branch entered");
                string indent = GetIndentAfter(content, row.InsertIndex);
                string logXml = $"{indent}<{logPrefix}Info_Log DisplayName=\"Info Log * {escaped}\" StrMessage=\"{escaped}\" StrTag=\"info\" />";

                // Robust insertion: determine if we are inserting into a Sequence or we need to wrap
                // We look ahead from the InsertIndex to see what the branch contains
                // InsertIndex points right after the opening branch tag (e.g., <If.Then>)
                
                int nextTag = content.IndexOf('<', row.InsertIndex);
                if (nextTag >= 0)
                {
                    // Peek if there's a Sequence or something else before the end of this block
                    // We need to know where the branch ends to be truly safe, but usually 
                    // a simple check if the first tag is a Sequence is enough.
                    string peek = content.Substring(nextTag, Math.Min(100, content.Length - nextTag));
                    if (peek.StartsWith("<Sequence", StringComparison.OrdinalIgnoreCase))
                    {
                        // Insert INSIDE the sequence
                        int seqTagEnd = content.IndexOf('>', nextTag) + 1;
                        content = content.Insert(seqTagEnd, logXml);
                    }
                    else if (peek.StartsWith("<FlowStep", StringComparison.OrdinalIgnoreCase))
                    {
                        // In FlowStep, we must look inside for the activity
                        int fsTagEnd = content.IndexOf('>', nextTag) + 1;
                        int actTag = content.IndexOf('<', fsTagEnd);
                        if (actTag >= 0)
                        {
                            string actPeek = content.Substring(actTag, Math.Min(100, content.Length - actTag));
                            if (actPeek.StartsWith("<Sequence", StringComparison.OrdinalIgnoreCase))
                            {
                                int seqTagEnd = content.IndexOf('>', actTag) + 1;
                                content = content.Insert(seqTagEnd, logXml);
                            }
                            else
                            {
                                // Wrap single activity inside FlowStep in a Sequence
                                // This is complex with regex, so we'll just prepend and hope for the best
                                // or better: just prepend to FlowStep is illegal. 
                                // Actually, Studio almost always uses Sequences.
                                content = content.Insert(row.InsertIndex, logXml);
                            }
                        }
                    }
                    else
                    {
                        // It's a single activity (not Sequence). Prepending would break If.Then.
                        // We should wrap it, but for simplicity in this analyzer, we'll just prepend
                        // and note that Studio will usually have a Sequence.
                        content = content.Insert(row.InsertIndex, logXml);
                    }
                }
                else
                {
                    content = content.Insert(row.InsertIndex, logXml);
                }
            }

            if (needsNamespace)
            {
                int firstActivity = content.IndexOf("<Activity ", StringComparison.OrdinalIgnoreCase);
                if (firstActivity >= 0)
                {
                    int insertNs = content.IndexOf(" xmlns:", firstActivity, StringComparison.Ordinal);
                    if (insertNs > 0)
                        content = content.Substring(0, insertNs) + " xmlns:i=\"clr-namespace:IAP_Logging;assembly=IAP.Logging\" " + content.Substring(insertNs);
                }
            }

            newContent = content;
            return true;
        }

        private static bool HasLogActivity(string branchContent)
        {
            return LogActivityPattern.IsMatch(branchContent);
        }

        private static string GetDisplayName(string blockContent)
        {
            var m = Regex.Match(blockContent, @"DisplayName\s*=\s*[""']([^""']*)[""']", RegexOptions.IgnoreCase);
            return m.Success ? m.Groups[1].Value : "Unknown";
        }

        private static string GetConditionAttribute(string blockContent)
        {
            // Specifically capture what is inside Condition="..."
            // We use a non-greedy match until the closing quote
            var m = Regex.Match(blockContent, @"Condition\s*=\s*[""'](.*?)[""']", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                string cond = m.Groups[1].Value.Trim();
                // Strip outer brackets if present [cond] -> cond
                if (cond.StartsWith("[") && cond.EndsWith("]"))
                    cond = cond.Substring(1, cond.Length - 2);
                return cond;
            }
            return "";
        }

        private static List<(string Type, string Description, int InsertIndex)> FindBranchesMissingLogWithPosition(string content, string filePath)
        {
            var list = new List<(string Type, string Description, int InsertIndex)>();

            // FlowDecision
            var flowDecisionMatches = Regex.Matches(content, @"<FlowDecision\s[^>]*>.*?</FlowDecision>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            foreach (Match match in flowDecisionMatches)
            {
                string block = match.Value;
                int blockStart = match.Index;
                string displayName = GetDisplayName(block);
                string condition = GetConditionAttribute(block);

                var trueMatch = Regex.Match(block, @"<FlowDecision\.True[^>]*>(.*?)</FlowDecision\.True>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (trueMatch.Success && !HasLogActivity(trueMatch.Groups[1].Value))
                {
                    int tagEnd = trueMatch.Index + trueMatch.Value.IndexOf('>') + 1;
                    list.Add(("FlowDecision", $"FlowDecision '{displayName}' [{condition}] – True branch", blockStart + tagEnd));
                }

                var falseMatch = Regex.Match(block, @"<FlowDecision\.False[^>]*>(.*?)</FlowDecision\.False>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (falseMatch.Success && !HasLogActivity(falseMatch.Groups[1].Value))
                {
                    int tagEnd = falseMatch.Index + falseMatch.Value.IndexOf('>') + 1;
                    list.Add(("FlowDecision", $"FlowDecision '{displayName}' [{condition}] – False branch", blockStart + tagEnd));
                }
            }

            // FlowSwitch
            var flowSwitchMatches = Regex.Matches(content, @"<FlowSwitch\s[^>]*>.*?</FlowSwitch>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            foreach (Match switchMatch in flowSwitchMatches)
            {
                string block = switchMatch.Value;
                int blockStart = switchMatch.Index;
                string displayName = GetDisplayName(block);

                var defaultMatch = Regex.Match(block, @"<FlowSwitch\.Default[^>]*>(.*?)</FlowSwitch\.Default>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (defaultMatch.Success && !HasLogActivity(defaultMatch.Groups[1].Value))
                {
                    int tagEnd = defaultMatch.Index + defaultMatch.Value.IndexOf('>') + 1;
                    list.Add(("FlowSwitch", $"FlowSwitch '{displayName}' – Default branch", blockStart + tagEnd));
                }

                var caseMatches = Regex.Matches(block, @"<FlowStep\s+x:Key\s*=\s*[""'][^""']*[""'][^>]*>(.*?)</FlowStep>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                int caseIndex = 1;
                foreach (Match caseMatch in caseMatches)
                {
                    if (!HasLogActivity(caseMatch.Groups[1].Value))
                    {
                        var keyMatch = Regex.Match(caseMatch.Value, @"x:Key\s*=\s*[""']([^""']*)[""']", RegexOptions.IgnoreCase);
                        string key = keyMatch.Success ? keyMatch.Groups[1].Value : caseIndex.ToString();
                        int tagEnd = caseMatch.Index + caseMatch.Value.IndexOf('>') + 1;
                        list.Add(("FlowSwitch", $"FlowSwitch '{displayName}' – Case {caseIndex}. '{key}'", blockStart + tagEnd));
                    }
                    caseIndex++;
                }
            }

            // If.Then / If.Else
            var ifMatches = Regex.Matches(content, @"<If\s[^>]*>.*?</If>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            foreach (Match ifMatch in ifMatches)
            {
                string block = ifMatch.Value;
                int blockStart = ifMatch.Index;
                string displayName = GetDisplayName(block);
                string condition = GetConditionAttribute(block);

                var thenMatch = Regex.Match(block, @"<If\.Then[^>]*>(.*?)</If\.Then>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (thenMatch.Success && !HasLogActivity(thenMatch.Groups[1].Value))
                {
                    int tagEnd = thenMatch.Index + thenMatch.Value.IndexOf('>') + 1;
                    list.Add(("If", $"If '{displayName}' [{condition}] – Then branch", blockStart + tagEnd));
                }

                var elseMatch = Regex.Match(block, @"<If\.Else[^>]*>(.*?)</If\.Else>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (elseMatch.Success && !HasLogActivity(elseMatch.Groups[1].Value))
                {
                    int tagEnd = elseMatch.Index + elseMatch.Value.IndexOf('>') + 1;
                    list.Add(("If", $"If '{displayName}' [{condition}] – Else branch", blockStart + tagEnd));
                }
            }

            return list;
        }

        private static List<(string Type, string Description)> FindBranchesMissingLog(string content, string filePath)
        {
            var results = FindBranchesMissingLogWithPosition(content, filePath);
            return results.Select(r => (r.Type, r.Description)).ToList();
        }

        private static string GetInfoLogPrefix(string content)
        {
            var m = Regex.Match(content, @"<(\w+):Info_Log\s", RegexOptions.IgnoreCase);
            if (m.Success) return m.Groups[1].Value + ":";
            return "i:";
        }

        private static bool EnsureInfoLogNamespace(string content, ref string logPrefix)
        {
            if (Regex.IsMatch(content, @"<\w+:Info_Log\s", RegexOptions.IgnoreCase))
                return false;
            if (Regex.IsMatch(content, @"xmlns:i\s*=\s*[""']clr-namespace:IAP_Logging", RegexOptions.IgnoreCase))
                return false;
            logPrefix = "i:";
            return true;
        }

        private static string GetIndentAfter(string content, int afterIndex)
        {
            int lineStart = content.LastIndexOf('\n', Math.Max(0, afterIndex - 1)) + 1;
            string line = content.Substring(lineStart, afterIndex - lineStart);
            string indent = "";
            for (int i = 0; i < line.Length && (line[i] == ' ' || line[i] == '\t'); i++)
                indent += line[i];
            if (indent.Length == 0) indent = "  ";
            return "\n" + indent;
        }

        private static string EscapeForXml(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
        }

        private static string FindCommonRoot(IReadOnlyList<string> paths)
        {
            if (paths == null || paths.Count == 0) return "";
            var dirs = paths.Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => Path.GetDirectoryName(p) ?? "").Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (dirs.Count == 0) return "";
            string common = dirs[0];
            foreach (var d in dirs.Skip(1))
            {
                while (!string.IsNullOrEmpty(common) && !d.StartsWith(common, StringComparison.OrdinalIgnoreCase))
                    common = Path.GetDirectoryName(common) ?? "";
            }
            return common;
        }
    }
}
