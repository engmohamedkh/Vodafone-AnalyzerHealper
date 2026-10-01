using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;
using AnalyzerHelper.Interfaces;
using AnalyzerHelper.Models;

namespace AnalyzerHelper.Rules
{
    /// <summary>
    /// VF-034 — Checks Main workflow naming convention (PPMID_BusinessVertical_LocalMarket_ProcessName)
    /// for Main workflows (*_worker.xaml, *_loader.xaml, *_loaderworker.xaml),
    /// validates the strProcessIdentifier variable and default value, and verifies project.json name vs main.
    /// </summary>
    public sealed class NamingConvention : IAnalyzerRule
    {
        private const string ProcessIdentifierVarName = "strProcessIdentifier";

        private static readonly HashSet<string> BusinessVerticals = new(StringComparer.OrdinalIgnoreCase)
        {
            "CFSL", "CARE", "Enterprise", "HR", "TES", "Audit",
            "Finance-Operations", "Business", "SCM", "Human-Resources",
            "HumanResources", "RSA", "Network-Operations", "Technology-Enterprise"
        };

        private static readonly HashSet<string> LocalMarkets = new(StringComparer.OrdinalIgnoreCase)
        {
            "IT", "DE", "SPAIN", "UK", "VNO", "Group", "VOIS", "AL",
            "RO", "CZ", "ES", "GR", "SA", "IE", "VOIS-UK", "VNO-UK",
            "VNO-RO", "ES-Mob", "ES-WP", "VSSI"
        };

        public string RuleId => "VF-034";
        public string RuleName => "Naming Convention";
        public string DefaultRecommendation =>
            "Please use the following Naming Convention without any Special Characters: " +
            "'PPMID_BusinessVertical_LocalMarket_ProcessName' For Main Workflows Name and Process Identifier value.";
        public bool RequiresUserInteraction => false;

