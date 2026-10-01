using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AnalyzerHelper.Interfaces;
using AnalyzerHelper.Models;

namespace AnalyzerHelper.Rules
{
    /// <summary>
    /// VF-046 — Validates project folder structure compliance.
    /// Ensures all .xaml workflow files are placed in designated structured folders
    /// (Navigate, Other, Read, Write, Logic, Subprocess, or Testing) rather than loosely in project directories.
    /// Main workflows (*_worker.xaml, *_loader.xaml, *_loaderworker.xaml, Main.xaml) are exempted.
    /// </summary>
    public sealed class FolderStructure : IAnalyzerRule
    {
        private static readonly HashSet<string> AllowedFolderNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "navigate",
            "other",
            "read",
            "write",
            "logic",
            "subprocess",
            "testing"
        };

        public string RuleId => "VF-046";
        public string RuleName => "Folder Structure";
        public string DefaultRecommendation =>
            "Follow the folder structure of the process. Place workflow files into structured folders (Navigate, Other, Read, Write, Logic, Subprocess, or Testing).";
        public bool RequiresUserInteraction => false;

        public IReadOnlyList<RuleCheckResult> Check(string filePath, string content)
        {
            var results = new List<RuleCheckResult>();

            if (string.IsNullOrWhiteSpace(filePath))
                return results;

            if (!filePath.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
                return results;

            string fileName = Path.GetFileName(filePath);

            // Exempt Main/Loader/Worker workflows typically located at project root
            if (fileName.EndsWith("worker.xaml", StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith("loader.xaml", StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith("loaderworker.xaml", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileName, "Main.xaml", StringComparison.OrdinalIgnoreCase))
            {
                return results;
            }

            string normalized = filePath.Replace('\\', '/');
            var segments = normalized.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            // Check if any enclosing directory is in the allowed structured folders list
            bool inStructuredFolder = false;
            for (int i = 0; i < segments.Length - 1; i++)
            {
                if (AllowedFolderNames.Contains(segments[i]))
                {
                    inStructuredFolder = true;
                    break;
                }
            }

            if (!inStructuredFolder)
            {
                string workflowDisplayName = Path.GetFileNameWithoutExtension(filePath);
                results.Add(new RuleCheckResult
                {
                    RuleId = RuleId,
                    RuleName = RuleName,
                    Level = RuleLevel.Warning,
                    FilePath = filePath,
                    Message = $"Add Sequence {workflowDisplayName} to structured folders",
                    Recommendation = DefaultRecommendation,
                    RequiresUserInteraction = false
                });
            }

            return results;
        }
    }
}
