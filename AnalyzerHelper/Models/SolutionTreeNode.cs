using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AnalyzerHelper.Models
{
    /// <summary>
    /// Node in the file tree: either a folder (expandable) or a file (leaf with checkbox).
    /// </summary>
    public class SolutionTreeNode : INotifyPropertyChanged
    {
        private bool _isExpanded = true;

        public string DisplayName { get; set; } = "";
        public bool IsFolder { get; set; }
        public ObservableCollection<SolutionTreeNode> Children { get; } = new ObservableCollection<SolutionTreeNode>();
        public SolutionFileItem? FileItem { get; set; }

        public bool IsExpanded
        {
            get => _isExpanded;
            set { if (_isExpanded == value) return; _isExpanded = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>Builds a folder tree from a flat list of solution files.</summary>
    public static class SolutionTreeBuilder
    {
        public static IReadOnlyList<SolutionTreeNode> BuildTree(IEnumerable<SolutionFileItem> files)
        {
            var root = new List<SolutionTreeNode>();
            var folderMap = new Dictionary<string, SolutionTreeNode>(System.StringComparer.OrdinalIgnoreCase);

            foreach (var file in files)
            {
                var parts = file.DisplayPath.Replace('\\', '/').Split('/');
                if (parts.Length == 0) continue;
                string currentPath = "";
                SolutionTreeNode? parentFolder = null;

                for (int i = 0; i < parts.Length - 1; i++)
                {
                    string segment = parts[i];
                    currentPath = string.IsNullOrEmpty(currentPath) ? segment : currentPath + "/" + segment;
                    if (!folderMap.TryGetValue(currentPath, out var folder))
                    {
                        folder = new SolutionTreeNode { DisplayName = segment, IsFolder = true };
                        folderMap[currentPath] = folder;
                        if (parentFolder != null)
                            parentFolder.Children.Add(folder);
                        else
                            root.Add(folder);
                    }
                    parentFolder = folder;
                }

                var fileNode = new SolutionTreeNode
                {
                    DisplayName = parts[parts.Length - 1],
                    IsFolder = false,
                    FileItem = file
                };
                if (parentFolder != null)
                    parentFolder.Children.Add(fileNode);
                else
                    root.Add(fileNode);
            }

            return root;
        }
    }
}
