using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AnalyzerHelper.Interfaces;
using AnalyzerHelper.Models;
using AnalyzerHelper.Rules;
using AnalyzerHelper.Services;
using ClosedXML.Excel;
using Microsoft.Win32;

namespace AnalyzerHelper.View
{
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<RoleFixItem> _autoFixItems = new();
        private readonly ObservableCollection<RoleFixItem> _userInputItems = new();
        private readonly ObservableCollection<SolutionFileItem> _solutionFiles = new();
        private readonly ObservableCollection<SolutionTreeNode> _fileTreeRoots = new();
        private readonly ObservableCollection<AnalyzerReportRow> _reportRows = new();

        public ObservableCollection<RoleFixItem> AutoFixItems => _autoFixItems;
        public ObservableCollection<RoleFixItem> UserInputItems => _userInputItems;
        public ObservableCollection<SolutionFileItem> SolutionFiles => _solutionFiles;
        public ObservableCollection<SolutionTreeNode> FileTreeRoots => _fileTreeRoots;
        public ObservableCollection<AnalyzerReportRow> ReportRows => _reportRows;

        // ── Filtered view for report ──────────────────────────────────────────
        private ICollectionView? _reportView;

        // ── FileSystemWatcher for auto-refresh ────────────────────────────────
        private FileSystemWatcher? _solutionWatcher;
        private readonly DispatcherTimer _refreshDebounce;
        private string _loadedSolutionPath = "";
        private bool _isUpdatingFilters = false;
        private const string DefaultTitle = "No Process Selected";

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            _refreshDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _refreshDebounce.Tick += RefreshDebounce_Tick;

            LoadRoleFixes();
            LoadReportRows();
            UpdateCounts();
            UpdateStatusBar();
            UpdateSelectedRulesSummary();

