using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AnalyzerHelper.Interfaces;
using AnalyzerHelper.Models;
using AnalyzerHelper.View;

namespace AnalyzerHelper.Rules
{
    /// <summary>
    /// Validates that FlowDecision (True/False) and FlowSwitch (Default and Cases) branches
    /// contain an Info_Log, Message_Log, or Error_Log activity. Fix requires user interaction: shows a dialog
    /// with branch details and message input per branch, then inserts Info_Log with the user message.
    /// Reference: VodafoneWorkFlowsCustomRules/WorflowsBranchesLogMessages.cs and
    /// VodafoneActivitiesCustomRules/BranchesLogMessages.cs.
    /// </summary>
    public sealed class BranchesLogsRule : IAnalyzerRuleWithFix
    {
        public string RuleId => "VF-038"; 
        public string RuleName => "Branches Logging";
        public string DefaultRecommendation => "Add Info Log, Message Log, or Error Log at the beginning of every branch. Use Fix to add Info Log with your message.";
        public bool RequiresUserInteraction => true;

        /// <summary>Branch meets the rule if it contains Info_Log, Message_Log, or Error_Log.</summary>
        private static readonly Regex LogActivityPattern = new Regex(@"<\w+:(Info_Log|Message_Log|Error_Log)\s", RegexOptions.IgnoreCase);

        public IReadOnlyList<RuleCheckResult> Check(string filePath, string content)
        {
            var results = new List<RuleCheckResult>();
            if (string.IsNullOrWhiteSpace(content)) return results;

            var findings = FindBranchesMissingLog(content, filePath);
            foreach (var desc in findings)
            {
                results.Add(new RuleCheckResult
                {
                    RuleId = RuleId,
                    RuleName = RuleName,
                    Level = RuleLevel.Warning,
                    Message = desc,
                    FilePath = filePath,
                    Recommendation = DefaultRecommendation,
                    RequiresUserInteraction = true
                });
            }
            return results;
        }

