using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AnalyzerHelper.Interfaces;
using AnalyzerHelper.Models;
using ClosedXML.Excel;

namespace AnalyzerHelper.Rules
{
    public class ConfigConstantsUsageRule : IAnalyzerRule
    {
        public string RuleId => "VF-022";
        public string RuleName => "Config Constants Usage";
        public string DefaultRecommendation => "Ensure all Config dictionaries use valid keys mapped to correct sheets, and unused Config values are reviewed.";
        public bool RequiresUserInteraction => false;

        private static string _lastAnalyzedProjectRoot = null;
        private static DateTime _lastAnalyzedTime = DateTime.MinValue;
        private static bool _configNotFoundBoxShown = false;
        
        // Maps categorized sheet type -> hashset of keys found in those sheets
        private static Dictionary<string, HashSet<string>> _configKeysByCategory = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        
        private class XamlKeyUsage
        {
            public string DictName { get; set; }
            public string[] ExpectedCategories { get; set; }
            public string Key { get; set; }
        }
        
        // Maps DictName -> XamlKeyUsage used in XAML
        private static List<XamlKeyUsage> _allXamlKeys = new List<XamlKeyUsage>();
        
        private static List<RuleCheckResult> _projectWideResults = new List<RuleCheckResult>();

        // We use a regex to match: dictName("KeyName") or in_dictName("KeyName") etc.
        // Group 1: Dictionary Name, Group 2: Key
        private static readonly Regex _dictAccessRegex = new Regex(
            @"\b(?:in_|io_|out_)?(intdctroboInt|booldctcaseBool|booldctroboBool|crddctroboCred|dctcaseText|dctmailTemplates|dctroboText|dctselector|intdctcaseInt)\s*\(\s*(?:""|&quot;)(.*?)(?:""|&quot;)\s*\)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public IReadOnlyList<RuleCheckResult> Check(string filePath, string content)
        {
            var results = new List<RuleCheckResult>();
            
            string projectRoot = FindProjectRoot(filePath);
            if (string.IsNullOrEmpty(projectRoot))
                return results;

            // Simple caching to only do the global analysis once per run (within a few seconds)
            if (_lastAnalyzedProjectRoot != projectRoot || (DateTime.Now - _lastAnalyzedTime).TotalSeconds > 5)
            {
                _lastAnalyzedProjectRoot = projectRoot;
                _lastAnalyzedTime = DateTime.Now;
                _configNotFoundBoxShown = false;
                _configKeysByCategory.Clear();
                _allXamlKeys.Clear();
                _projectWideResults.Clear();

                AnalyzeProject(projectRoot);
            }

            // Report logic:
            // 1. Missing from config or wrong dictionary (Errors)
            var localXamlKeys = ExtractKeysFromXaml(content);
            foreach (var item in localXamlKeys)
            {
                if (_configKeysByCategory.Count > 0)
                {
                    bool existsInCorrectCategory = item.ExpectedCategories.Any(cat => _configKeysByCategory.TryGetValue(cat, out var set) && set.Contains(item.Key));
                    
                    if (!existsInCorrectCategory)
                    {
                        // Check if it exists in another category to provide a better error message
                        string foundInOtherCategory = null;
                        foreach(var kvp in _configKeysByCategory)
                        {
                            if (kvp.Value.Contains(item.Key))
                            {
                                foundInOtherCategory = kvp.Key;
                                break;
                            }
                        }

                        string expectedStr = string.Join(" or ", item.ExpectedCategories);

                        if (foundInOtherCategory != null)
                        {
                            results.Add(new RuleCheckResult
                            {
                                RuleId = RuleId,
                                RuleName = RuleName,
                                Level = RuleLevel.Warning,
                                Message = $"Key '{item.Key}' is accessed via '{item.DictName}' which expects sheet-type [{expectedStr}], but it exists in sheets for [{foundInOtherCategory}].",
                                FilePath = filePath,
                                Recommendation = "Use the correct dictionary for this config value or move the value to the correct sheet."
                            });
                        }
                        else
                        {
                            results.Add(new RuleCheckResult
                            {
                                RuleId = RuleId,
                                RuleName = RuleName,
                                Level = RuleLevel.Warning,
                                Message = $"Key '{item.Key}' is used in dictionary '{item.DictName}' but was not found in corresponding Config.xlsx sheets ([{expectedStr}]).",
                                FilePath = filePath,
                                Recommendation = "Add the key to the relevant Config.xlsx sheet or correct the XAML key."
                            });
                        }
                    }
                }
            }

            // 2. Unused config keys
            if (_projectWideResults.Count > 0)
            {
                results.AddRange(_projectWideResults);
                _projectWideResults.Clear();
            }

            return results;
        }

        private void AnalyzeProject(string projectRoot)
        {
            string configPath = Path.Combine(projectRoot, "Data", "Config.xlsx");
            if (!File.Exists(configPath))
                configPath = Path.Combine(projectRoot, "Config", "Config.xlsx");
            
            if (!File.Exists(configPath))
            {
                if (!_configNotFoundBoxShown)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        System.Windows.MessageBox.Show("Config.xlsx not found in Data or Config folder.", "Config Not Found", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    });
                    _configNotFoundBoxShown = true;
                }
                return;
            }