            // Setup collection view for filtering
            _reportView = CollectionViewSource.GetDefaultView(_reportRows);
            _reportView.Filter = ReportRowFilter;
            _reportView.SortDescriptions.Add(new SortDescription(nameof(AnalyzerReportRow.RowNumber), ListSortDirection.Ascending));
        }

        // =====================================================================
        //  Lifecycle
        // =====================================================================

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            TryLoadVodafoneLogo();
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            StopSolutionWatcher();
        }

        // =====================================================================
        //  Logo handling
        // =====================================================================

        private void TryLoadVodafoneLogo()
        {
            bool logoLoaded = false;
            try
            {
                var packUri = new Uri("pack://application:,,,/View/Assets/vodafone-logo.png", UriKind.Absolute);
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = packUri;
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                if (bmp.PixelWidth > 0) { HeaderLogo.Source = bmp; logoLoaded = true; }
            }
            catch { }

            if (!logoLoaded)
            {
                try
                {
                    var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    foreach (var path in new[]
                    {
                        Path.Combine(baseDir, "View", "Assets", "vodafone-logo.png"),
                        Path.Combine(baseDir, "Assets", "vodafone-logo.png"),
                        Path.Combine(baseDir, "vodafone-logo.png")
                    })
                    {
                        if (File.Exists(path))
                        {
                            HeaderLogo.Source = new BitmapImage(new Uri(path, UriKind.Absolute));
                            logoLoaded = true;
                            break;
                        }
                    }
                }
                catch { }
            }

            if (!logoLoaded) HeaderLogo.Visibility = Visibility.Collapsed;
            HeaderVodafoneText.Visibility = Visibility.Visible;
        }

        private void HeaderLogo_ImageFailed(object sender, ExceptionRoutedEventArgs e)
        {
            HeaderLogo.Visibility = Visibility.Collapsed;
        }

        // =====================================================================
        //  Rules loading
        // =====================================================================

        private void LoadReportRows()
        {
            _reportRows.Clear();
            foreach (var row in RoleFixRegistry.GetReportRows())
            {
                row.PropertyChanged += OnReportRowPropertyChanged;
                _reportRows.Add(row);
            }
        }

        private void LoadRoleFixes()
        {
            _autoFixItems.Clear();
            _userInputItems.Clear();

            foreach (var item in RoleFixRegistry.GetNoInteractionRoles())
                _autoFixItems.Add(item);

            foreach (var item in RoleFixRegistry.GetNeedInteractionRoles())
                _userInputItems.Add(item);

            UpdateCounts();
        }

        private void UpdateCounts()
        {
            NoInteractionCount.Text = $" ({_autoFixItems.Count})";
            NeedInteractionCount.Text = $" ({_userInputItems.Count})";
        }

        // =====================================================================
        //  Browse + auto-load + Refresh
        // =====================================================================

        private void BrowseSolution_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select UiPath Solution / Project Folder",
                UseDescriptionForTitle = true
            };
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                SolutionPathBox.Text = dialog.SelectedPath;
                LoadSolutionFromPath(dialog.SelectedPath);
            }
        }

        private void LoadSolution_Click(object sender, RoutedEventArgs e)
        {
            var path = SolutionPathBox.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(path))
            {
                System.Windows.MessageBox.Show("Please select a project main folder first.",
                    "Refresh Files", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            LoadSolutionFromPath(path);
        }

        private void LoadSolutionFromPath(string path)
        {
            // Capture currently UNselected files to preserve their unselected state
            var previouslyUnselected = _solutionFiles
                .Where(f => !f.IsSelected)
                .Select(f => f.FullPath)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            bool isFirstLoad = _solutionFiles.Count == 0;

            _solutionFiles.Clear();
            _fileTreeRoots.Clear();

            var items = SolutionLoader.LoadWorkflowFiles(path);
            foreach (var item in items)
            {
                // If it's a refresh and we know this file was explicitly unchecked, keep it unchecked.
                // New files or files that were checked will default to IsSelected = true.
                if (!isFirstLoad && previouslyUnselected.Contains(item.FullPath))
                    item.IsSelected = false;

                item.PropertyChanged += OnFileSelectionChanged;
                _solutionFiles.Add(item);
            }

            foreach (var node in SolutionTreeBuilder.BuildTree(_solutionFiles))
                _fileTreeRoots.Add(node);

            _loadedSolutionPath = path;
            UpdateFilesSelectionSummary();
            UpdateSelectedRulesSummary();
            UpdateEmptyState();
            TryUpdateHeaderFromProjectJson(path);
            StartSolutionWatcher(path);

            if (items.Count == 0 && !string.IsNullOrEmpty(path) && Directory.Exists(path))
                System.Windows.MessageBox.Show("No .xaml workflow files found.\nPlease select the project main folder.",
                    "Load Solution", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void TryUpdateHeaderFromProjectJson(string solutionPath)
        {
            try
            {
                var projectJsonPath = Path.Combine(solutionPath, "project.json");
                if (!File.Exists(projectJsonPath)) { HeaderTitle.Text = DefaultTitle; return; }

                var json = File.ReadAllText(projectJsonPath);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("name", out var nameProp))
                {
                    var projectName = nameProp.GetString();
                    HeaderTitle.Text = string.IsNullOrWhiteSpace(projectName) ? DefaultTitle : projectName;
                    UnloadProjectButton.Visibility = Visibility.Visible;
                }
                else 
                {
                    HeaderTitle.Text = DefaultTitle;
                    UnloadProjectButton.Visibility = Visibility.Collapsed;
                }
            }
            catch 
            { 
                HeaderTitle.Text = DefaultTitle; 
                UnloadProjectButton.Visibility = Visibility.Collapsed;
            }
        }

        // =====================================================================
        //  Unload Project
        // =====================================================================

        private void UnloadProject_Click(object sender, RoutedEventArgs e)
        {
            _loadedSolutionPath = "";
            SolutionPathBox.Text = "";
            HeaderTitle.Text = DefaultTitle;
            _solutionFiles.Clear();
            _fileTreeRoots.Clear();
            _reportRows.Clear();
            foreach (var item in _autoFixItems) item.IsSelected = false;
            foreach (var item in _userInputItems) item.IsSelected = false;
            
            StopSolutionWatcher();
            UpdateFilesSelectionSummary();
            UpdateSelectedRulesSummary();
            UpdateEmptyState();
            UpdateStatusBar();
            UpdateReportCounters();
            
            UnloadProjectButton.Visibility = Visibility.Collapsed;
        }

        // =====================================================================
        //  FileSystemWatcher
        // =====================================================================

        private void StartSolutionWatcher(string path)
        {
            StopSolutionWatcher();
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return;
            try
            {
                _solutionWatcher = new FileSystemWatcher(path)
                {
                    Filter = "*.xaml",
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite,
                    EnableRaisingEvents = true
                };
                _solutionWatcher.Created += OnSolutionFileChanged;
                _solutionWatcher.Deleted += OnSolutionFileChanged;
                _solutionWatcher.Renamed += OnSolutionFileRenamed;
                _solutionWatcher.Changed += OnSolutionFileChanged;
            }
            catch { }
        }

        private void StopSolutionWatcher()
        {
            if (_solutionWatcher != null)
            {
                _solutionWatcher.EnableRaisingEvents = false;
                _solutionWatcher.Created -= OnSolutionFileChanged;
                _solutionWatcher.Deleted -= OnSolutionFileChanged;
                _solutionWatcher.Renamed -= OnSolutionFileRenamed;
                _solutionWatcher.Changed -= OnSolutionFileChanged;
                _solutionWatcher.Dispose();
                _solutionWatcher = null;
            }
        }

        private void OnSolutionFileChanged(object sender, FileSystemEventArgs e) =>
            Dispatcher.Invoke(() => { _refreshDebounce.Stop(); _refreshDebounce.Start(); });

        private void OnSolutionFileRenamed(object sender, RenamedEventArgs e) =>
            Dispatcher.Invoke(() => { _refreshDebounce.Stop(); _refreshDebounce.Start(); });

        private void RefreshDebounce_Tick(object? sender, EventArgs e)
        {
            _refreshDebounce.Stop();
            if (string.IsNullOrWhiteSpace(_loadedSolutionPath) || !Directory.Exists(_loadedSolutionPath)) return;

            var previouslyUnselected = _solutionFiles.Where(f => !f.IsSelected).Select(f => f.FullPath)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (_solutionWatcher != null) _solutionWatcher.EnableRaisingEvents = false;

            _solutionFiles.Clear();
            _fileTreeRoots.Clear();

            var items = SolutionLoader.LoadWorkflowFiles(_loadedSolutionPath);
            foreach (var item in items)
            {
                if (previouslyUnselected.Contains(item.FullPath))
                    item.IsSelected = false;
                item.PropertyChanged += OnFileSelectionChanged;
                _solutionFiles.Add(item);
            }

            foreach (var node in SolutionTreeBuilder.BuildTree(_solutionFiles))
                _fileTreeRoots.Add(node);

            UpdateFilesSelectionSummary();
            if (_solutionWatcher != null) _solutionWatcher.EnableRaisingEvents = true;
        }

        // =====================================================================
        //  File selection
        // =====================================================================

        private void UpdateFilterDropdowns()
        {
            if (_reportRows == null || _reportRows.Count == 0 || _isUpdatingFilters) return;

            _isUpdatingFilters = true;
            try
            {
                // To implement cascading filters, each dropdown should show values available
                // based on the CURRENT TEXT of the other 3 filters.
                var ruleIdTxt = FilterRuleId?.Text?.Trim() ?? "";
                var ruleNameTxt = FilterRuleName?.Text?.Trim() ?? "";
                var levelTxt = (FilterLevel?.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "All";
                var fileTxt = FilterFilePath?.Text?.Trim() ?? "";

                // 1. Rule ID choices (base on Name, Level, File)
                var idChoices = _reportRows
                    .Where(r => PassFilter(r.RuleName, ruleNameTxt) && 
                                PassFilter(r.Level, levelTxt, true) && 
                                PassFilter(r.FilePath, fileTxt))
                    .Select(r => r.RuleId).Distinct().OrderBy(x => x).ToList();
                
                // 2. Rule Name choices (base on ID, Level, File)
                var nameChoices = _reportRows
                    .Where(r => PassFilter(r.RuleId, ruleIdTxt) && 
                                PassFilter(r.Level, levelTxt, true) && 
                                PassFilter(r.FilePath, fileTxt))
                    .Select(r => r.RuleName).Distinct().OrderBy(x => x).ToList();

                // 3. File choices (base on ID, Name, Level)
                var fileChoices = _reportRows
                    .Where(r => PassFilter(r.RuleId, ruleIdTxt) && 
                                PassFilter(r.RuleName, ruleNameTxt) && 
                                PassFilter(r.Level, levelTxt, true))
                    .Select(r => r.FilePath).Distinct().OrderBy(x => x).ToList();

                // Update ItemSources without losing current text
                UpdateComboItems(FilterRuleId, idChoices);
                UpdateComboItems(FilterRuleName, nameChoices);
                UpdateComboItems(FilterFilePath, fileChoices);
            }
            finally
            {
                _isUpdatingFilters = false;
            }
        }

        private bool PassFilter(string value, string filter, bool isLevel = false)
        {
            if (string.IsNullOrEmpty(filter) || (isLevel && filter == "All")) return true;
            return value.Contains(filter, StringComparison.OrdinalIgnoreCase);
        }

        private void UpdateComboItems(System.Windows.Controls.ComboBox combo, List<string> items)
        {
            if (combo == null) return;
            string current = combo.Text;
            combo.ItemsSource = items;
            
            if (!string.IsNullOrEmpty(current) && items.Contains(current))
            {
                combo.SelectedItem = current;
            }
            else
            {
                combo.Text = current;
            }
        }

        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            // Just update summary on selection change if needed
            UpdateFilesSelectionSummary();
        }

        private void OnFileSelectionChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SolutionFileItem.IsSelected))
                UpdateFilesSelectionSummary();
        }

        private void SelectAllFiles_Click(object sender, RoutedEventArgs e)
        {
            foreach (var f in _solutionFiles) f.IsSelected = true;
            RefreshFolderCheckedStates();
            UpdateFilesSelectionSummary();
        }

        private void ClearFilesSelection_Click(object sender, RoutedEventArgs e)
        {
            foreach (var f in _solutionFiles) f.IsSelected = false;
            RefreshFolderCheckedStates();
            UpdateFilesSelectionSummary();
        }

        private void TreeNodeCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            UpdateFilesSelectionSummary();
        }

        /// <summary>Recalculates all folder IsChecked states from their children.</summary>
        private void RefreshFolderCheckedStates()
        {
            foreach (var root in _fileTreeRoots)
                root.RefreshCheckedStateRecursive();
        }

        private void UpdateFilesSelectionSummary()
        {
            var total = _solutionFiles.Count;
            var selected = _solutionFiles.Count(f => f.IsSelected);
            SideMenuFilesHeader.Text = total == 0
                ? "No Files Loaded"
                : selected == 0 ? $"{total} Loaded" : $"{selected}/{total} Selected";
        }

        private List<SolutionFileItem> GetSelectedFiles()
        {
            var list = _solutionFiles.Where(f => f.IsSelected).ToList();
            return list.Count > 0 ? list : _solutionFiles.ToList();
        }

        // =====================================================================
        //  Rule selection & detail panel (footer)
        // =====================================================================

        private void CategoryTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Guard: fires during InitializeComponent before controls are ready
            if (SelectedRuleId == null) return;
            
            ClearFooterDetails();
            UpdateSelectedRulesSummary();
        }

        /// <summary>Show rule details when clicking a rule row in the ListBox.</summary>
        private void RuleListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is System.Windows.Controls.ListBox lb && lb.SelectedItem is RoleFixItem item)
            {
                SelectedRuleId.Text = $"{item.RuleId} – {item.DisplayName}";
                SelectedDescription.Text = item.Description;
            }
            else
            {
                ClearFooterDetails();
            }
        }

        /// <summary>Toggle the IsSelected checkbox when clicking anywhere on a rule row.</summary>
        private void RuleListBox_RowClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is not System.Windows.Controls.ListBox) return;
            var clickedElement = e.OriginalSource as DependencyObject;
            while (clickedElement != null && clickedElement is not ListBoxItem)
                clickedElement = VisualTreeHelper.GetParent(clickedElement);

            if (clickedElement is ListBoxItem lbi && lbi.DataContext is RoleFixItem item)
            {
                item.IsSelected = !item.IsSelected;
                UpdateSelectedRulesSummary();
                SelectedRuleId.Text = $"{item.RuleId} – {item.DisplayName}";
                SelectedDescription.Text = item.Description;
            }
            else
            {
                // Clicked empty area – reset footer to default
                ClearFooterDetails();
            }
        }

        private void RuleListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is System.Windows.Controls.ListBox lb && lb.SelectedItem is RoleFixItem item)
            {
                SelectedRuleId.Text = $"{item.RuleId} – {item.DisplayName}";
                SelectedDescription.Text = item.Description;
            }
        }

        private void ClearFooterDetails()
        {
            if (SelectedRuleId == null || SelectedDescription == null) return;
            bool hasProject = _solutionFiles.Count > 0;
            var totalSelected = GetTotalSelectedRulesCount();

            if (totalSelected > 0)
            {
                SelectedRuleId.Text = $"{totalSelected} Rule(s) Selected";
                SelectedDescription.Text = "Run to validate or apply fix.";
            }
            else
            {
                SelectedRuleId.Text = hasProject
                    ? "Ready to validate."
                    : "";
                SelectedDescription.Text = hasProject
                    ? "No specific rules selected. All available rules will run."
                    : "";
            }
        }

        private void SelectAllRules_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement fe || fe.Tag is not string tag) return;
            var collection = tag == "NoInteraction" ? _autoFixItems : _userInputItems;
            foreach (var item in collection) item.IsSelected = true;
            UpdateSelectedRulesSummary();
        }

        private void ClearRulesSelection_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement fe || fe.Tag is not string tag) return;
            var collection = tag == "NoInteraction" ? _autoFixItems : _userInputItems;
            foreach (var item in collection) item.IsSelected = false;
            UpdateSelectedRulesSummary();
        }

        private void RuleCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            UpdateSelectedRulesSummary();
            if (sender is System.Windows.Controls.CheckBox cb && cb.DataContext is RoleFixItem item)
            {
                SelectedRuleId.Text = $"{item.RuleId} – {item.DisplayName}";
                SelectedDescription.Text = item.Description;
            }
        }

        /// <summary>Gets selected rules from BOTH tabs combined.</summary>
        private int GetTotalSelectedRulesCount()
        {
            return _autoFixItems.Count(r => r.IsSelected) + _userInputItems.Count(r => r.IsSelected);
        }

        private void UpdateSelectedRulesSummary()
        {
            // Guard: may be called before XAML controls are ready
            if (RunRulesButtonNoInt == null || SelectedRuleId == null) return;

            var allSelected = GetAllSelectedRules();
            var totalSelected = allSelected.Count;
            bool hasFiles = _solutionFiles.Count > 0;

            RunRulesButtonNoInt.IsEnabled = RunRulesButtonNeedInt.IsEnabled = RunRulesButtonReport.IsEnabled = hasFiles;
            ApplyFixButtonNoInt.IsEnabled = ApplyFixButtonNeedInt.IsEnabled = hasFiles && allSelected.Count > 0;

            if (totalSelected == 0)
            {
                if (SelectedDescription.Text == "")
                {
                    SelectedRuleId.Text = hasFiles
                        ? "Ready to validate."
                        : "";
                    SelectedDescription.Text = hasFiles
                        ? "No specific rules selected. All available rules will run."
                        : "";
                }
            }
            else if (totalSelected > 0)
            {
                SelectedRuleId.Text = $"{totalSelected} Rule(s) Selected";
                SelectedDescription.Text = "Only these rules will be validated or fixed.";
            }
        }

        private void UpdateEmptyState()
        {
            EmptyStateText.Visibility = _solutionFiles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private List<RoleFixItem> GetAllSelectedRules()
        {
            var list = new List<RoleFixItem>();
            list.AddRange(_autoFixItems.Where(r => r.IsSelected));
            list.AddRange(_userInputItems.Where(r => r.IsSelected));
            return list;
        }

        // =====================================================================
        //  Status bar
        // =====================================================================

        private void UpdateStatusBar()
        {
            if (StatusErrors == null) return;
            var errCount = _reportRows.Count(r => r.Level == "Error");
            var warnCount = _reportRows.Count(r => r.Level == "Warning");
            var infoCount = _reportRows.Count(r => r.Level == "Info");
            var total = _reportRows.Count;

            StatusErrors.Text = $"{errCount} Error{(errCount != 1 ? "s" : "")}";
            StatusWarnings.Text = $"{warnCount} Warning{(warnCount != 1 ? "s" : "")}";
            StatusInfo.Text = $"{infoCount} Info";
            StatusTotal.Text = total == 0 ? "Ready" : $"{total} Finding{(total != 1 ? "s" : "")} Total";
        }

        // =====================================================================
        //  Report row selection tracking (fix counters)
        // =====================================================================

        private void OnReportRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AnalyzerReportRow.IsSelectedForFix) ||
                e.PropertyName == nameof(AnalyzerReportRow.IsFixed))
            {
                Dispatcher.BeginInvoke(new Action(UpdateReportCounters));
            }
        }

        private void UpdateReportCounters()
        {
            var selectedCount = _reportRows.Count(r => r.IsSelectedForFix);
            var fixedCount = _reportRows.Count(r => r.IsFixed);
            var unfixedCount = _reportRows.Count - fixedCount;

            ReportSelectionCounter.Text = $"{selectedCount} Selected for Fix";
            ReportFixedCounter.Text = $"{fixedCount} Fixed";
            ReportUnfixedCounter.Text = $"{unfixedCount} Unfixed";
        }

        private void SelectAllReportRows_Click(object sender, RoutedEventArgs e)
        {
            if (_reportView != null)
                foreach (AnalyzerReportRow row in _reportView) row.IsSelectedForFix = true;
        }

        private void ClearReportSelection_Click(object sender, RoutedEventArgs e)
        {
            if (_reportView != null)
                foreach (AnalyzerReportRow row in _reportView) row.IsSelectedForFix = false;
        }

        // =====================================================================
        //  Report column filters
        // =====================================================================

        private bool ReportRowFilter(object obj)
        {
            if (obj is not AnalyzerReportRow row) return false;

            var ruleIdFilter = FilterRuleId?.Text?.Trim() ?? "";
            var ruleNameFilter = FilterRuleName?.Text?.Trim() ?? "";
            var filePathFilter = FilterFilePath?.Text?.Trim() ?? "";
            var levelFilter = (FilterLevel?.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "All";

            if (!string.IsNullOrEmpty(ruleIdFilter) &&
                !row.RuleId.Contains(ruleIdFilter, StringComparison.OrdinalIgnoreCase))
                return false;

            if (!string.IsNullOrEmpty(ruleNameFilter) &&
                !row.RuleName.Contains(ruleNameFilter, StringComparison.OrdinalIgnoreCase))
                return false;

            if (!string.IsNullOrEmpty(filePathFilter) &&
                !row.FilePath.Contains(filePathFilter, StringComparison.OrdinalIgnoreCase))
                return false;

            if (levelFilter != "All" && !row.Level.Equals(levelFilter, StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }

        private void ReportFilter_Changed(object sender, SelectionChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(RefreshReportWithIndices), DispatcherPriority.Input);
        }

        private void ReportFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(RefreshReportWithIndices), DispatcherPriority.Input);
        }

        private void FilterLevel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(RefreshReportWithIndices), DispatcherPriority.Input);
        }

        private void ResetFilters_Click(object sender, RoutedEventArgs e)
        {
            _isUpdatingFilters = true;

            FilterRuleId.Text = string.Empty;
            FilterRuleId.SelectedIndex = -1;
            
            FilterRuleName.Text = string.Empty;
            FilterRuleName.SelectedIndex = -1;
            
            FilterFilePath.Text = string.Empty;
            FilterFilePath.SelectedIndex = -1;
            
            FilterLevel.SelectedIndex = 0; // "All"

            _isUpdatingFilters = false;
            RefreshReportWithIndices();
        }

        private void RefreshReportWithIndices()
        {
            if (_reportView == null || _isUpdatingFilters) return;
            
            _reportView.Refresh();
            UpdateVisibleIndices();
            UpdateFilterDropdowns();
        }

        private void UpdateVisibleIndices()
        {
            if (_reportView == null) return;
            int counter = 1;
            foreach (var item in _reportView)
            {
                if (item is AnalyzerReportRow row)
                {
                    row.RowNumber = counter++;
                }
            }
        }

        // =====================================================================
        //  Column visibility toggle
        // =====================================================================

        private void ToggleColumns_Click(object sender, RoutedEventArgs e)
        {
            var menu = new ContextMenu();
            var columns = new (DataGridColumn col, string name)[]
            {
                (ColRowNumber, "#"),
                (ColRuleId, "Rule ID"),
                (ColRuleName, "Rule Name"),
                (ColLevel, "Level"),
                (ColMessage, "Message"),
                (ColFilePath, "File"),
                (ColRecommendation, "Recommendation"),
            };

            foreach (var (col, name) in columns)
            {
                var item = new MenuItem
                {
                    Header = name,
                    IsCheckable = true,
                    IsChecked = col.Visibility == Visibility.Visible
                };
                var capturedCol = col;
                item.Click += (s, _) =>
                {
                    if (s is MenuItem mi)
                        capturedCol.Visibility = mi.IsChecked ? Visibility.Visible : Visibility.Collapsed;
                };
                menu.Items.Add(item);
            }

            if (sender is System.Windows.Controls.Button btn)
            {
                menu.PlacementTarget = btn;
                menu.Placement = PlacementMode.Bottom;
                menu.IsOpen = true;
            }
        }

        // =====================================================================
        //  Fix selected report rows
        // =====================================================================

        private void FixSelectedReport_Click(object sender, RoutedEventArgs e)
        {
            var selectedRows = _reportRows.Where(r => r.IsSelectedForFix && !r.IsFixed).ToList();
            if (selectedRows.Count == 0)
            {
                System.Windows.MessageBox.Show("Please select report rows to fix first.",
                    "Fix Selected", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            ActionProgressBar.Visibility = Visibility.Visible;
            ForceUIRefresh();
            try
            {
                var ruleIds = selectedRows.Select(r => r.RuleId).Distinct();
                var rules = RoleFixRegistry.GetRulesByIds(ruleIds);
                var filePaths = selectedRows.Select(r => r.OriginalFilePath)
                    .Where(p => !string.IsNullOrEmpty(p)).Distinct().ToList();

                if (filePaths.Count == 0 || rules.Count == 0)
                {
                    System.Windows.MessageBox.Show("No valid files or rules found for the selected rows.",
                        "Fix Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var fixResults = FixRunner.ApplyFix(filePaths, rules);
                var appliedResults = fixResults.Where(r => r.Applied).ToList();
                var appliedCount = appliedResults.Count;
                var withMessage = fixResults.Where(r => !r.Applied && !string.IsNullOrEmpty(r.Message)).ToList();

                var rulesApplied = appliedResults.Select(r => r.RuleId).Distinct().Count();
                var filesChanged = appliedResults.Select(r => r.FilePath).Distinct().Count();

                string summary = appliedCount > 0
                    ? $"Success: Applied {rulesApplied} rule(s) to fix {appliedCount} finding(s) across {filesChanged} file(s)."
                    : "No changes needed.";

                if (appliedCount > 0)
                {
                    var ruleGroups = appliedResults.GroupBy(r => r.RuleId);
                    var appliedDetails = string.Join("\n", ruleGroups.Select(g => $"• {g.Key}: fixed in {g.Select(r => r.FilePath).Distinct().Count()} file(s)"));
                    summary += "\n\nFixed Rules:\n" + appliedDetails;

                    var appliedRuleFiles = appliedResults.Select(r => (r.RuleId, r.FilePath)).ToHashSet();

                    foreach (var row in selectedRows)
                    {
                        if (appliedRuleFiles.Contains((row.RuleId, row.OriginalFilePath)))
                        {
                            row.IsFixed = true;
                            row.ResultMessage = "Fixed";
                            row.IsSelectedForFix = false;
                        }
                    }
                }

                if (withMessage.Count > 0)
                {
                    summary += "\n\nNotices:\n" + string.Join("\n", withMessage.Take(5).Select(r => $"• {r.RuleId} -> {System.IO.Path.GetFileName(r.FilePath)}: {r.Message}"));
                }

                UpdateReportCounters();
                UpdateStatusBar();

                if (appliedCount > 0)
                {
                    ShowScrollableSummaryDialog("Fix Selected Results", summary);
                }
                else
                {
                    System.Windows.MessageBox.Show(summary, "Fix Selected", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (OperationCanceledException)
            {
                System.Windows.MessageBox.Show("The fix operation was cancelled.", "Fix Selected",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Fix failed: {ex.Message}", "Fix Selected",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ActionProgressBar.Visibility = Visibility.Collapsed;
            }
        }

        // =====================================================================
        //  Run rules
        // =====================================================================

        private void RunRules_Click(object sender, RoutedEventArgs e)
        {
            var selectedFiles = GetSelectedFiles();
            if (selectedFiles.Count == 0)
            {
                System.Windows.MessageBox.Show("Please select a project main folder and load files first.",
                    "Run Rules", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            ActionProgressBar.Visibility = Visibility.Visible;
            ForceUIRefresh();
            try
            {
                var selectedRules = GetAllSelectedRules();
                var rulesToRun = selectedRules.Count > 0
                    ? RoleFixRegistry.GetRulesByIds(selectedRules.Select(r => r.RuleId))
                    : StandardRules.GetAll();
                var filePaths = selectedFiles.Select(f => f.FullPath).ToList();
                var results = RuleRunner.Run(filePaths, rulesToRun);

                _reportRows.Clear();
                int rowNum = 1;
                foreach (var r in results)
                {
                    var pathStr = r.FilePath;
                    if (!string.IsNullOrEmpty(SolutionPathBox.Text) && pathStr.StartsWith(SolutionPathBox.Text, StringComparison.OrdinalIgnoreCase))
                    {
                        pathStr = pathStr.Substring(SolutionPathBox.Text.Length).TrimStart('\\', '/');
                    }

                    var row = new AnalyzerReportRow
                    {
                        RowNumber = rowNum++,
                        RuleId = r.RuleId,
                        RuleName = RoleFixRegistry.HumanizeRuleName(r.RuleName),
                        Level = r.Level.ToString(),
                        Category = r.Level == RuleLevel.Error ? "Error" : "Warning",
                        ResultMessage = r.Message,
                        HasErrors = r.Level == RuleLevel.Error,
                        FilePath = pathStr,
                        Recommendation = r.Recommendation ?? "",
                        Source = "Standard rules"
                    };
                    row.OriginalFilePath = r.FilePath; // Store absolute just in case
                    row.PropertyChanged += OnReportRowPropertyChanged;
                    _reportRows.Add(row);
                }

                // Refresh filter view
                _reportView = CollectionViewSource.GetDefaultView(_reportRows);
                _reportView.Filter = ReportRowFilter;
                _reportView.SortDescriptions.Add(new SortDescription(nameof(AnalyzerReportRow.RowNumber), ListSortDirection.Ascending));

                UpdateFilterDropdowns();
                UpdateVisibleIndices();

                CategoryTabs.SelectedItem = TabAnalyzerReport;
                UpdateStatusBar();
                UpdateReportCounters();

                var errCount = results.Count(x => x.Level == RuleLevel.Error);
                var warnCount = results.Count(x => x.Level == RuleLevel.Warning);
                System.Windows.MessageBox.Show(
                    $"Checked {filePaths.Count} file(s) · {rulesToRun.Count} rule(s)\n" +
                    $"Found {errCount} error(s), {warnCount} warning(s)",
                    "Validation", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Run failed: {ex.Message}", "Run Rules",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ActionProgressBar.Visibility = Visibility.Collapsed;
            }
        }

        // =====================================================================
        //  Apply fix
        // =====================================================================

        private void ApplyFix_Click(object sender, RoutedEventArgs e)
        {
            var selectedFiles = GetSelectedFiles();
            var selectedRules = GetAllSelectedRules();

            if (selectedFiles.Count == 0)
            {
                System.Windows.MessageBox.Show("Please check files in the side panel.", "Apply Fix",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (selectedRules.Count == 0)
            {
                System.Windows.MessageBox.Show("Please select one or more rules first.", "Apply Fix",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            ActionProgressBar.Visibility = Visibility.Visible;
            ForceUIRefresh();
            try
            {
                var rules = RoleFixRegistry.GetRulesByIds(selectedRules.Select(r => r.RuleId));
                var filePaths = selectedFiles.Select(f => f.FullPath).ToList();
                var fixResults = FixRunner.ApplyFix(filePaths, rules, userConfirmed: null); // run without pre-confirmations

                if (CategoryTabs.SelectedItem is TabItem autoFixTab && autoFixTab == TabNoInteraction)
                {
                    bool anyApplied = fixResults.Any(r => r.Applied);
                    bool anyFailure = fixResults.Any(IsAutofixRunFailure);
                    bool allBenignOrApplied = fixResults.Count > 0 &&
                        fixResults.All(r => r.Applied || IsAutofixNoChangeMessage(r.Message));
                    if (anyApplied && !anyFailure && allBenignOrApplied)
                    {
                        System.Windows.MessageBox.Show(
                            "The selected rule(s) were fixed successfully.",
                            "Apply Fix",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                }

                var appliedResults = fixResults.Where(r => r.Applied).ToList();
                var appliedCount = appliedResults.Count;
                var withMessage = fixResults.Where(r => !r.Applied && !string.IsNullOrEmpty(r.Message)).ToList();
                
                var rulesApplied = appliedResults.Select(r => r.RuleId).Distinct().Count();
                var filesChanged = appliedResults.Select(r => r.FilePath).Distinct().Count();

                string summary = appliedCount > 0
                    ? $"Success: Applied {rulesApplied} rule(s) to fix {appliedCount} finding(s) across {filesChanged} file(s)."
                    : "No changes needed.";

                if (appliedCount > 0)
                {
                    var ruleGroups = appliedResults.GroupBy(r => r.RuleId);
                    var appliedDetails = string.Join("\n", ruleGroups.Select(g => $"• {g.Key}: fixed in {g.Select(r => r.FilePath).Distinct().Count()} file(s)"));
                    summary += "\n\nFixed Rules:\n" + appliedDetails;

                    if (withMessage.Count > 0)
                    {
                        summary += "\n\nNotices:\n" + string.Join("\n", withMessage.Take(5).Select(r => $"• {r.RuleId} -> {System.IO.Path.GetFileName(r.FilePath)}: {r.Message}"));
                    }
                    
                    ShowScrollableSummaryDialog("Apply Fix Result", summary);
                }
                else
                {
                    System.Windows.MessageBox.Show(summary, "Apply Fix", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (OperationCanceledException)
            {
                System.Windows.MessageBox.Show("The fix operation was cancelled.", "Apply Fix",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Apply fix failed: {ex.Message}", "Apply Fix",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ActionProgressBar.Visibility = Visibility.Collapsed;
            }
        }

        // =====================================================================
        //  Export to Excel
        // =====================================================================

        private void ExportToExcel_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Excel files (*.xlsx)|*.xlsx|All files (*.*)|*.*",
                DefaultExt = ".xlsx",
                FileName = "AnalyzerReport.xlsx",
                Title = "Export Analyzer Report to Excel"
            };
            if (dialog.ShowDialog() != true) return;

            try
            {
                using var workbook = new XLWorkbook();
                var sheet = workbook.Worksheets.Add("Analyzer Report");

                sheet.Cell(1, 1).Value = "#";
                sheet.Cell(1, 2).Value = "Rule ID";
                sheet.Cell(1, 3).Value = "Rule Name";
                sheet.Cell(1, 4).Value = "Level";
                sheet.Cell(1, 5).Value = "Message";
                sheet.Cell(1, 6).Value = "File";
                sheet.Cell(1, 7).Value = "Recommendation";
                sheet.Range(1, 1, 1, 7).Style.Font.Bold = true;

                var row = 2;
                foreach (var r in _reportRows)
                {
                    sheet.Cell(row, 1).Value = r.RowNumber;
                    sheet.Cell(row, 2).Value = r.RuleId;
                    sheet.Cell(row, 3).Value = r.RuleName;
                    sheet.Cell(row, 4).Value = r.Level;
                    sheet.Cell(row, 5).Value = r.ResultMessage;
                    sheet.Cell(row, 6).Value = r.FilePath;
                    sheet.Cell(row, 7).Value = r.Recommendation;
                    row++;
                }

                sheet.Columns().AdjustToContents();
                workbook.SaveAs(dialog.FileName);
                System.Windows.MessageBox.Show($"Exported to:\n{dialog.FileName}", "Export",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Export failed: {ex.Message}", "Export",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // =====================================================================
        //  Helpers
        // =====================================================================

        /// <summary>DefineAndFix returned without changing the file (expected for some rule×file pairs).</summary>
        private static bool IsAutofixNoChangeMessage(string? message) =>
            string.Equals(message?.Trim(), "No change (rule did not apply fix).", StringComparison.Ordinal);

        /// <summary>True when a fix attempt did not apply and was not a benign no-op (read errors, exceptions, etc.).</summary>
        private static bool IsAutofixRunFailure(FixRunner.FixResult r) =>
            !r.Applied && !IsAutofixNoChangeMessage(r.Message);

        private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is ScrollViewer sv)
            {
                sv.ScrollToVerticalOffset(sv.VerticalOffset - e.Delta / 3.0);
                e.Handled = true;
            }
        }

        private void ForceUIRefresh()
        {
            Dispatcher.Invoke(DispatcherPriority.Render, new Action(() => { }));
        }

        private void ShowScrollableSummaryDialog(string title, string summary)
        {
            var window = new System.Windows.Window
            {
                Title = title,
                Width = 550,
                Height = 350,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(244, 245, 247))
            };
            var grid = new Grid { Margin = new Thickness(15) };
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var textBox = new System.Windows.Controls.TextBox
            {
                Text = summary,
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 15),
                Background = System.Windows.Media.Brushes.White,
                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(221, 225, 230)),
                Padding = new Thickness(5)
            };
            Grid.SetRow(textBox, 0);
            grid.Children.Add(textBox);

            var okButton = new System.Windows.Controls.Button
            {
                Content = "OK",
                Width = 80,
                Height = 28,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                IsDefault = true,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            okButton.Click += (s, e) => window.Close();
            Grid.SetRow(okButton, 1);
            grid.Children.Add(okButton);

            window.Content = grid;
            window.ShowDialog();
        }
    }
}
