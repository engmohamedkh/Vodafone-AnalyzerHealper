using System.Collections.Generic;
using AnalyzerHelper.Interfaces;
using AnalyzerHelper.Models;

namespace AnalyzerHelper.Rules
{
    /// <summary>VF-024 — inventory of EncryptText / DecryptText (workqueue encryption) usages.</summary>
    public sealed class WorkqueueEncryptionRule : IAnalyzerRule
    {
        public string RuleId => "VF-024";
        public string RuleName => "WorkqueueEncryption";
        public string DefaultRecommendation => "List of all workqueue encryption/decryption activities.";
        public bool RequiresUserInteraction => false;

        public IReadOnlyList<RuleCheckResult> Check(string filePath, string content)
        {
            var results = new List<RuleCheckResult>();
            if (!XamlActivityHelper.TryParse(content, out var doc) || doc == null) return results;

            foreach (var el in XamlActivityHelper.EnumerateActivities(doc))
            {
                bool isEncrypt = XamlActivityHelper.NameContains(el, "EncryptText") ||
                                 XamlActivityHelper.NameContains(el, "encrypttext");
                bool isDecrypt = XamlActivityHelper.NameContains(el, "DecryptText") ||
                                 XamlActivityHelper.NameContains(el, "decrypttext");
                if (!isEncrypt && !isDecrypt) continue;

                results.Add(new RuleCheckResult
                {
                    RuleId = RuleId,
                    RuleName = RuleName,
                    Level = RuleLevel.Info,
                    Message = $"The following activity {XamlActivityHelper.GetDisplayName(el)} is encryption or decryption activity.",
                    FilePath = filePath,
                    Recommendation = DefaultRecommendation
                });
            }
            return results;
        }
    }
}
