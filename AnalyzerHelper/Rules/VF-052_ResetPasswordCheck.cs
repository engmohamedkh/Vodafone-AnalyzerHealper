using System;
using System.Collections.Generic;
using System.IO;
using AnalyzerHelper.Interfaces;
using AnalyzerHelper.Models;

namespace AnalyzerHelper.Rules
{
    /// <summary>
    /// VF-052 — Validates that InitialiseApplications workflows implement the reset password mechanism
    /// (invoking IAP_ValidateApplicationsCredentials and handling reset logic).
    /// </summary>
    public sealed class ResetPasswordCheck : IAnalyzerRule
    {
        public string RuleId => "VF-052";
        public string RuleName => "ResetPasswordCheck";
        public string DefaultRecommendation => "Please ensure that Reset Password Mechanism is being used";
        public bool RequiresUserInteraction => false;

        public IReadOnlyList<RuleCheckResult> Check(string filePath, string content)
        {
            var results = new List<RuleCheckResult>();

            if (string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(content))
                return results;

            if (!filePath.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
                return results;

            string fileName = Path.GetFileNameWithoutExtension(filePath);
            bool isTargetWorkflow = fileName.IndexOf("initialiseapplications", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    fileName.IndexOf("initializeapplications", StringComparison.OrdinalIgnoreCase) >= 0;

            if (!isTargetWorkflow)
            {
                if (XamlActivityHelper.TryParse(content, out var doc) && doc?.Root != null)
                {
                    string? rootDisplayName = XamlActivityHelper.GetDisplayName(doc.Root);
                    if (!string.IsNullOrEmpty(rootDisplayName) &&
                        (rootDisplayName.IndexOf("initialiseapplications", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         rootDisplayName.IndexOf("initializeapplications", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        isTargetWorkflow = true;
                    }
                }
            }

            if (!isTargetWorkflow)
                return results;

            bool hasValidateCreds = content.IndexOf("iap_validateapplicationscredentials", StringComparison.OrdinalIgnoreCase) >= 0;
            bool hasReset = content.IndexOf("reset", StringComparison.OrdinalIgnoreCase) >= 0;

            if (!hasValidateCreds || !hasReset)
            {
                results.Add(new RuleCheckResult
                {
                    RuleId = RuleId,
                    RuleName = RuleName,
                    Level = RuleLevel.Warning,
                    FilePath = filePath,
                    Message = $"The following workflow: {fileName} does not have reset password",
                    Recommendation = DefaultRecommendation,
                    RequiresUserInteraction = false
                });
            }

            return results;
        }
    }
}
