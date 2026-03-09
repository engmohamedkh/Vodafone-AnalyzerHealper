using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AnalyzerHelper.Interfaces;

namespace AnalyzerHelper.Rules
{
    /// <summary>Applies DefineAndFix for selected rules on selected files (Fix tab).</summary>
    public static class FixRunner
    {
        public class FixResult
        {
            public string RuleId { get; set; } = "";
            public string RuleName { get; set; } = "";
            public string FilePath { get; set; } = "";
            public bool Applied { get; set; }
            public string Message { get; set; } = "";
        }

        public static IReadOnlyList<FixResult> ApplyFix(
            IReadOnlyList<string> filePaths,
            IReadOnlyList<IAnalyzerRule> rules,
            Func<IAnalyzerRule, string, bool>? userConfirmed = null)
        {
            var results = new List<FixResult>();
            foreach (var path in filePaths ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    results.Add(new FixResult { FilePath = path, Message = "File not found or empty path." });
                    continue;
                }

                string content;
                try
                {
                    content = File.ReadAllText(path);
                }
                catch (Exception ex)
                {
                    results.Add(new FixResult { FilePath = path, Message = "Read failed: " + ex.Message });
                    continue;
                }

                foreach (var rule in rules ?? Array.Empty<IAnalyzerRule>())
                {
                    if (rule is not IAnalyzerRuleWithFix withFix)
                        continue;

                    // For need-interaction rules: only show confirmation for files that have findings
                    if (rule.RequiresUserInteraction)
                    {
                        var checkResults = rule.Check(path, content);
                        if (checkResults.Count == 0)
                        {
                            results.Add(new FixResult { RuleId = rule.RuleId, RuleName = rule.RuleName, FilePath = path, Message = "No findings in file." });
                            continue;
                        }
                        if (userConfirmed?.Invoke(rule, path) != true)
                        {
                            results.Add(new FixResult { RuleId = rule.RuleId, RuleName = rule.RuleName, FilePath = path, Message = "User did not confirm." });
                            continue;
                        }
                    }

                    bool applied = false;
                    string message = "";
                    try
                    {
                        if (withFix.DefineAndFix(path, content, out string newContent))
                        {
                            // Only write if the file still exists at path (rule may have renamed it)
                            if (File.Exists(path))
                                File.WriteAllText(path, newContent);
                            applied = true;
                            message = "Fix applied.";
                        }
                        else
                            message = "No change (rule did not apply fix).";
                    }
                    catch (Exception ex)
                    {
                        message = ex.Message;
                    }

                    results.Add(new FixResult
                    {
                        RuleId = rule.RuleId,
                        RuleName = rule.RuleName,
                        FilePath = path,
                        Applied = applied,
                        Message = message
                    });
                }
            }

            return results;
        }
    }
}