            // 1. Read Config.xlsx keys and categorize by sheet name conventions
            try
            {
                using (var workbook = new XLWorkbook(configPath))
                {
                    foreach (var worksheet in workbook.Worksheets)
                    {
                        var lastRowUsed = worksheet.LastRowUsed();
                        if (lastRowUsed == null) continue;
                        
                        int lastRowNum = lastRowUsed.RowNumber();
                        string sheetName = worksheet.Name;
                        
                        // Handle "Constants" and "Assets" sheets with "Type" column
                        if (sheetName.Equals("Constants", StringComparison.OrdinalIgnoreCase) || 
                            sheetName.Equals("Assets", StringComparison.OrdinalIgnoreCase))
                        {
                            string sheetPrefix = sheetName.Equals("Constants", StringComparison.OrdinalIgnoreCase) ? "Constants" : "Assets";
                            int typeCol = 0;
                            // Find 'Type' column in Row 1
                            for (int c = 1; c <= worksheet.LastColumnUsed()?.ColumnNumber(); c++)
                            {
                                if (worksheet.Cell(1, c).GetString()?.Trim().Equals("Type", StringComparison.OrdinalIgnoreCase) == true)
                                {
                                    typeCol = c;
                                    break;
                                }
                            }

                            for (int i = 2; i <= lastRowNum; i++)
                            {
                                string key = worksheet.Cell(i, 1).GetString()?.Trim();
                                if (string.IsNullOrEmpty(key)) continue;

                                string typeVal = typeCol > 0 ? worksheet.Cell(i, typeCol).GetString()?.Trim().ToLower() : "text"; // Default to text if missing
                                if (typeVal == "casetest") typeVal = "casetext"; // Handle typo case
                                
                                string formattedType = typeVal switch {
                                    "int" => "Int",
                                    "casebool" => "CaseBool",
                                    "bool" => "Bool",
                                    "casetext" => "CaseText",
                                    "text" => "Text",
                                    "caseint" => "CaseInt",
                                    _ => string.IsNullOrEmpty(typeVal) ? "Text" : char.ToUpper(typeVal[0]) + typeVal.Substring(1)
                                };
                                
                                string category = $"{sheetPrefix}.{formattedType}";
                                if (!_configKeysByCategory.ContainsKey(category))
                                    _configKeysByCategory[category] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                                _configKeysByCategory[category].Add(key);
                            }
                        }
                        else
                        {
                            // Other sheets (Assets, Credentials, Mail Templates, Selectors)
                            string category = GetCategoryFromSheetName(sheetName);
                            if (category == null) continue; // Unmapped sheet

                            if (!_configKeysByCategory.ContainsKey(category))
                                _configKeysByCategory[category] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                            for (int i = 2; i <= lastRowNum; i++)
                            {
                                string key = worksheet.Cell(i, 1).GetString()?.Trim();
                                if (!string.IsNullOrEmpty(key))
                                {
                                    _configKeysByCategory[category].Add(key);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
            }

            // 2. Read all XAML files to find all used keys
            try
            {
                var xamlFiles = Directory.GetFiles(projectRoot, "*.xaml", SearchOption.AllDirectories);
                foreach (var xf in xamlFiles)
                {
                    string text = File.ReadAllText(xf);
                    var keys = ExtractKeysFromXaml(text);
                    _allXamlKeys.AddRange(keys);
                }
            }
            catch { }

            // 3. Find unused config keys
            foreach (var kvp in _configKeysByCategory)
            {
                string cat = kvp.Key;
                foreach (var cfgKey in kvp.Value)
                {
                    bool isUsed = _allXamlKeys.Any(x => x.ExpectedCategories.Contains(cat, StringComparer.OrdinalIgnoreCase) && x.Key.Equals(cfgKey, StringComparison.OrdinalIgnoreCase));
                    if (!isUsed)
                    {
                        _projectWideResults.Add(new RuleCheckResult
                        {
                            RuleId = RuleId,
                            RuleName = RuleName,
                            Level = RuleLevel.Info,
                            Message = $"Config key '{cfgKey}' is defined in a '{cat}' sheet but never used in XAML files by a matching dictionary.",
                            FilePath = configPath,
                            Recommendation = "Remove unused keys to clean up Config.xlsx."
                        });
                    }
                }
            }
        }

        // Helpers for Categories
        private string GetCategoryFromSheetName(string sheetName)
        {
            if (sheetName.IndexOf("Mail", StringComparison.OrdinalIgnoreCase) >= 0) return "Mail Templates"; // Capitalized
            if (sheetName.IndexOf("Cred", StringComparison.OrdinalIgnoreCase) >= 0) return "Credentials";
            if (sheetName.IndexOf("Selector", StringComparison.OrdinalIgnoreCase) >= 0) return "Selectors";
            return null; // Ignore unknown sheets implicitly
        }

        private string[] GetExpectedCategoriesFromDictName(string dictName)
        {
            if (dictName.Equals("intdctroboInt", StringComparison.OrdinalIgnoreCase)) return new[] { "Constants.Int", "Assets.Int" };
            if (dictName.Equals("booldctcaseBool", StringComparison.OrdinalIgnoreCase)) return new[] { "Constants.CaseBool", "Assets.CaseBool" };
            if (dictName.Equals("booldctroboBool", StringComparison.OrdinalIgnoreCase)) return new[] { "Constants.Bool", "Assets.Bool" };
            if (dictName.Equals("crddctroboCred", StringComparison.OrdinalIgnoreCase)) return new[] { "Credentials" };
            if (dictName.Equals("dctcaseText", StringComparison.OrdinalIgnoreCase)) return new[] { "Constants.CaseText", "Assets.CaseText" };
            if (dictName.Equals("dctmailTemplates", StringComparison.OrdinalIgnoreCase)) return new[] { "Mail Templates" };
            if (dictName.Equals("dctroboText", StringComparison.OrdinalIgnoreCase)) return new[] { "Constants.Text", "Assets.Text" };
            if (dictName.Equals("dctselector", StringComparison.OrdinalIgnoreCase)) return new[] { "Selectors" };
            if (dictName.Equals("intdctcaseInt", StringComparison.OrdinalIgnoreCase)) return new[] { "Constants.CaseInt", "Assets.CaseInt" };
            
            // Fallbacks
            if (dictName.StartsWith("dctmail", StringComparison.OrdinalIgnoreCase)) return new[] { "Mail Templates" };
            if (dictName.StartsWith("crddct", StringComparison.OrdinalIgnoreCase)) return new[] { "Credentials" };
            if (dictName.StartsWith("dctselector", StringComparison.OrdinalIgnoreCase)) return new[] { "Selectors" };
            return new[] { "Constants.Text", "Assets.Text" };
        }

        private List<XamlKeyUsage> ExtractKeysFromXaml(string content)
        {
            var keys = new List<XamlKeyUsage>();
            if (string.IsNullOrWhiteSpace(content)) return keys;

            var matches = _dictAccessRegex.Matches(content);
            foreach (Match match in matches)
            {
                string dictName = match.Groups[1].Value.Trim();
                string key = match.Groups[2].Value.Trim();

                if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(dictName))
                {
                    string[] expectedCats = GetExpectedCategoriesFromDictName(dictName);
                    // Avoid duplicates in the localized file
                    if (!keys.Any(x => x.DictName.Equals(dictName, StringComparison.OrdinalIgnoreCase) && x.Key.Equals(key, StringComparison.OrdinalIgnoreCase)))
                    {
                        keys.Add(new XamlKeyUsage { DictName = dictName, ExpectedCategories = expectedCats, Key = key });
                    }
                }
            }
            return keys;
        }

        private string FindProjectRoot(string filePath)
        {
            var dir = Path.GetDirectoryName(filePath);
            while (!string.IsNullOrEmpty(dir))
            {
                if (File.Exists(Path.Combine(dir, "project.json")))
                    return dir;
                dir = Path.GetDirectoryName(dir);
            }
            return null;
        }
    }
}
