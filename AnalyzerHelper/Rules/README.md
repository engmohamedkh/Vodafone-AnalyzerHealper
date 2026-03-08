# Rules

**Purpose:** Rule implementations and rule execution.

- **TryCatchRule** – Example rule (implements `IAnalyzerRule` + `IAnalyzerRuleWithFix`). Add new rules here following the same pattern.
- **StandardRules** – Registers all rules; used by Report and by No/Need interaction tabs.
- **RuleRunner** – Runs `Check()` on selected files for the Report.
- **FixRunner** – Runs `DefineAndFix()` for selected rules/files when user clicks Apply fix.

To add a new rule: create a new class implementing the interfaces (see **Interfaces/**), then register it in **StandardRules.cs**.
