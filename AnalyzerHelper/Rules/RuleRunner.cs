using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AnalyzerHelper.Interfaces;
using AnalyzerHelper.Models;

namespace AnalyzerHelper.Rules
{
    /// <summary>Runs standard rules on selected files and returns all findings.</summary>
    public static class RuleRunner
    {
        public static IReadOnlyList<RuleCheckResult> Run(
            IReadOnlyList<string> filePaths,
            IReadOnlyList<IAnalyzerRule> rules)
        {
            var results = new List<RuleCheckResult>();
            foreach (var path in filePaths ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) continue;
                string content;
                try
                {
                    content = File.ReadAllText(path);
                }
                catch (Exception ex)
                {
                    results.Add(new RuleCheckResult
                    {
                        RuleId = "SYS",
                        RuleName = "File read",
                        Level = RuleLevel.Error,
                        Message = "Could not read file: " + ex.Message,
                        FilePath = path,
                        Recommendation = "Check file permissions and path."
                    });
                    continue;
                }

                foreach (var rule in rules ?? Array.Empty<IAnalyzerRule>())
                {
                    try
                    {
                        var findings = rule.Check(path, content);
                        if (findings != null)
                        {
                            foreach (var r in findings)
                            {
                                if (string.IsNullOrEmpty(r.FilePath)) r.FilePath = path;
                                if (string.IsNullOrEmpty(r.Recommendation)) r.Recommendation = rule.DefaultRecommendation;
                                results.Add(r);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        results.Add(new RuleCheckResult
                        {
                            RuleId = rule.RuleId,
                            RuleName = rule.RuleName,
                            Level = RuleLevel.Error,
                            Message = "Rule threw: " + ex.Message,
                            FilePath = path,
                            Recommendation = rule.DefaultRecommendation
                        });
                    }
                }
            }

            return results;
        }
    }
}
