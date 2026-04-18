using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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
                DisplayName = HumanizeRuleName(r.RuleName),
                Description = r.DefaultRecommendation,
                Category = FixCategory.AutoFix
            }).OrderBy(r => r.RuleId).ToList();
        }

        public static IReadOnlyList<RoleFixItem> GetNeedInteractionRoles()
        {
            return StandardRules.GetNeedInteractionRules().Select(r => new RoleFixItem
            {
                RuleId = r.RuleId,
                DisplayName = HumanizeRuleName(r.RuleName),
                Description = r.DefaultRecommendation,
                Category = FixCategory.RequiresUserInteraction
            }).OrderBy(r => r.RuleId).ToList();
        }

        public static IReadOnlyList<AnalyzerReportRow> GetReportRows() => new List<AnalyzerReportRow>();

        public static IReadOnlyList<IAnalyzerRule> GetRulesByIds(IEnumerable<string> ruleIds)
        {
            var ids = new HashSet<string>(ruleIds ?? Enumerable.Empty<string>(), System.StringComparer.OrdinalIgnoreCase);
            return StandardRules.GetAll().Where(r => ids.Contains(r.RuleId)).ToList();
        }

        /// <summary>
        /// Converts PascalCase/camelCase rule names like "RemoveDefaultsVarArg" into
        /// human-readable form like "Remove Defaults Var Arg".
        /// </summary>
        public static string HumanizeRuleName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return name;
            // Insert space before uppercase letters that follow lowercase letters, or before an uppercase followed by lowercase
            var spaced = Regex.Replace(name, @"(?<=[a-z])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])", " ");
            return spaced.Trim();
        }
    }
}
