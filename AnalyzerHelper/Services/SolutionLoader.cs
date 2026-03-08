using System.Collections.Generic;
using System.IO;
using AnalyzerHelper.Models;

namespace AnalyzerHelper.Services
{
    /// <summary>Loads UiPath solution .xaml files from a given folder path.</summary>
    public static class SolutionLoader
    {
        public static IReadOnlyList<SolutionFileItem> LoadWorkflowFiles(string solutionPath)
        {
            var list = new List<SolutionFileItem>();
            if (string.IsNullOrWhiteSpace(solutionPath) || !Directory.Exists(solutionPath))
                return list;

            var root = Path.GetFullPath(solutionPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            try
            {
                foreach (var fullPath in Directory.GetFiles(root, "*.xaml", SearchOption.AllDirectories))
                {
                    try
                    {
                        var relative = Path.GetRelativePath(root, fullPath);
                        list.Add(new SolutionFileItem { FullPath = fullPath, DisplayPath = relative });
                    }
                    catch { /* skip */ }
                }
            }
            catch { /* return what we have */ }

            return list;
        }
    }
}