        public bool DefineAndFix(string filePath, string content, out string newContent)
        {
            newContent = content;
            if (string.IsNullOrWhiteSpace(content)) return false;

            var findings = FindBranchesMissingLogWithPosition(content, filePath);
            if (findings.Count == 0) return false;

            var branchInfos = findings.Select(f => (f.Description, Source: filePath)).ToList();
            var dialog = new BranchLogConfigWindow(branchInfos);
            if (dialog.ShowDialog() != true) return false;

            var messages = dialog.GetMessages();
            if (messages == null || messages.Count != findings.Count) return false;

            string logPrefix = GetInfoLogPrefix(content);
            bool needsNamespace = EnsureInfoLogNamespace(content, ref logPrefix);

            var sortedFindings = findings
                .Select((f, i) => (f.InsertIndex, Message: messages[i], f.Description))
                .OrderByDescending(x => x.InsertIndex)
                .ToList();

            foreach (var (insertIndex, message, _) in sortedFindings)
            {
                string escaped = EscapeForXml(message ?? "Branch entered");
                string indent = GetIndentAfter(content, insertIndex);
                string logXml = $"{indent}<{logPrefix}Info_Log DisplayName=\"Info Log\" StrMessage=\"{escaped}\" StrTag=\"info\" />";
                content = content.Substring(0, insertIndex) + logXml + content.Substring(insertIndex);
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

        /// <summary>True if branch contains an Info_Log, Message_Log, or Error_Log activity.</summary>
        private static bool HasLogActivity(string branchContent)
        {
            return LogActivityPattern.IsMatch(branchContent);
        }

        private static string GetDisplayName(string blockContent)
        {
            var m = Regex.Match(blockContent, @"DisplayName\s*=\s*[""']([^""']*)[""']", RegexOptions.IgnoreCase);
            return m.Success ? m.Groups[1].Value : "Unknown";
        }

        /// <summary>Find branches missing log (for Check). Returns descriptions only.</summary>
        private static List<string> FindBranchesMissingLog(string content, string filePath)
        {
            var list = new List<string>();

            // FlowDecision: True and False branches
            var flowDecMatches = Regex.Matches(content, @"<FlowDecision\s[^>]*>.*?</FlowDecision>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            foreach (Match decMatch in flowDecMatches)
            {
                string block = decMatch.Value;
                string displayName = GetDisplayName(block);

                var trueMatch = Regex.Match(block, @"<FlowDecision\.True[^>]*>(.*?)</FlowDecision\.True>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (trueMatch.Success && !HasLogActivity(trueMatch.Groups[1].Value))
                    list.Add($"FlowDecision '{displayName}' – True branch has no Info Log, Message Log, or Error Log.");

                var falseMatch = Regex.Match(block, @"<FlowDecision\.False[^>]*>(.*?)</FlowDecision\.False>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (falseMatch.Success && !HasLogActivity(falseMatch.Groups[1].Value))
                    list.Add($"FlowDecision '{displayName}' – False branch has no Info Log, Message Log, or Error Log.");
            }

            // FlowSwitch: Default and Cases
            var flowSwitchMatches = Regex.Matches(content, @"<FlowSwitch\s[^>]*>.*?</FlowSwitch>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            foreach (Match switchMatch in flowSwitchMatches)
            {
                string block = switchMatch.Value;
                string displayName = GetDisplayName(block);

                var defaultMatch = Regex.Match(block, @"<FlowSwitch\.Default[^>]*>(.*?)</FlowSwitch\.Default>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (defaultMatch.Success && !HasLogActivity(defaultMatch.Groups[1].Value))
                    list.Add($"FlowSwitch '{displayName}' – Default branch has no Info Log, Message Log, or Error Log.");

                var caseMatches = Regex.Matches(block, @"<FlowStep\s+x:Key\s*=\s*[""'][^""']*[""'][^>]*>(.*?)</FlowStep>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                int caseIndex = 0;
                foreach (Match caseMatch in caseMatches)
                {
                    if (!HasLogActivity(caseMatch.Groups[1].Value))
                    {
                        var keyMatch = Regex.Match(caseMatch.Value, @"x:Key\s*=\s*[""']([^""']*)[""']", RegexOptions.IgnoreCase);
                        string key = keyMatch.Success ? keyMatch.Groups[1].Value : caseIndex.ToString();
                        list.Add($"FlowSwitch '{displayName}' – Case '{key}' has no Info Log, Message Log, or Error Log.");
                    }
                    caseIndex++;
                }
            }

            // If.Then / If.Else (activity model)
            var ifMatches = Regex.Matches(content, @"<If\s[^>]*>.*?</If>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            foreach (Match ifMatch in ifMatches)
            {
                string block = ifMatch.Value;
                string displayName = GetDisplayName(block);

                var thenMatch = Regex.Match(block, @"<If\.Then[^>]*>(.*?)</If\.Then>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (thenMatch.Success && !HasLogActivity(thenMatch.Groups[1].Value))
                    list.Add($"If '{displayName}' – Then branch has no Info Log, Message Log, or Error Log.");

                var elseMatch = Regex.Match(block, @"<If\.Else[^>]*>(.*?)</If\.Else>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (elseMatch.Success && !HasLogActivity(elseMatch.Groups[1].Value))
                    list.Add($"If '{displayName}' – Else branch has no Info Log, Message Log, or Error Log.");
            }

            return list;
        }

        /// <summary>Find branches missing log with insert position (for DefineAndFix).</summary>
        private static List<(string Description, int InsertIndex)> FindBranchesMissingLogWithPosition(string content, string filePath)
        {
            var list = new List<(string Description, int InsertIndex)>();

            // FlowDecision
            var flowDecMatches = Regex.Matches(content, @"<FlowDecision\s[^>]*>.*?</FlowDecision>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            foreach (Match decMatch in flowDecMatches)
            {
                string block = decMatch.Value;
                int blockStart = decMatch.Index;
                string displayName = GetDisplayName(block);

                var trueMatch = Regex.Match(block, @"<FlowDecision\.True[^>]*>(.*?)</FlowDecision\.True>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (trueMatch.Success && !HasLogActivity(trueMatch.Groups[1].Value))
                {
                    int tagEnd = trueMatch.Index + trueMatch.Value.IndexOf('>') + 1;
                    list.Add(($"FlowDecision '{displayName}' – True branch", blockStart + tagEnd));
                }

                var falseMatch = Regex.Match(block, @"<FlowDecision\.False[^>]*>(.*?)</FlowDecision\.False>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (falseMatch.Success && !HasLogActivity(falseMatch.Groups[1].Value))
                {
                    int tagEnd = falseMatch.Index + falseMatch.Value.IndexOf('>') + 1;
                    list.Add(($"FlowDecision '{displayName}' – False branch", blockStart + tagEnd));
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
                    list.Add(($"FlowSwitch '{displayName}' – Default branch", blockStart + tagEnd));
                }

                var caseMatches = Regex.Matches(block, @"<FlowStep\s+x:Key\s*=\s*[""'][^""']*[""'][^>]*>(.*?)</FlowStep>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                int caseIndex = 0;
                foreach (Match caseMatch in caseMatches)
                {
                    if (!HasLogActivity(caseMatch.Groups[1].Value))
                    {
                        var keyMatch = Regex.Match(caseMatch.Value, @"x:Key\s*=\s*[""']([^""']*)[""']", RegexOptions.IgnoreCase);
                        string key = keyMatch.Success ? keyMatch.Groups[1].Value : caseIndex.ToString();
                        int tagEnd = caseMatch.Index + caseMatch.Value.IndexOf('>') + 1;
                        list.Add(($"FlowSwitch '{displayName}' – Case '{key}'", blockStart + tagEnd));
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

                var thenMatch = Regex.Match(block, @"<If\.Then[^>]*>(.*?)</If\.Then>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (thenMatch.Success && !HasLogActivity(thenMatch.Groups[1].Value))
                {
                    int tagEnd = thenMatch.Index + thenMatch.Value.IndexOf('>') + 1;
                    list.Add(($"If '{displayName}' – Then branch", blockStart + tagEnd));
                }

                var elseMatch = Regex.Match(block, @"<If\.Else[^>]*>(.*?)</If\.Else>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (elseMatch.Success && !HasLogActivity(elseMatch.Groups[1].Value))
                {
                    int tagEnd = elseMatch.Index + elseMatch.Value.IndexOf('>') + 1;
                    list.Add(($"If '{displayName}' – Else branch", blockStart + tagEnd));
                }
            }

            return list;
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
            if (indent.Length == 0) indent = "\n  ";
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
    }
}
