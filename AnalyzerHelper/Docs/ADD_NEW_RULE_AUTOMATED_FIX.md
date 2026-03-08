# How to Add a New Rule with Automated Fix (No Interaction)

This flow describes how to add a new rule that **reports** validation findings and **applies a fix automatically** (no user interaction). Use **TryCatchRule** as the reference implementation.

---

## Overview

| Flow | What runs | Purpose |
|------|-----------|--------|
| **Report** | `Check(filePath, content)` | Fill the Report tab with findings (Level, Message, File, Recommendation). |
| **Fix (No interaction)** | `DefineAndFix(filePath, content, out newContent)` | Decide if the file has the error and apply the fix; caller saves `newContent` to disk. |

The two flows are **independent**: the fix does not use the report; it defines the error and applies the fix inside the rule.

---

## Step-by-step flow

### 1. Create the rule class

**Location:** `AnalyzerHelper/Rules/`  
**Example:** `TryCatchRule.cs`  
**Interfaces:** `AnalyzerHelper/Interfaces/` (IAnalyzerRule, IAnalyzerRuleWithFix)  
**Models:** `AnalyzerHelper/Models/` (RuleCheckResult, RuleLevel)

- Create a new class that implements **both** `IAnalyzerRule` and **IAnalyzerRuleWithFix** (so it appears in Report and in the **No interaction** fix tab).
- Set **RequiresUserInteraction = false** so it is treated as automated fix.

```csharp
using System.Collections.Generic;
using AnalyzerHelper.Interfaces;
using AnalyzerHelper.Models;

namespace AnalyzerHelper.Rules
{
    public sealed class MyNewRule : IAnalyzerRuleWithFix
    {
        public string RuleId => "VF-XXX";           // e.g. "VF-099"
        public string RuleName => "MyNewRule";     // Shown in UI
        public string DefaultRecommendation => "Your recommendation text.";
        public bool RequiresUserInteraction => false;  // Automated fix

        // ---- Report path: used when user clicks "Run rules (validate)" ----
        public IReadOnlyList<RuleCheckResult> Check(string filePath, string content)
        {
            var results = new List<RuleCheckResult>();
            if (string.IsNullOrWhiteSpace(content)) return results;

            // 1) Detect the problem (e.g. missing element, bad pattern).
            bool hasProblem = /* your logic */;

            if (hasProblem)
            {
                results.Add(new RuleCheckResult
                {
                    RuleId = RuleId,
                    RuleName = RuleName,
                    Level = RuleLevel.Error,       // or RuleLevel.Warning
                    Message = "Clear message for the report",
                    FilePath = filePath,
                    Recommendation = DefaultRecommendation,
                    RequiresUserInteraction = RequiresUserInteraction
                });
            }

            return results;
        }

        // ---- Fix path: used when user clicks "Apply fix" from No interaction tab ----
        public bool DefineAndFix(string filePath, string content, out string newContent)
        {
            newContent = content;
            // 1) Decide if this file has the error (same/similar logic as Check).
            if (string.IsNullOrWhiteSpace(content) || !HasError(content))
                return false;

            // 2) Build fixed content (e.g. string replace, wrap, insert).
            newContent = ApplyFixLogic(content);
            return true;  // Caller will save newContent to file
        }

        private static bool HasError(string content) { /* ... */ }
        private static string ApplyFixLogic(string content) { /* ... */ }
    }
}
```

- **Check:** Used only for the **Report**. Return one or more `RuleCheckResult` for each finding (error/warning); return empty list if the file passes.
- **DefineAndFix:** Used only for the **Fix** tab. Return `true` only if you changed the content (and set `newContent`); otherwise return `false` and leave `newContent` unchanged. The app will write `newContent` to the file when you return `true`.

---

### 2. Register the rule in StandardRules

**File:** `AnalyzerHelper/Rules/StandardRules.cs`

Add an instance of your rule to the **AllRules** array:

```csharp
private static readonly IAnalyzerRule[] AllRules =
{
    new TryCatchRule(),
    new MyNewRule(),   // add here
};
```

- Rules with **RequiresUserInteraction = false** appear in the **No interaction** tab and in the report.
- No other code change is needed: **RoleFixRegistry** and the UI read from **StandardRules.GetAll()** / **GetAutoFixRules()**.

---

### 3. (Optional) Add unit tests

**Location:** `AnalyzerHelper.Tests/`

- **Check:** Test that `Check(path, content)` returns the expected findings (e.g. error when problem present, empty when fixed).
- **DefineAndFix:** Test that `DefineAndFix(path, content, out newContent)` returns `true` and produces correct XAML when the error is present, and `false` when it is not.

---

## End-to-end flow in the app

1. **Load solution**  
   User loads a folder → files are listed in the sidebar (tree + checkboxes).

2. **Report (Run rules)**  
   User clicks **Run rules (validate)** (from any tab) → for each **checked** file, **RuleRunner** calls **every** rule’s **Check(filePath, content)** → findings are shown in the **Analyzer Report** tab.

3. **No interaction tab**  
   **RoleFixRegistry.GetNoInteractionRoles()** returns rules where **RequiresUserInteraction = false** (from **StandardRules.GetAutoFixRules()**). Your new rule appears there with a checkbox.

4. **Apply fix**  
   User selects one or more rules (e.g. your rule) and clicks **Apply fix to selected files** → **FixRunner.ApplyFix** runs only for rules that implement **IAnalyzerRuleWithFix**. For each (rule, file) it calls **DefineAndFix(filePath, content, out newContent)**; if it returns `true`, it writes `newContent` to the file.

---

## Summary checklist

| Step | Action |
|------|--------|
| 1 | Create a new class in `AnalyzerHelper/Rules/` implementing `IAnalyzerRuleWithFix`. |
| 2 | Implement **Check** for report findings; implement **DefineAndFix** for automated fix; set **RequiresUserInteraction = false**. |
| 3 | Register the rule in **StandardRules.cs** (add to **AllRules**). |
| 4 | (Optional) Add tests in **AnalyzerHelper.Tests** for **Check** and **DefineAndFix**. |

No changes are required in **RoleFixRegistry**, **FixRunner**, or the UI: they discover your rule via **StandardRules** and use **Check** for the report and **DefineAndFix** for the fix.
