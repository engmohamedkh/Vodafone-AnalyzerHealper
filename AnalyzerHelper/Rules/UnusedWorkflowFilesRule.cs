using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AnalyzerHelper.Interfaces;
using AnalyzerHelper.Models;
using AnalyzerHelper.View;

namespace AnalyzerHelper.Rules
{
    /// <summary>
    /// Finds .xaml files under Automation, Subprocess, and Logic that are never referenced
    /// from any other workflow in the project. Fix: one batch dialog (PrepareBatch), then deletes
    /// chosen files when DefineAndFix runs per path — same pattern as AnnotationRule / BranchesLogsRule.
    /// </summary>
    public sealed class UnusedWorkflowFilesRule : IBatchAnalyzerRuleWithFix
    {
        public string RuleId => "VF-039";
        public string RuleName => "Unused workflow files";
        public string DefaultRecommendation =>
            "Remove unused workflows from Automation, Subprocess, and Logic or invoke them from another workflow.";
        public bool RequiresUserInteraction => true;

        private bool _batchPrepared;
        private bool _batchCancelled;
        private HashSet<string>? _pathsToDelete;

        public IReadOnlyList<RuleCheckResult> Check(string filePath, string content)
        {
            var results = new List<RuleCheckResult>();
            if (string.IsNullOrWhiteSpace(filePath)) return results;

            string? root = FindProjectRoot(filePath);
            if (root == null) return results;

            if (!IsUnderMonitoredFolders(filePath, root)) return results;

            IReadOnlyList<string> unused = ComputeUnusedFiles(root);
            if (!unused.Any(f => PathsEqual(f, filePath))) return results;

            string rel = Path.GetRelativePath(root, filePath);
            results.Add(new RuleCheckResult
            {
                RuleId = RuleId,
                RuleName = RuleName,
                Level = RuleLevel.Warning,
                Message = $"Unused workflow (not referenced by another .xaml in this project): {rel}",
                FilePath = filePath,
                Recommendation = DefaultRecommendation,
                RequiresUserInteraction = RequiresUserInteraction
            });
            return results;
        }

        /// <summary>Show one dialog per project root that has unused files; collect paths to delete.</summary>
        public void PrepareBatch(IReadOnlyList<string> filePaths)
        {
            _batchPrepared = false;
            _batchCancelled = false;
            _pathsToDelete = null;

            var projectRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string? p in filePaths ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(p) || !File.Exists(p)) continue;
                string? r = FindProjectRoot(p);
                if (r != null) projectRoots.Add(Path.GetFullPath(r));
            }

            if (projectRoots.Count == 0)
            {
                _batchCancelled = true;
                return;
            }

            var toDelete = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool anyUnused = false;

            foreach (string projRoot in projectRoots.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                IReadOnlyList<string> unused = ComputeUnusedFiles(projRoot);
                if (unused.Count == 0) continue;

                anyUnused = true;
                var dialog = new UnusedWorkflowFilesWindow(projRoot, unused);
                if (dialog.ShowDialog() != true)
                {
                    _batchCancelled = true;
                    return;
                }

                foreach (string path in dialog.GetFilesMarkedForDeletion())
                    toDelete.Add(Path.GetFullPath(path));
            }

            if (!anyUnused)
            {
                _batchCancelled = true;
                return;
            }

            _pathsToDelete = toDelete;
            _batchPrepared = true;
        }

        public bool DefineAndFix(string filePath, string content, out string newContent)
        {
            newContent = content;
            if (_batchCancelled || !_batchPrepared || _pathsToDelete == null || _pathsToDelete.Count == 0)
                return false;

            string full = Path.GetFullPath(filePath);
            if (!_pathsToDelete.Contains(full))
                return false;

            try
            {
                if (File.Exists(full))
                    File.Delete(full);
            }
            catch
            {
                return false;
            }

            _pathsToDelete.Remove(full);
            return true;
        }

        /// <summary>Walk up from file until a folder contains Automation, Subprocess, or Logic.</summary>
        internal static string? FindProjectRoot(string filePath)
        {
            try
            {
                string? dir = Path.GetDirectoryName(Path.GetFullPath(filePath));
                while (!string.IsNullOrEmpty(dir))
                {
                    if (HasMonitoredChildFolder(dir))
                        return dir;
                    dir = Path.GetDirectoryName(dir);
                }
            }
            catch
            {
                /* ignore */
            }
            return null;
        }

        private static bool HasMonitoredChildFolder(string dir)
        {
            foreach (string name in new[] { "Automation", "Subprocess", "Logic" })
            {
                try
                {
                    string p = Path.Combine(dir, name);
                    if (Directory.Exists(p)) return true;
                }
                catch { /* ignore */ }
            }
            return false;
        }

        private static bool IsUnderMonitoredFolders(string filePath, string root)
        {
            string rel = Path.GetRelativePath(root, Path.GetFullPath(filePath));
            var parts = rel.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (parts.Length == 0) return false;
            return parts.Any(p => MonitoredFolderNames.Contains(p, StringComparer.OrdinalIgnoreCase));
        }

        private static readonly HashSet<string> MonitoredFolderNames =
            new(StringComparer.OrdinalIgnoreCase) { "Automation", "Subprocess", "Logic" };

        internal static IReadOnlyList<string> ComputeUnusedFiles(string root)
        {
            root = Path.GetFullPath(root);
            var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string folderName in MonitoredFolderNames)
            {
                string sub = Path.Combine(root, folderName);
                if (!Directory.Exists(sub)) continue;
                foreach (string xaml in Directory.GetFiles(sub, "*.xaml", SearchOption.AllDirectories))
                    targets.Add(Path.GetFullPath(xaml));
            }

            if (targets.Count == 0) return Array.Empty<string>();

            string[] allXaml = Directory.GetFiles(root, "*.xaml", SearchOption.AllDirectories)
                .Select(Path.GetFullPath).ToArray();

            var unused = new List<string>();
            foreach (string t in targets.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                bool referenced = false;
                foreach (string s in allXaml)
                {
                    if (PathsEqual(s, t)) continue;
                    string txt;
                    try { txt = File.ReadAllText(s); }
                    catch { continue; }
                    if (ContentReferencesWorkflow(txt, t, root, targets))
                    {
                        referenced = true;
                        break;
                    }
                }
                if (!referenced) unused.Add(t);
            }

            return unused;
        }

        internal static bool ContentReferencesWorkflow(string scannerContent, string candidateFullPath, string root,
            HashSet<string> targetSet)
        {
            string rel = Path.GetRelativePath(root, candidateFullPath).Replace('/', '\\');
            string relAlt = rel.Replace('\\', '/');

            if (scannerContent.IndexOf(rel, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (scannerContent.IndexOf(relAlt, StringComparison.OrdinalIgnoreCase) >= 0) return true;

            string fileName = Path.GetFileName(candidateFullPath);
            int sameName = targetSet.Count(f => string.Equals(Path.GetFileName(f), fileName, StringComparison.OrdinalIgnoreCase));
            if (sameName == 1 &&
                scannerContent.IndexOf(fileName, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            return false;
        }

        private static bool PathsEqual(string a, string b) =>
            string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
    }
}