        public IReadOnlyList<RuleCheckResult> Check(string filePath, string content)
        {
            var results = new List<RuleCheckResult>();

            string fileName = Path.GetFileName(filePath);
            string fileNameLower = fileName.ToLowerInvariant();
            string lastToken = fileNameLower.Split('_').Last();

            bool isMainWorkflow = lastToken == "worker.xaml" ||
                                  lastToken == "loader.xaml" ||
                                  lastToken == "loaderworker.xaml";

            if (!isMainWorkflow)
            {
                return results;
            }

            string workflowDisplayName = Path.GetFileNameWithoutExtension(filePath);
            var mainDisplayNameTokens = workflowDisplayName.Split('_').ToList();

            // 1. Check Main Workflow File Name Structure
            if (mainDisplayNameTokens.Count >= 4)
            {
                bool validPpmId = int.TryParse(mainDisplayNameTokens[0], out _) && mainDisplayNameTokens[0].Length == 6;
                bool validBv = BusinessVerticals.Contains(mainDisplayNameTokens[1].Trim());
                bool validLm = LocalMarkets.Contains(mainDisplayNameTokens[2].Trim());

                if (!validPpmId || !validBv || !validLm)
                {
                    results.Add(new RuleCheckResult
                    {
                        RuleId = RuleId,
                        RuleName = RuleName,
                        Level = RuleLevel.Error,
                        Message = $"The following workflow Name: '{workflowDisplayName}' doesn't match the Needed Structure 'PPMID_BusinessVertical_LocalMarket_ProcessName'. " +
                                  $"[PPMID Validity: {validPpmId} ; BV Validity: {validBv} ; LM Validity: {validLm}]",
                        FilePath = filePath,
                        Recommendation = DefaultRecommendation
                    });
                }
            }
            else
            {
                results.Add(new RuleCheckResult
                {
                    RuleId = RuleId,
                    RuleName = RuleName,
                    Level = RuleLevel.Error,
                    Message = $"The following workflow Name: '{workflowDisplayName}' doesn't match the needed number of fields 'PPMID_BusinessVertical_LocalMarket_ProcessName'.",
                    FilePath = filePath,
                    Recommendation = DefaultRecommendation
                });
            }

            // 2. Check strProcessIdentifier Variable in XAML
            if (XamlActivityHelper.TryParse(content, out var doc) && doc?.Root != null)
            {
                var processIdentifierVar = doc.Descendants().FirstOrDefault(e =>
                    e.Name.LocalName == "Variable" &&
                    string.Equals(e.Attributes().FirstOrDefault(a => a.Name.LocalName == "Name")?.Value, ProcessIdentifierVarName, StringComparison.OrdinalIgnoreCase));

                if (processIdentifierVar != null)
                {
                    string? defaultValue = processIdentifierVar.Attributes().FirstOrDefault(a => a.Name.LocalName == "Default")?.Value;

                    if (string.IsNullOrWhiteSpace(defaultValue))
                    {
                        var defElement = processIdentifierVar.Elements().FirstOrDefault(e => e.Name.LocalName.EndsWith(".Default", StringComparison.OrdinalIgnoreCase));
                        if (defElement != null)
                        {
                            defaultValue = defElement.Descendants().FirstOrDefault(d => d.Name.LocalName == "Literal")?.Value
                                           ?? defElement.Descendants().FirstOrDefault(d => d.Name.LocalName == "VisualBasicValue")?.Attribute("Expression")?.Value
                                           ?? defElement.Value;
                        }
                    }

                    if (string.IsNullOrWhiteSpace(defaultValue))
                    {
                        results.Add(new RuleCheckResult
                        {
                            RuleId = RuleId,
                            RuleName = RuleName,
                            Level = RuleLevel.Error,
                            Message = $"The following workflow: '{workflowDisplayName}' has '{ProcessIdentifierVarName}' variable but its default value is empty.",
                            FilePath = filePath,
                            Recommendation = DefaultRecommendation
                        });
                    }
                    else
                    {
                        string cleanDefault = defaultValue.Replace("\"", "").Trim();
                        var identifierTokens = cleanDefault.Split('_').ToList();

                        if (identifierTokens.Count >= 4)
                        {
                            bool validPpmId = int.TryParse(identifierTokens[0], out _) && identifierTokens[0].Length == 6;
                            bool validBv = BusinessVerticals.Contains(identifierTokens[1].Trim());
                            bool validLm = LocalMarkets.Contains(identifierTokens[2].Trim());

                            if (!validPpmId || !validBv || !validLm)
                            {
                                results.Add(new RuleCheckResult
                                {
                                    RuleId = RuleId,
                                    RuleName = RuleName,
                                    Level = RuleLevel.Error,
                                    Message = $"The following workflow: '{workflowDisplayName}' has '{ProcessIdentifierVarName}' variable but its default value doesn't match the Needed Structure 'PPMID_BusinessVertical_LocalMarket_ProcessName'. " +
                                              $"[PPMID Validity: {validPpmId} ; BV Validity: {validBv} ; LM Validity: {validLm}]",
                                    FilePath = filePath,
                                    Recommendation = DefaultRecommendation
                                });
                            }
                        }
                        else
                        {
                            results.Add(new RuleCheckResult
                            {
                                RuleId = RuleId,
                                RuleName = RuleName,
                                Level = RuleLevel.Error,
                                Message = $"The following workflow: '{workflowDisplayName}' has '{ProcessIdentifierVarName}' variable but its default value doesn't match the Needed Number of fields 'PPMID_BusinessVertical_LocalMarket_ProcessName'.",
                                FilePath = filePath,
                                Recommendation = DefaultRecommendation
                            });
                        }
                    }
                }
                else
                {
                    results.Add(new RuleCheckResult
                    {
                        RuleId = RuleId,
                        RuleName = RuleName,
                        Level = RuleLevel.Error,
                        Message = $"The following workflow: '{workflowDisplayName}' Doesn't have {ProcessIdentifierVarName} Variable",
                        FilePath = filePath,
                        Recommendation = DefaultRecommendation
                    });
                }
            }

            // 3. Check project.json (name vs main)
            string? projectDir = FindProjectDirectory(filePath);
            if (!string.IsNullOrEmpty(projectDir))
            {
                string projectJsonPath = Path.Combine(projectDir, "project.json");
                if (File.Exists(projectJsonPath))
                {
                    try
                    {
                        using var jsonDoc = JsonDocument.Parse(File.ReadAllText(projectJsonPath));
                        if (jsonDoc.RootElement.TryGetProperty("name", out var nameProp) &&
                            jsonDoc.RootElement.TryGetProperty("main", out var mainProp))
                        {
                            string? pName = nameProp.GetString();
                            string? pMain = mainProp.GetString();
                            if (!string.IsNullOrEmpty(pName) && !string.IsNullOrEmpty(pMain))
                            {
                                string cleanMain = pMain.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase)
                                    ? pMain.Substring(0, pMain.Length - 5)
                                    : pMain;

                                if (!string.Equals(pName, cleanMain, StringComparison.OrdinalIgnoreCase))
                                {
                                    results.Add(new RuleCheckResult
                                    {
                                        RuleId = RuleId,
                                        RuleName = RuleName,
                                        Level = RuleLevel.Error,
                                        Message = "Name and Main don't have the same value",
                                        FilePath = filePath,
                                        Recommendation = DefaultRecommendation
                                    });
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Ignore malformed project.json
                    }
                }
            }

            return results;
        }

        private static string? FindProjectDirectory(string filePath)
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
