using System.Collections.Generic;
using System.Linq;
using AnalyzerHelper.Interfaces;
using AnalyzerHelper.Models;
using AnalyzerHelper.Rules;

namespace AnalyzerHelper.Services
{
    /// <summary>Registry for rules and report; provides rule lists for No/Need interaction tabs.</summary>
    public static class RoleFixRegistry
    {
        public static IReadOnlyList<RoleFixItem> GetNoInteractionRoles()
        {
            return StandardRules.GetAutoFixRules().Select(r => new RoleFixItem
            {
                RuleId = r.RuleId,
                DisplayName = r.RuleName,
                Description = r.DefaultRecommendation,
                Category = FixCategory.AutoFix
            }).ToList();
        }

        public static IReadOnlyList<RoleFixItem> GetNeedInteractionRoles()
        {
            return StandardRules.GetNeedInteractionRules().Select(r => new RoleFixItem
            {
                RuleId = r.RuleId,
                DisplayName = r.RuleName,
                Description = r.DefaultRecommendation,
                Category = FixCategory.RequiresUserInteraction
            }).ToList();
        }

        public static IReadOnlyList<AnalyzerReportRow> GetReportRows() => new List<AnalyzerReportRow>();

        public static IReadOnlyList<IAnalyzerRule> GetRulesByIds(IEnumerable<string> ruleIds)
        {
            var ids = new HashSet<string>(ruleIds ?? Enumerable.Empty<string>(), System.StringComparer.OrdinalIgnoreCase);
            return StandardRules.GetAll().Where(r => ids.Contains(r.RuleId)).ToList();
        }
    }
}
