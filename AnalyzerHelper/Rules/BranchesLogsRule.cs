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
        public string RuleId => "VF-018"; 
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
                throw new OperationCanceledException($"User cancelled the {RuleName} batch fix dialog.");
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

            // Track new FlowStep IDs so we can register their <x:Reference> entries
            var newFlowStepIds = new List<string>();

            foreach (var row in sortedRows)
            {
                if (!row.AddLog) continue;

                string escaped = EscapeForXml(row.Message ?? "Branch entered");
                string indent = GetIndentAfter(content, row.InsertIndex);
                string chosenLog = row.LogType ?? "Info_Log";
                string chosenDisplayName = chosenLog.Replace("_Log", " Log");
                string chosenTag = chosenLog.ToLower().Replace("_log", "");
                string logXml = $"{indent}<{logPrefix}{chosenLog} DisplayName=\"{chosenDisplayName}\" StrMessage=\"{escaped}\" StrTag=\"{chosenTag}\" />";

                if (row.Type == "FlowDecision" || row.Type == "FlowSwitch")
                {
                    // ── Flowchart branch insertion ──
                    // Find the node currently in the branch
                    int nextTagAtBranch = content.IndexOf('<', row.InsertIndex);
                    if (nextTagAtBranch >= 0)
                    {
                        // Derive the branch property name (True/False/case) from BranchDescription
                        string branchPropName = row.BranchDescription.Contains("False", StringComparison.OrdinalIgnoreCase) ? "False" : "True";
                        
                        // Detect prefix from the property tag (e.g., <FlowDecision.True>)
                        string branchTagName = row.Type + "." + branchPropName;
                        int branchTagStart = content.LastIndexOf("<" + branchTagName, row.InsertIndex, StringComparison.OrdinalIgnoreCase);
                        string prefix = "";
                        if (branchTagStart >= 0 && branchTagStart + 50 < content.Length && content.Substring(branchTagStart, 50).Contains(":"))
                        {
                            int colon = content.IndexOf(':', branchTagStart);
                            int space = content.IndexOf(' ', branchTagStart);
                            if (colon > 0 && (space < 0 || colon < space))
                                prefix = content.Substring(branchTagStart + 1, colon - branchTagStart);
                        }

                        // Get the entire content of the branch to move it
                        int nodeTagNameEnd = nextTagAtBranch + 1;
                        while (nodeTagNameEnd < content.Length && !" />\t\r\n".Contains(content[nodeTagNameEnd])) nodeTagNameEnd++;
                        string nodeTagName = content.Substring(nextTagAtBranch + 1, nodeTagNameEnd - (nextTagAtBranch + 1));

                        int nodeCloseEnd = FindMatchingCloseTag(content, nextTagAtBranch, nodeTagName);
                        if (nodeCloseEnd < 0)
                        {
                            int selfEnd = FindSelfCloseOrOpenEnd(content, nextTagAtBranch);
                            if (selfEnd >= 0 && content[selfEnd - 1] == '/') nodeCloseEnd = selfEnd + 1;
                        }

                        if (nodeCloseEnd > nextTagAtBranch)
                        {
                            string originalNodeHtml = content.Substring(nextTagAtBranch, nodeCloseEnd - nextTagAtBranch);
                            string branchIndent = GetIndentAfter(content, nextTagAtBranch);
                            string innerIndent = branchIndent + "  ";
                            
                            // Generate a unique ID for the new FlowStep
                            string logStepId = "__ReferenceID_Log_" + Guid.NewGuid().ToString("N").Substring(0, 6);
                            
                            // Structure matches UiPath native pattern:
                            // <FlowStep x:Name="...">
                            //   <i:Info_Log ... />           ← activity of the FlowStep
                            //   <FlowStep.Next>
                            //     [original node]            ← the node that was originally in the branch
                            //   </FlowStep.Next>
                            // </FlowStep>
                            string flowStepXml = $@"<{prefix}FlowStep x:Name=""{logStepId}"">
{logXml}
{innerIndent}<{prefix}FlowStep.Next>
{innerIndent}  {originalNodeHtml}
{innerIndent}</{prefix}FlowStep.Next>
{branchIndent}</{prefix}FlowStep>";

                            // Replace the original node with our new FlowStep chain
                            content = content.Remove(nextTagAtBranch, nodeCloseEnd - nextTagAtBranch);
                            content = content.Insert(nextTagAtBranch, flowStepXml);

                            // Track the ID so we can register it as <x:Reference> later
                            newFlowStepIds.Add(logStepId);
                        }
                        else
                        {
                            content = content.Insert(row.InsertIndex, logXml);
                        }
                    }
                    else
                    {
                        content = content.Insert(row.InsertIndex, logXml);
                    }
                }
                else
                {
                    // ── If.Then / If.Else branch insertion ──
                    int nextTag = content.IndexOf('<', row.InsertIndex);
                    if (nextTag >= 0)
                    {
                        string peek = content.Substring(nextTag, Math.Min(100, content.Length - nextTag));
                        if (peek.StartsWith("<Sequence", StringComparison.OrdinalIgnoreCase))
                        {
                            // Insert inside the Sequence (after any properties like Variables)
                            int seqOpenEnd = FindSelfCloseOrOpenEnd(content, nextTag);
                            if (seqOpenEnd >= 0 && content[seqOpenEnd - 1] != '/')
                            {
                                int insertAt = SkipNonActivityElements(content, seqOpenEnd + 1);

                                string innerIndent = GetIndentAfter(content, insertAt);
                                string seqLogXml = $"{innerIndent}<{logPrefix}{chosenLog} DisplayName=\"{chosenDisplayName}\" StrMessage=\"{escaped}\" StrTag=\"{chosenTag}\" />";
                                content = content.Insert(insertAt, seqLogXml);
                            }
                        }
                        else if (!peek.StartsWith("</", StringComparison.Ordinal))
                        {
                            // Single activity without Sequence — wrap in Sequence and add log
                            int actNameEnd = nextTag + 1;
                            while (actNameEnd < content.Length && content[actNameEnd] != ' ' && content[actNameEnd] != '>' && content[actNameEnd] != '/') actNameEnd++;
                            string actTagName = content.Substring(nextTag + 1, actNameEnd - (nextTag + 1));

                            int actCloseEnd = FindMatchingCloseTag(content, nextTag, actTagName);
                            if (actCloseEnd < 0)
                            {
                                int selfEnd = FindSelfCloseOrOpenEnd(content, nextTag);
                                if (selfEnd >= 0 && content[selfEnd - 1] == '/')
                                    actCloseEnd = selfEnd + 1;
                            }

                            if (actCloseEnd > 0)
                            {
                                string actIndent = GetIndentAfter(content, nextTag);
                                string innerIndent = actIndent + "  ";

                                content = content.Insert(actCloseEnd, $"{actIndent}</Sequence>");
                                string seqOpen = $"{actIndent}<Sequence DisplayName=\"Sequence\">{innerIndent}<{logPrefix}{chosenLog} DisplayName=\"{chosenDisplayName}\" StrMessage=\"{escaped}\" StrTag=\"{chosenTag}\" />";
                                content = content.Insert(nextTag, seqOpen);
                            }
                            else
                            {
                                // Fallback
                                content = content.Insert(row.InsertIndex, logXml);
                            }
                        }
                        // else: empty branch (closing tag), just insert
                        else
                        {
                            content = content.Insert(row.InsertIndex, logXml);
                        }
                    }
                    else
                    {
                        content = content.Insert(row.InsertIndex, logXml);
                    }
                }
            }

            // ── Register <x:Reference> entries for all new FlowStep nodes ──
            // Each FlowStep inside a Flowchart MUST have a <x:Reference> entry at the
            // bottom of the enclosing <Flowchart> element, otherwise UiPath cannot
            // resolve the node graph and crashes on load.
            foreach (var refId in newFlowStepIds)
            {
                // Find where this FlowStep lives in the content
                int namePos = content.IndexOf($"x:Name=\"{refId}\"", StringComparison.Ordinal);
                if (namePos < 0) continue;

                // Find the closing </Flowchart> or </prefix:Flowchart> tag that encloses this FlowStep
                var flowchartCloseMatch = Regex.Match(
                    content.Substring(namePos),
                    @"</(\w+:)?Flowchart\s*>",
                    RegexOptions.IgnoreCase);
                if (!flowchartCloseMatch.Success) continue;

                int closeTagPos = namePos + flowchartCloseMatch.Index;

                // Derive indent from the closing tag's line
                int lineStart = content.LastIndexOf('\n', Math.Max(0, closeTagPos - 1)) + 1;
                string refIndent = "";
                for (int ci = lineStart; ci < closeTagPos && (content[ci] == ' ' || content[ci] == '\t'); ci++)
                    refIndent += content[ci];

                // Insert <x:Reference> just before the closing </Flowchart> tag
                string refEntry = $"{refIndent}  <x:Reference>{refId}</x:Reference>\n";
                content = content.Insert(closeTagPos, refEntry);
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
            if (string.IsNullOrWhiteSpace(branchContent)) return false;
            if (LogActivityPattern.IsMatch(branchContent)) return true;
            
            // If the branch only contains a reference to another node (Flowchart style), 
            // we skip it for now as we can't easily insert a log there without knowing the target node.
            if (branchContent.Contains("<x:Reference") || branchContent.Contains("<av:Reference")) return true;
            
            return false;
        }

        private static string GetDisplayName(string blockContent)
        {
            var m = Regex.Match(blockContent, @"DisplayName\s*=\s*[""']([^""']*)[""']", RegexOptions.IgnoreCase);
            return m.Success ? m.Groups[1].Value : "Unknown";
        }

        /// <summary>
        /// Extract DisplayName only from the opening tag (up to the first '>'),
        /// so we don't accidentally match a nested element's DisplayName.
        /// </summary>
        private static string GetDisplayNameFromOpenTag(string content, int openTagStart)
        {
            int tagEnd = content.IndexOf('>', openTagStart);
            if (tagEnd < 0) return "Unknown";
            string openTag = content.Substring(openTagStart, tagEnd - openTagStart + 1);
            var m = Regex.Match(openTag, @"DisplayName\s*=\s*[""']([^""']*)[""']", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                string name = m.Groups[1].Value;
                return name.Replace("&gt;", ">").Replace("&lt;", "<").Replace("&amp;", "&").Replace("&quot;", "\"").Replace("&apos;", "'");
            }
            return "Unknown";
        }

        private static string GetConditionAttribute(string blockContent)
        {
            // 1) Try attribute style first: Condition="..."
            var m = Regex.Match(blockContent, @"Condition\s*=\s*[""'](.*?)[""']", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                string cond = m.Groups[1].Value.Trim();
                // Strip outer brackets if present [cond] -> cond
                if (cond.StartsWith("[") && cond.EndsWith("]"))
                    cond = cond.Substring(1, cond.Length - 2);
                // Decode common XML entities for display
                cond = cond.Replace("&gt;", ">").Replace("&lt;", "<").Replace("&amp;", "&");
                return cond;
            }

            // 2) Try element style: <*.Condition> ... ExpressionText="..." ...
            var elemMatch = Regex.Match(blockContent,
                @"<\w+\.Condition[^>]*>\s*<[^>]*ExpressionText\s*=\s*[""'](.*?)[""']",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (elemMatch.Success)
            {
                string cond = elemMatch.Groups[1].Value.Trim();
                cond = cond.Replace("&gt;", ">").Replace("&lt;", "<").Replace("&amp;", "&");
                return cond;
            }

            return "";
        }

        // ── Balanced-tag parsing helpers ──

        /// <summary>
        /// Finds all top-level occurrences of a given XML tag in content,
        /// correctly handling nested instances of the same tag.
        /// Returns a list of (startIndex, blockContent) for each match.
        /// </summary>
        private static List<(int Start, string Block)> FindBalancedBlocks(string content, string tagName)
        {
            var results = new List<(int Start, string Block)>();
            // Pattern to find opening tags: optional prefix:tagName followed by whitespace or >
            string openPattern = $"<(\\w+:)?{tagName}[\\s>]";
            var openRegex = new Regex(openPattern, RegexOptions.IgnoreCase);

            int searchFrom = 0;
            while (searchFrom < content.Length)
            {
                var openMatch = openRegex.Match(content, searchFrom);
                if (!openMatch.Success) break;

                int blockStart = openMatch.Index;
                
                // Get the actual tag name used (including prefix if present)
                int nameEnd = blockStart + 1;
                while (nameEnd < content.Length && !" />\t\r\n".Contains(content[nameEnd])) nameEnd++;
                string actualTagName = content.Substring(blockStart + 1, nameEnd - (blockStart + 1));

                // Check if this is a self-closing tag
                int selfCloseEnd = FindSelfCloseOrOpenEnd(content, blockStart);
                if (selfCloseEnd >= 0 && content[selfCloseEnd - 1] == '/')
                {
                    string selfBlock = content.Substring(blockStart, selfCloseEnd - blockStart + 1);
                    results.Add((blockStart, selfBlock));
                    searchFrom = selfCloseEnd + 1;
                    continue;
                }

                int closeEnd = FindMatchingCloseTag(content, blockStart, actualTagName);
                if (closeEnd < 0)
                {
                    searchFrom = blockStart + 1;
                    continue;
                }

                string block = content.Substring(blockStart, closeEnd - blockStart);
                results.Add((blockStart, block));
                searchFrom = closeEnd;
            }

            return results;
        }

        // ── Insertion helpers ──

        /// <summary>
        /// Starting from a position inside a parent element (after '>'), skip past ViewState,
        /// property tags (like Sequence.Variables), and other non-activity elements
        /// to find the first real activity element.
        /// Returns the index of the first '<' of the activity, or the index of the parent's
        /// closing tag index (e.g. index of '&lt;/Sequence&gt;') if no activity is found.
        /// </summary>
        private static int SkipNonActivityElements(string content, int startPos)
        {
            int pos = startPos;
            while (pos < content.Length)
            {
                int nextTag = content.IndexOf('<', pos);
                if (nextTag < 0) return pos;

                if (content.Substring(nextTag).StartsWith("</", StringComparison.Ordinal))
                    return nextTag; // Reached end of parent - insert here

                // Find tag name
                int nameEnd = nextTag + 1;
                while (nameEnd < content.Length && !" />\t\r\n".Contains(content[nameEnd])) nameEnd++;
                string tagName = content.Substring(nextTag + 1, nameEnd - (nextTag + 1));

                // 1. Skip ViewState (sap:) and other structural containers
                // 2. Skip Property Tags (those containing a dot '.')
                if (tagName.StartsWith("sap:", StringComparison.OrdinalIgnoreCase) || tagName.Contains("."))
                {
                    // Special case: if it's the .Activities property tag, we must go INSIDE it
                    if (tagName.EndsWith(".Activities", StringComparison.OrdinalIgnoreCase))
                    {
                        int openEnd = FindSelfCloseOrOpenEnd(content, nextTag);
                        if (openEnd >= 0 && content[openEnd - 1] != '/')
                        {
                            return SkipNonActivityElements(content, openEnd + 1);
                        }
                    }

                    // Otherwise skip the tag block entirely
                    int closePos = FindMatchingCloseTag(content, nextTag, tagName);
                    if (closePos > 0)
                    {
                        pos = closePos;
                        continue;
                    }
                    else
                    {
                        // Self-closing or malformed
                        int endOpen = FindSelfCloseOrOpenEnd(content, nextTag);
                        pos = (endOpen >= 0) ? endOpen + 1 : nextTag + 1;
                        continue;
                    }
                }

                return nextTag; // Found real activity
            }
            return pos;
        }

        /// <summary>
        /// Returns the index of the '>' that ends the opening tag starting at openTagStart.
        /// If the tag is self-closing (/>) the returned index points to '>'.
        /// Returns -1 if not found.
        /// </summary>
        private static int FindSelfCloseOrOpenEnd(string content, int openTagStart)
        {
            bool inQuote = false;
            char quoteChar = '"';
            for (int i = openTagStart; i < content.Length; i++)
            {
                char c = content[i];
                if (inQuote)
                {
                    if (c == quoteChar) inQuote = false;
                }
                else
                {
                    if (c == '"' || c == '\'') { inQuote = true; quoteChar = c; }
                    else if (c == '>') return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// Starting from an opening tag at openTagStart, finds the end position
        /// (one past the closing '>') of the matching close tag, handling nesting.
        /// </summary>
        private static int FindMatchingCloseTag(string content, int openTagStart, string tagName)
        {
            string closeTag = $"</{tagName}>";
            string openPattern = $"<{tagName}";

            // Move past the opening tag's '>'
            int pos = FindSelfCloseOrOpenEnd(content, openTagStart);
            if (pos < 0) return -1;
            // If self-closing, there's no close tag
            if (pos > 0 && content[pos - 1] == '/') return pos + 1;
            pos++; // move past '>'

            int depth = 1;
            while (pos < content.Length && depth > 0)
            {
                int nextOpen = IndexOfTag(content, tagName, pos, isClose: false);
                int nextClose = content.IndexOf(closeTag, pos, StringComparison.OrdinalIgnoreCase);

                if (nextClose < 0) return -1; // unbalanced

                if (nextOpen >= 0 && nextOpen < nextClose)
                {
                    // Check if this open is self-closing
                    int openEnd = FindSelfCloseOrOpenEnd(content, nextOpen);
                    if (openEnd >= 0 && content[openEnd - 1] == '/')
                    {
                        // Self-closing, doesn't affect depth
                        pos = openEnd + 1;
                    }
                    else
                    {
                        depth++;
                        pos = (openEnd >= 0) ? openEnd + 1 : nextOpen + 1;
                    }
                }
                else
                {
                    depth--;
                    if (depth == 0)
                    {
                        return nextClose + closeTag.Length;
                    }
                    pos = nextClose + closeTag.Length;
                }
            }
            return -1;
        }

        /// <summary>
        /// Find the next occurrence of an opening or closing tag for the given tagName.
        /// For opening: <tagName followed by whitespace or >
        /// </summary>
        private static int IndexOfTag(string content, string tagName, int startIndex, bool isClose)
        {
            if (isClose)
            {
                // Try literal match, then try with prefix
                int idx = content.IndexOf($"</{tagName}>", startIndex, StringComparison.OrdinalIgnoreCase);
                if (idx >= 0) return idx;
                
                // If the tagName passed didn't have a prefix, try finding it with any prefix
                if (!tagName.Contains(":"))
                {
                   var m = Regex.Match(content.Substring(startIndex), $"</(\\w+:)?{tagName}>", RegexOptions.IgnoreCase);
                   if (m.Success) return startIndex + m.Index;
                }
                return -1;
            }

            // For opening tags
            string pattern = tagName.Contains(":") ? $"<{tagName}[\\s>]" : $"<(\\w+:)?{tagName}[\\s>]";
            var match = Regex.Match(content.Substring(startIndex), pattern, RegexOptions.IgnoreCase);
            return match.Success ? startIndex + match.Index : -1;
        }

        /// <summary>
        /// Finds a child property element (e.g. <If.Then>, <FlowDecision.True>) within a block,
        /// correctly handling nesting. Returns the inner content and the position of the opening tag's end.
        /// </summary>
        private static (bool Found, string InnerContent, int InsertIndex) FindPropertyElement(
            string block, int blockStartInContent, string parentTagName, string propertyName)
        {
            string propertyTagName = $"{parentTagName}.{propertyName}";
            
            // Skip the parent's opening tag
            int pos = FindSelfCloseOrOpenEnd(block, 0);
            if (pos < 0) return (false, "", 0);
            pos++; // After '>'

            while (pos < block.Length)
            {
                int nextTag = block.IndexOf('<', pos);
                if (nextTag < 0) break;

                // Is it our target property tag?
                if (block.Substring(nextTag).StartsWith($"<{propertyTagName}", StringComparison.OrdinalIgnoreCase))
                {
                    int endOfName = nextTag + 1 + propertyTagName.Length;
                    if (endOfName >= block.Length || " \t\r\n/>".Contains(block[endOfName]))
                    {
                        int tagEnd = block.IndexOf('>', nextTag);
                        if (tagEnd < 0) break;
                        
                        // Check for self-closing property tag (unlikely for branches but possible)
                        if (block[tagEnd - 1] == '/')
                        {
                            return (true, "", blockStartInContent + tagEnd + 1);
                        }

                        int innerStart = tagEnd + 1;
                        int closePos = FindMatchingCloseTag(block, nextTag, propertyTagName);
                        if (closePos > 0)
                        {
                            // closePos is index after '</propertyTagName>'
                            int closeTagStart = block.LastIndexOf('<', closePos - 1);
                            if (closeTagStart > innerStart)
                            {
                                return (true, block.Substring(innerStart, closeTagStart - innerStart), blockStartInContent + innerStart);
                            }
                        }
                    }
                }

                // If it's a DIFFERENT activity tag, skip its entire content to avoid 'stealing' tags from nested activities
                int spaceOrEnd = block.IndexOfAny(new char[] { ' ', '>', '/' }, nextTag + 1);
                if (spaceOrEnd > nextTag + 1)
                {
                    string tagName = block.Substring(nextTag + 1, spaceOrEnd - (nextTag + 1));
                    if (tagName.StartsWith("/") || tagName.Contains(".") || tagName.Equals("scg:Dictionary", StringComparison.OrdinalIgnoreCase))
                    {
                        // It's a closing tag, a property tag, or viewstate stuff - just move past the tag
                        int endOpen = FindSelfCloseOrOpenEnd(block, nextTag);
                        pos = (endOpen >= 0) ? endOpen + 1 : nextTag + 1;
                    }
                    else
                    {
                        // It's an intruder activity (e.g. <Sequence> or nested <If>). Use balanced skip.
                        int closeEnd = FindMatchingCloseTag(block, nextTag, tagName);
                        if (closeEnd > 0)
                            pos = closeEnd;
                        else
                        {
                            int endOpen = FindSelfCloseOrOpenEnd(block, nextTag);
                            pos = (endOpen >= 0) ? endOpen + 1 : nextTag + 1;
                        }
                    }
                }
                else
                {
                    pos = nextTag + 1;
                }
            }

            return (false, "", 0);
        }

        private static List<(string Type, string Description, int InsertIndex)> FindBranchesMissingLogWithPosition(string content, string filePath)
        {
            var allFindings = new List<(string Type, string Description, int InsertIndex)>();
            SearchRecursively(content, 0, allFindings);
            return allFindings;
        }

        private static void SearchRecursively(string content, int offset, List<(string Type, string Description, int InsertIndex)> allFindings)
        {
            if (string.IsNullOrWhiteSpace(content)) return;

            // Remove commented out blocks from the local content so we don't find them,
            // but keep the string length same to preserve indices (replace with spaces).
            string searchableContent = content;
            var commentBlocks = FindBalancedBlocks(content, "CommentOut");
            foreach (var (cStart, cBlock) in commentBlocks)
            {
                // Fill the commented area with spaces in our local searchable string
                char[] spaces = new string(' ', cBlock.Length).ToCharArray();
                searchableContent = searchableContent.Remove(cStart, cBlock.Length).Insert(cStart, new string(spaces));
            }

            // ── FlowDecision ──
            var flowDecisionBlocks = FindBalancedBlocks(searchableContent, "FlowDecision");
            foreach (var (blockStart, block) in flowDecisionBlocks)
            {
                string displayName = GetDisplayNameFromOpenTag(searchableContent, blockStart);
                string condition = GetConditionAttribute(block);

                // Need prefix for property search
                string actualTagName = GetActualTagNameFromBlock(block);
                string prefix = actualTagName.Contains(":") ? actualTagName.Split(':')[0] : "";

                var trueBranch = FindPropertyElement(block, blockStart + offset, actualTagName, "True");
                if (trueBranch.Found)
                {
                    if (!HasLogActivity(trueBranch.InnerContent))
                        allFindings.Add(("FlowDecision", $"FlowDecision '{displayName}' [{condition}] – True branch", trueBranch.InsertIndex));
                    SearchRecursively(trueBranch.InnerContent, trueBranch.InsertIndex, allFindings);
                }

                var falseBranch = FindPropertyElement(block, blockStart + offset, actualTagName, "False");
                if (falseBranch.Found)
                {
                    if (!HasLogActivity(falseBranch.InnerContent))
                        allFindings.Add(("FlowDecision", $"FlowDecision '{displayName}' [{condition}] – False branch", falseBranch.InsertIndex));
                    SearchRecursively(falseBranch.InnerContent, falseBranch.InsertIndex, allFindings);
                }
            }

            // ── FlowSwitch ──
            var flowSwitchBlocks = FindBalancedBlocks(searchableContent, "FlowSwitch");
            foreach (var (blockStart, block) in flowSwitchBlocks)
            {
                string displayName = GetDisplayNameFromOpenTag(searchableContent, blockStart);
                string actualTagName = GetActualTagNameFromBlock(block);

                var defaultBranch = FindPropertyElement(block, blockStart + offset, actualTagName, "Default");
                if (defaultBranch.Found)
                {
                    if (!HasLogActivity(defaultBranch.InnerContent))
                        allFindings.Add(("FlowSwitch", $"FlowSwitch '{displayName}' – Default branch", defaultBranch.InsertIndex));
                    SearchRecursively(defaultBranch.InnerContent, defaultBranch.InsertIndex, allFindings);
                }

                var caseSteps = FindBalancedBlocks(block, "FlowStep");
                int caseIndex = 1;
                foreach (var (caseStart, caseBlock) in caseSteps)
                {
                    if (!Regex.IsMatch(caseBlock, @"x:Key\s*=", RegexOptions.IgnoreCase)) continue;

                    int tagEnd = caseBlock.IndexOf('>');
                    int absoluteInsertIndex = offset + blockStart + caseStart + tagEnd + 1;

                    if (!HasLogActivity(caseBlock))
                    {
                        var keyMatch = Regex.Match(caseBlock, @"x:Key\s*=\s*[""']([^""']*)[""']", RegexOptions.IgnoreCase);
                        string key = keyMatch.Success ? keyMatch.Groups[1].Value : caseIndex.ToString();
                        allFindings.Add(("FlowSwitch", $"FlowSwitch '{displayName}' – Case {caseIndex}. '{key}'", absoluteInsertIndex));
                    }
                    
                    // Always recurse into the case block (it's a FlowStep)
                    SearchRecursively(caseBlock, absoluteInsertIndex - (tagEnd + 1) + caseStart, allFindings); // Wait, index calc needs care
                    caseIndex++;
                }
            }

            // ── If.Then / If.Else ──
            // IMPORTANT: Skip If blocks that are nested inside FlowDecision/FlowSwitch blocks,
            // because those are already found via recursion into those branches.
            // Without this check, nested Ifs get detected twice (once here, once via recursion).
            var ifBlocks = FindBalancedBlocks(searchableContent, "If");
            foreach (var (blockStart, block) in ifBlocks)
            {
                // Check if this If block is inside any FlowDecision or FlowSwitch block
                bool insideFlowBlock = false;
                foreach (var (fdStart, fdBlock) in flowDecisionBlocks)
                {
                    if (blockStart >= fdStart && blockStart < fdStart + fdBlock.Length)
                    { insideFlowBlock = true; break; }
                }
                if (!insideFlowBlock)
                {
                    foreach (var (fsStart, fsBlock) in flowSwitchBlocks)
                    {
                        if (blockStart >= fsStart && blockStart < fsStart + fsBlock.Length)
                        { insideFlowBlock = true; break; }
                    }
                }
                if (insideFlowBlock) continue;

                string displayName = GetDisplayNameFromOpenTag(searchableContent, blockStart);
                string condition = GetConditionAttribute(block);
                string actualTagName = GetActualTagNameFromBlock(block);

                var thenBranch = FindPropertyElement(block, blockStart + offset, actualTagName, "Then");
                if (thenBranch.Found)
                {
                    if (!HasLogActivity(thenBranch.InnerContent))
                        allFindings.Add(("If", $"If '{displayName}' [{condition}] – Then branch", thenBranch.InsertIndex));
                    SearchRecursively(thenBranch.InnerContent, thenBranch.InsertIndex, allFindings);
                }

                var elseBranch = FindPropertyElement(block, blockStart + offset, actualTagName, "Else");
                if (elseBranch.Found)
                {
                    if (!HasLogActivity(elseBranch.InnerContent))
                        allFindings.Add(("If", $"If '{displayName}' [{condition}] – Else branch", elseBranch.InsertIndex));
                    SearchRecursively(elseBranch.InnerContent, elseBranch.InsertIndex, allFindings);
                }
            }
        }

        private static string GetActualTagNameFromBlock(string block)
        {
            int nameStart = block.IndexOf('<') + 1;
            int nameEnd = nameStart;
            while (nameEnd < block.Length && !" />\t\r\n".Contains(block[nameEnd])) nameEnd++;
            return block.Substring(nameStart, nameEnd - nameStart);
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
