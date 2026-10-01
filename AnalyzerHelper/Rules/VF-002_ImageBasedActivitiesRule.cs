using System.Collections.Generic;
using AnalyzerHelper.Interfaces;
using AnalyzerHelper.Models;

namespace AnalyzerHelper.Rules
{
    /// <summary>VF-002 — flags image-based UI activities (replacement needs design).</summary>
    public sealed class ImageBasedActivitiesRule : IAnalyzerRule
    {
        private static readonly string[] ImageActivities =
        {
            "OnImageAppear", "OnImageVanish", "WaitingImageAppear", "FindImageMatches",
            "ImageFound", "ClickImage", "HoverImage", "WaitImageVanish", "ClickImageTrigger"
        };

        public string RuleId => "VF-002";
        public string RuleName => "ImageBasedActivities";
        public string DefaultRecommendation => "Please verify if required or use Windows/UI automation activities instead.";
        public bool RequiresUserInteraction => false;

        public IReadOnlyList<RuleCheckResult> Check(string filePath, string content)
        {
            var results = new List<RuleCheckResult>();
            if (!XamlActivityHelper.TryParse(content, out var doc) || doc == null) return results;

            foreach (var el in XamlActivityHelper.EnumerateActivities(doc))
            {
                foreach (var token in ImageActivities)
                {
                    if (!XamlActivityHelper.NameContains(el, token)) continue;
                    results.Add(new RuleCheckResult
                    {
                        RuleId = RuleId,
                        RuleName = RuleName,
                        Level = RuleLevel.Warning,
                        Message = $"The following activity: {XamlActivityHelper.GetDisplayName(el)} is image based activity, and it's not recommended to be used.",
                        FilePath = filePath,
                        Recommendation = DefaultRecommendation
                    });
                    break;
                }
            }
            return results;
        }
    }
}
