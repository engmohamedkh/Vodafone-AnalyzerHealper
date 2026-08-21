using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using AnalyzerHelper.Interfaces;
using AnalyzerHelper.Models;

namespace AnalyzerHelper.Rules
{
    /// <summary>VF-005 — hard-coded passwords / insecure password properties (cannot invent secrets).</summary>
    public sealed class HardCodedPasswordsRule : IAnalyzerRule
    {
        public string RuleId => "VF-005";
        public string RuleName => "HardCodedPasswords";
        public string DefaultRecommendation =>
            "Do not use hardcoded passwords. Prefer SecureString from Orchestrator/Config credentials.";
        public bool RequiresUserInteraction => false;

        public IReadOnlyList<RuleCheckResult> Check(string filePath, string content)
        {
            var results = new List<RuleCheckResult>();
            if (!XamlActivityHelper.TryParse(content, out var doc) || doc == null) return results;

            foreach (var el in XamlActivityHelper.EnumerateActivities(doc))
            {
                var messages = new List<string>();
                string display = XamlActivityHelper.GetDisplayName(el);

                // Password string properties with a value
                foreach (var attr in el.Attributes())
                {
                    string n = attr.Name.LocalName;
                    if (n.IndexOf("Password", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (string.IsNullOrWhiteSpace(attr.Value)) continue;

                    bool isSecureName = n.IndexOf("SecurePassword", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                        n.IndexOf("Secure", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!isSecureName)
                    {
                        messages.Add($"The following activity {display} is using string password property. Please use the secure string instead.");
                    }
                    else if (attr.Value.Contains("\",\""))
                    {
                        messages.Add($"The following activity {display} has hard coded secure password value.");
                    }
                }

                foreach (var varEl in el.Descendants().Where(e => e.Name.LocalName == "Variable"))
                {
                    string typeHint = string.Join(" ", varEl.Attributes().Select(a => a.Value));
                    if (typeHint.IndexOf("SecureString", StringComparison.OrdinalIgnoreCase) < 0) continue;

                    bool hasDefault = varEl.Attributes().Any(a =>
                            string.Equals(a.Name.LocalName, "Default", StringComparison.OrdinalIgnoreCase) &&
                            !string.IsNullOrWhiteSpace(a.Value))
                        || varEl.Elements().Any(e => e.Name.LocalName == "Variable.Default");
                    if (hasDefault)
                        messages.Add($"The following activity {display} has hard coded password variable.");
                }

                foreach (var msg in messages.Distinct())
                {
                    results.Add(new RuleCheckResult
                    {
                        RuleId = RuleId,
                        RuleName = RuleName,
                        Level = RuleLevel.Error,
                        Message = msg,
                        FilePath = filePath,
                        Recommendation = DefaultRecommendation
                    });
                }
            }

            // Also scan workflow-level variables
            foreach (var varEl in doc.Descendants().Where(e => e.Name.LocalName == "Variable"))
            {
                string typeHint = string.Join(" ", varEl.Attributes().Select(a => a.Value));
                if (typeHint.IndexOf("SecureString", StringComparison.OrdinalIgnoreCase) < 0) continue;
                bool hasDefault = varEl.Attributes().Any(a =>
                        string.Equals(a.Name.LocalName, "Default", StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrWhiteSpace(a.Value))
                    || varEl.Elements().Any(e => e.Name.LocalName == "Variable.Default");
                if (!hasDefault) continue;

                string name = varEl.Attributes()
                    .FirstOrDefault(a => string.Equals(a.Name.LocalName, "Name", StringComparison.OrdinalIgnoreCase))
                    ?.Value ?? "(unnamed)";
                results.Add(new RuleCheckResult
                {
                    RuleId = RuleId,
                    RuleName = RuleName,
                    Level = RuleLevel.Error,
                    Message = $"SecureString variable '{name}' has a default/hard-coded value.",
                    FilePath = filePath,
                    Recommendation = DefaultRecommendation
                });
            }

            return results;
        }
    }
}
