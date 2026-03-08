using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
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

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            LoadRoleFixes();
            LoadReportRows();
            UpdateCounts();
        }

        private void LoadReportRows()
        {
            _reportRows.Clear();
            // Report is filled when user clicks Run (validation results)
            foreach (var row in RoleFixRegistry.GetReportRows())
                _reportRows.Add(row);
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            TryLoadVodafoneLogo();
        }

        private void TryLoadVodafoneLogo()
        {
            try
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var paths = new[]
                {
                    Path.Combine(baseDir, "View", "Assets", "vodafone-logo.png"),
                    Path.Combine(baseDir, "Assets", "vodafone-logo.png"),
                    Path.Combine(baseDir, "vodafone-logo.png")
                };
                foreach (var path in paths)
                {
                    if (File.Exists(path))
                    {
                        HeaderLogo.Source = new BitmapImage(new Uri(path, UriKind.Absolute));
                        HeaderLogo.Visibility = Visibility.Visible;
                        HeaderVodafoneText.Visibility = Visibility.Collapsed;
                        break;
                    }
                }
            }
            catch
            {
                // Keep "Vodafone" text visible
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

        private void BrowseSolution_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select UiPath solution/project folder",
                UseDescriptionForTitle = true
            };
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                SolutionPathBox.Text = dialog.SelectedPath;
        }

        private void LoadSolution_Click(object sender, RoutedEventArgs e)
        {
            var path = SolutionPathBox.Text?.Trim() ?? "";
            _solutionFiles.Clear();
            _fileTreeRoots.Clear();

            var items = SolutionLoader.LoadWorkflowFiles(path);
            foreach (var item in items)
            {
                item.PropertyChanged += OnFileSelectionChanged;
                _solutionFiles.Add(item);
            }

            foreach (var node in SolutionTreeBuilder.BuildTree(_solutionFiles))
                _fileTreeRoots.Add(node);

            UpdateFilesSelectionSummary();
            UpdateSelectedRulesSummary();
            if (items.Count == 0 && !string.IsNullOrEmpty(path))
                System.Windows.MessageBox.Show("No .xaml workflow files found in the selected folder.", "Load solution", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OnFileSelectionChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SolutionFileItem.IsSelected))
                UpdateFilesSelectionSummary();
        }

        private void SelectAllFiles_Click(object sender, RoutedEventArgs e)
        {
            foreach (var f in _solutionFiles)
                f.IsSelected = true;
            UpdateFilesSelectionSummary();
        }

        private void ClearFilesSelection_Click(object sender, RoutedEventArgs e)
        {
            foreach (var f in _solutionFiles)
                f.IsSelected = false;
            UpdateFilesSelectionSummary();
        }

        private void UpdateFilesSelectionSummary()
        {
            var total = _solutionFiles.Count;
            var selected = _solutionFiles.Count(f => f.IsSelected);
            if (total == 0)
                SideMenuFilesHeader.Text = "0 files loaded (check files to apply rules)";
            else if (selected == 0)
                SideMenuFilesHeader.Text = $"{total} file(s) loaded (check files to apply)";
            else
                SideMenuFilesHeader.Text = $"{selected} of {total} file(s) selected to apply rules";
        }

        /// <summary>Files with checkbox checked (for Run rules / Apply fix). If none checked, returns all.</summary>
        private List<SolutionFileItem> GetSelectedFiles()
        {
            var list = _solutionFiles.Where(f => f.IsSelected).ToList();
            return list.Count > 0 ? list : _solutionFiles.ToList();
        }

        private void CategoryTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateSelectedRulesSummary();
        }

        private void SelectAllRules_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.FrameworkElement fe || fe.Tag is not string tag)
                return;
            var collection = tag == "NoInteraction" ? _autoFixItems : _userInputItems;
            foreach (var item in collection)
                item.IsSelected = true;
            UpdateSelectedRulesSummary();
        }

        private void ClearRulesSelection_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.FrameworkElement fe || fe.Tag is not string tag)
                return;
            var collection = tag == "NoInteraction" ? _autoFixItems : _userInputItems;
            foreach (var item in collection)
                item.IsSelected = false;
            UpdateSelectedRulesSummary();
        }

        private void RuleCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            UpdateSelectedRulesSummary();
        }

        private void UpdateSelectedRulesSummary()
        {
            var selected = GetSelectedRulesFromActiveTab();
            var count = selected.Count;
            bool hasFiles = _solutionFiles.Count > 0;
            RunRulesButtonNoInt.IsEnabled = RunRulesButtonNeedInt.IsEnabled = RunRulesButtonReport.IsEnabled = hasFiles;
            ApplyFixButtonNoInt.IsEnabled = ApplyFixButtonNeedInt.IsEnabled = hasFiles && count > 0;
            if (count == 0)
            {
                SelectedRuleId.Text = hasFiles
                    ? "Select rules (or leave unselected to run all rules on selected files); select at least one rule to Apply fix"
                    : "Load solution first, then select rules";
                SelectedDescription.Text = "";
                return;
            }
            SelectedRuleId.Text = count == 1
                ? $"{selected[0].RuleId} – {selected[0].DisplayName}"
                : $"{count} rule(s) selected";
            SelectedDescription.Text = count == 1 ? selected[0].Description : "Run rules to validate; Apply fix to fix selected files.";
        }

        private List<RoleFixItem> GetSelectedRulesFromActiveTab()
        {
            var collection = CategoryTabs.SelectedItem is TabItem tab && tab == TabNeedInteraction ? _userInputItems : _autoFixItems;
            return collection.Where(r => r.IsSelected).ToList();
        }

        private void RunRules_Click(object sender, RoutedEventArgs e)
        {
            var selectedFiles = GetSelectedFiles();

            if (selectedFiles.Count == 0)
            {
                System.Windows.MessageBox.Show("Load a solution first (side menu), then run. With no files selected, all loaded files are checked.", "Run rules", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var selectedRules = GetSelectedRulesFromActiveTab();
                var rulesToRun = selectedRules.Count > 0
                    ? RoleFixRegistry.GetRulesByIds(selectedRules.Select(r => r.RuleId))
                    : StandardRules.GetAll();
                var filePaths = selectedFiles.Select(f => f.FullPath).ToList();

                var results = RuleRunner.Run(filePaths, rulesToRun);

                _reportRows.Clear();
                foreach (var r in results)
                    _reportRows.Add(new AnalyzerReportRow
                    {
                        RuleId = r.RuleId,
                        RuleName = r.RuleName,
                        Level = r.Level.ToString(),
                        Category = r.Level == RuleLevel.Error ? "Error" : "Warning",
                        ResultMessage = r.Message,
                        HasErrors = r.Level == RuleLevel.Error,
                        FilePath = r.FilePath,
                        Recommendation = r.Recommendation ?? "",
                        Source = "Standard rules"
                    });

                CategoryTabs.SelectedItem = TabAnalyzerReport;
                var errCount = results.Count(x => x.Level == RuleLevel.Error);
                var warnCount = results.Count(x => x.Level == RuleLevel.Warning);
                System.Windows.MessageBox.Show(
                    $"Checked {filePaths.Count} file(s) with {rulesToRun.Count} rule(s). Found {errCount} error(s), {warnCount} warning(s). See Analyzer Report tab.",
                    "Run rules", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Run failed: {ex.Message}", "Run rules", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyFix_Click(object sender, RoutedEventArgs e)
        {
            var selectedFiles = GetSelectedFiles();
            var selectedRules = GetSelectedRulesFromActiveTab();

            if (selectedFiles.Count == 0)
            {
                System.Windows.MessageBox.Show("Check one or more files in the side menu to apply fix.", "Apply fix", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (selectedRules.Count == 0)
            {
                System.Windows.MessageBox.Show("Select one or more rules in the tab above (No interaction = auto-fix, Need interaction = fix after you confirm).", "Apply fix", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var rules = RoleFixRegistry.GetRulesByIds(selectedRules.Select(r => r.RuleId));
                var filePaths = selectedFiles.Select(f => f.FullPath).ToList();
                var isNeedInteractionTab = CategoryTabs.SelectedItem is TabItem tab && tab == TabNeedInteraction;

                Func<IAnalyzerRule, string, bool>? userConfirm = null;
                if (isNeedInteractionTab)
                    userConfirm = (rule, path) =>
                        System.Windows.MessageBox.Show(
                            $"Apply fix for rule {rule.RuleId} – {rule.RuleName} on file:\n{path}\n\nProceed?",
                            "Confirm fix", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

                var fixResults = FixRunner.ApplyFix(filePaths, rules, userConfirmed: userConfirm);
                var applied = fixResults.Count(r => r.Applied);
                var withMessage = fixResults.Where(r => !r.Applied && !string.IsNullOrEmpty(r.Message)).ToList();
                var summary = applied > 0
                    ? $"Fix applied to {applied} file(s)/rule(s)."
                    : "No fix was applied (no findings or user cancelled).";
                if (withMessage.Count > 0)
                    summary += "\n\n" + string.Join("\n", withMessage.Take(5).Select(r => r.RuleId + " " + r.FilePath + ": " + r.Message));
                System.Windows.MessageBox.Show(summary, "Apply fix", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Apply fix failed: {ex.Message}", "Apply fix", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

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

                sheet.Cell(1, 1).Value = "Rule ID";
                sheet.Cell(1, 2).Value = "Rule Name";
                sheet.Cell(1, 3).Value = "Level";
                sheet.Cell(1, 4).Value = "Message";
                sheet.Cell(1, 5).Value = "File";
                sheet.Cell(1, 6).Value = "Recommendation";
                sheet.Range(1, 1, 1, 6).Style.Font.Bold = true;

                var row = 2;
                foreach (var r in _reportRows)
                {
                    sheet.Cell(row, 1).Value = r.RuleId;
                    sheet.Cell(row, 2).Value = r.RuleName;
                    sheet.Cell(row, 3).Value = r.Level;
                    sheet.Cell(row, 4).Value = r.ResultMessage;
                    sheet.Cell(row, 5).Value = r.FilePath;
                    sheet.Cell(row, 6).Value = r.Recommendation;
                    row++;
                }

                sheet.Columns().AdjustToContents();
                workbook.SaveAs(dialog.FileName);

                System.Windows.MessageBox.Show($"Report exported to:\n{dialog.FileName}", "Export to Excel", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Export failed: {ex.Message}", "Export to Excel", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
