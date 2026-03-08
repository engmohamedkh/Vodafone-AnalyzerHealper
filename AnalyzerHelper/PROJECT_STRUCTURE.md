# AnalyzerHelper – Project structure

Code is split by **functionality** so you can find and edit one area without touching others.

---

## Folder layout

| Folder | Purpose | What to edit here |
|--------|--------|-------------------|
| **Interfaces/** | Contract definitions (rule + fix) | New rule contracts; signatures for Check / DefineAndFix |
| **Models/** | Data classes, enums, DTOs (properties) | Report row shape; file tree; rule list item; severity enum |
| **Rules/** | Rule implementations and execution | New rules (e.g. TryCatchRule); StandardRules registration; RuleRunner; FixRunner |
| **Services/** | Loading and registry | How solution files are loaded; how rules are listed for tabs |
| **Converters/** | WPF value converters | UI converters (e.g. bool → visibility) |
| **View/** | UI (XAML + code-behind) | Main window; bindings; buttons |
| **Docs/** | How-to and flow | e.g. ADD_NEW_RULE_AUTOMATED_FIX.md |

---

## Namespaces

- `AnalyzerHelper.Interfaces` – IAnalyzerRule, IAnalyzerRuleWithFix  
- `AnalyzerHelper.Models` – RuleLevel, RuleCheckResult, FixCategory, SolutionFileItem, SolutionTreeNode, AnalyzerReportRow, RoleFixItem, RoleReference, SolutionTreeBuilder  
- `AnalyzerHelper.Rules` – TryCatchRule, StandardRules, RuleRunner, FixRunner  
- `AnalyzerHelper.Services` – SolutionLoader, RoleFixRegistry  
- `AnalyzerHelper.Converters` – BooleanToVisibilityConverter, InverseBooleanToVisibilityConverter  
- `AnalyzerHelper.View` – MainWindow  

---

## Dependencies (who uses what)

- **View** → Interfaces, Models, Rules, Services  
- **Rules** → Interfaces, Models  
- **Services** → Rules, Models, Interfaces  
- **Interfaces** → Models  

The old **Roles/** folder has been removed; its types are in **Models/** and **Services/**.
