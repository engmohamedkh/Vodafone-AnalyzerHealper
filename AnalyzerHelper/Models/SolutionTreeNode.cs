using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace AnalyzerHelper.Models
{
    /// <summary>
    /// Node in the file tree: either a folder (expandable) or a file (leaf with checkbox).
    /// Folders also have a checkbox that checks/unchecks all children recursively.
    /// </summary>
    public class SolutionTreeNode : INotifyPropertyChanged
    {
        private bool _isExpanded = true;
        private bool? _isChecked = true;
        private bool _suppressCascade;

        public string DisplayName { get; set; } = "";
        public bool IsFolder { get; set; }
        public ObservableCollection<SolutionTreeNode> Children { get; } = new ObservableCollection<SolutionTreeNode>();
        public SolutionFileItem? FileItem { get; set; }
        public SolutionTreeNode? Parent { get; set; }

        public bool IsExpanded
        {
            get => _isExpanded;
            set { if (_isExpanded == value) return; _isExpanded = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// For folders: true = all checked, false = all unchecked, null = mixed.
        /// For files: bound to FileItem.IsSelected.
        /// </summary>
        public bool? IsChecked
        {
            get
            {
                if (!IsFolder)
                    return FileItem?.IsSelected ?? false;
                return _isChecked;
            }
            set
            {
                if (_suppressCascade) return;

                if (!IsFolder)
                {
                    // Leaf file node — update the FileItem
                    if (FileItem != null)
                        FileItem.IsSelected = value == true;
                    OnPropertyChanged();
                    // Bubble up to parent folder
                    Parent?.UpdateCheckedFromChildren();
                    return;
                }

                // Folder node — cascade to all children (only for true/false, not null)
                if (value == null) value = false; // treat indeterminate click as uncheck-all
                _isChecked = value;
                OnPropertyChanged();

                _suppressCascade = true;
                SetChildrenChecked(this, value == true);
                _suppressCascade = false;

                // Bubble up to parent
                Parent?.UpdateCheckedFromChildren();
            }
        }

        /// <summary>Recalculates folder checked state from children. Called when a child changes.</summary>
        internal void UpdateCheckedFromChildren()
        {
            if (!IsFolder || _suppressCascade) return;

            var leaves = GetAllLeaves(this);
            if (!leaves.Any())
            {
                _isChecked = false;
            }
            else
            {
                bool allChecked = leaves.All(l => l.FileItem?.IsSelected == true);
                bool noneChecked = leaves.All(l => l.FileItem?.IsSelected != true);
                _isChecked = allChecked ? true : noneChecked ? false : null;
            }
            OnPropertyChanged(nameof(IsChecked));

            // Continue bubbling up
            Parent?.UpdateCheckedFromChildren();
        }

        // ── Helpers ───────────────────────────────────────────────

        private static void SetChildrenChecked(SolutionTreeNode node, bool isChecked)
        {
            foreach (var child in node.Children)
            {
                if (child.IsFolder)
                {
                    child._isChecked = isChecked;
                    child.OnPropertyChanged(nameof(IsChecked));
                    SetChildrenChecked(child, isChecked);
                }
                else
                {
                    if (child.FileItem != null)
                        child.FileItem.IsSelected = isChecked;
                    child.OnPropertyChanged(nameof(IsChecked));
                }
            }
        }

        private static IEnumerable<SolutionTreeNode> GetAllLeaves(SolutionTreeNode node)
        {
            if (!node.IsFolder)
            {
                yield return node;
                yield break;
            }
            foreach (var child in node.Children)
                foreach (var leaf in GetAllLeaves(child))
                    yield return leaf;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>Builds a folder tree from a flat list of solution files. Folders appear first (sorted alphabetically), then files.</summary>
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
                        folder = new SolutionTreeNode { DisplayName = segment, IsFolder = true, Parent = parentFolder };
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
                    FileItem = file,
                    Parent = parentFolder
                };
                if (parentFolder != null)
                    parentFolder.Children.Add(fileNode);
                else
                    root.Add(fileNode);
            }

            // Sort every level: folders first (alphabetical), then files (alphabetical)
            SortChildrenRecursive(root);

            // Initialize folder checked states from children
            foreach (var node in root)
                if (node.IsFolder) node.UpdateCheckedFromChildren();

            return root;
        }

        private static void SortChildrenRecursive(IList<SolutionTreeNode> nodes)
        {
            if (nodes.Count <= 1) return;

            var sorted = nodes
                .OrderByDescending(n => n.IsFolder)        // folders first
                .ThenBy(n => n.DisplayName, System.StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Replace in-place
            for (int i = 0; i < sorted.Count; i++)
                nodes[i] = sorted[i];

            // Recurse into folder children
            foreach (var node in nodes)
            {
                if (node.IsFolder && node.Children.Count > 0)
                {
                    var childSorted = node.Children
                        .OrderByDescending(n => n.IsFolder)
                        .ThenBy(n => n.DisplayName, System.StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    node.Children.Clear();
                    foreach (var child in childSorted)
                        node.Children.Add(child);

                    SortChildrenRecursive(node.Children);
                }
            }
        }
    }
}
