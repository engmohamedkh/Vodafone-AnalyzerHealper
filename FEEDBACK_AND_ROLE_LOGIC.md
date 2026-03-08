# Feedback: Logic Use & Role-Dependent Rule Design

This document summarizes how your UiPath custom analyzer works, feedback on the current logic, and how to add **role-dependent** rule logic for both **activity-level** and **workflow-level** validation.

---

## 1. Solution flow (first to end)

1. **UiPath Studio** loads your analyzer packages (VodafoneActivitiesCustomRules, VodafoneWorkFlowsCustomRules) and calls `Initialize(IAnalyzerConfigurationService)` on the entry classes.
2. **Entry classes** (`VodafoneActivitiesCustomRules`, `VodafoneWorkFlowsCustomRules`) create one instance per rule and call each rule’s `Initialize(workflowAnalyzerConfigService)`.
3. **Each rule**:
   - Checks `HasFeature("WorkflowAnalyzerV4")` and returns if not supported.
   - Creates a `Rule<T>` with a delegate (e.g. `TryCatchCheck`, `ProhibtedActivities`) and `ErrorLevel` / `RecommendationMessage`.
   - Registers with `AddRule<IActivityModel>(...)` or `AddRule<IWorkflowModel>(...)`.
4. **At analysis time** UiPath runs:
   - **Activity rules** once per activity (only `IActivityModel` is passed).
   - **Workflow rules** once per workflow (full `IWorkflowModel`: `Root`, `RelativePath`, `DisplayName`, `Project.Directory`, etc.).

So: **activity-level** = rule runs per activity, **workflow-level** = rule runs per workflow and can inspect full structure and path.

---

## 2. Feedback on current logic

### Strengths

- Clear split: **VodafoneActivitiesCustomRules** (activity-level) vs **VodafoneWorkFlowsCustomRules** (workflow-level).
- Consistent pattern: `IRegisterAnalyzerConfiguration` → `Initialize` → `Rule<T>` → delegate returning `InspectionResult`.
- Version gating via `HasFeature("WorkflowAnalyzerV4")` avoids running on unsupported Studio versions.
- You already implement “role-like” behavior by **workflow type/location**: exclusions and inclusions based on `RelativePath`, `DisplayName`, and folder names (e.g. `_loader`, `_worker`, `Subprocess`, `logic`, `automation`).

### Areas to improve

1. **Duplicated “role” logic**  
   Exclusions are repeated in many rules with similar strings, e.g.:
   - `ExcludedWorkFlows`, `CurrentWorkflow.DisplayName`, path contains `IAP_`, `ProcessName_`, `Subprocess`, `_Loader`, `_Worker`, `_LoaderWorker`, `logic`, `worker.xaml`, `loader.xaml`, etc.  
   **Suggestion:** Centralize in a small **WorkflowRoleHelper** (see below) so one place defines “workflow role” and exclusions; rules call a single method.

2. **Inconsistent exclusion style**  
   Some rules use `ExcludedWorkFlows` (comma-separated display names), others use path/folder checks. Unifying behind a single “role” or “applies to” concept will make it easier to add new roles and rules.

3. **Activity rules have no workflow context**  
   The API gives activity rules only `IActivityModel`. So you **cannot** know the workflow path or “role” inside an activity rule callback. To make an activity rule role-dependent you have two options:
   - Implement the check in a **workflow rule** that walks `workflow.Root` and its children and applies logic per activity (by workflow role), or
   - Keep activity rules global and add **separate workflow-level rules** that enforce role-specific constraints (e.g. “in Logic workflows, no UI activities”).

4. **Magic strings**  
   Rule IDs (VF-xxx), feature name, excluded names, and path segments are hardcoded. Consider constants or a small config (e.g. JSON) so adding a new “role” or rule set doesn’t require editing many files.

5. **Error handling**  
   Some rules swallow exceptions and return `HasErrors = false` (e.g. `RetryScope`, `LogicFileUICheck`). Prefer logging and/or returning a clear “analysis failed” result so misconfiguration or API changes are visible.

---

## 3. How to add more role-dependent rule logic

### 3.1 What “role” means in your solution

Today, “role” is **implicit**: it’s the **kind of workflow** inferred from:

- **Path:** `RelativePath` or full path containing `logic`, `subprocess`, `automation`, `_loader`, `_worker`, `Navigate`, `Read`, `Write`, etc.
- **Name:** `DisplayName` or file name (e.g. `worker.xaml`, `loader.xaml`, `settransactionstatus.xaml`).

So “role” = **workflow type / template** (Loader, Worker, Process, Subprocess, Logic, Automation, etc.). You can extend this with more roles (e.g. “Framework”, “Integration”) by adding more path/name rules.

### 3.2 Centralize role logic (recommended)

Add a small shared helper so every rule uses the same definition of “workflow role” and “is excluded”.

Example (new shared project or static class in each project):

```csharp
// WorkflowRoleHelper.cs - put in a shared project or copy into both rule projects
using UiPath.Studio.Analyzer.Models;

public static class WorkflowRoleHelper
{
    public enum WorkflowRole
    {
        Unknown,
        Loader,
        Worker,
        LoaderWorker,
        Process,
        Subprocess,
        Logic,
        Automation,
        Framework
    }

    public static WorkflowRole GetRole(IWorkflowModel workflow)
    {
        if (workflow?.RelativePath == null) return WorkflowRole.Unknown;
        var path = (workflow.Project.Directory + "\\" + workflow.RelativePath).ToLower();
        var name = workflow.DisplayName?.ToLower() ?? "";
        var fileName = System.IO.Path.GetFileName(workflow.RelativePath)?.ToLower() ?? "";

        if (name.Contains("_loaderworker") || path.Contains("_loaderworker")) return WorkflowRole.LoaderWorker;
        if (name.Contains("_loader") || path.Contains("_loader") || fileName == "loader.xaml") return WorkflowRole.Loader;
        if (name.Contains("_worker") || path.Contains("_worker") || fileName == "worker.xaml") return WorkflowRole.Worker;
        if (path.Contains("subprocess")) return WorkflowRole.Subprocess;
        if (path.Contains("logic")) return WorkflowRole.Logic;
        if (path.Contains("automation")) return WorkflowRole.Automation;
        if (path.Contains("processname_") || name.Contains("processname_")) return WorkflowRole.Process;
        if (path.Contains("iap_") || name.Contains("iap_")) return WorkflowRole.Framework;
        if (path.Contains("framework")) return WorkflowRole.Framework;

        return WorkflowRole.Unknown;
    }

    /// <summary>Use in workflow rules: skip this workflow for this rule (e.g. framework/loader/worker).</summary>
    public static bool IsExcludedFromStandardRules(IWorkflowModel workflow)
    {
        var role = GetRole(workflow);
        return role == WorkflowRole.Loader || role == WorkflowRole.Worker || role == WorkflowRole.LoaderWorker
               || role == WorkflowRole.Process || role == WorkflowRole.Subprocess || role == WorkflowRole.Framework;
    }

    /// <summary>True only for Automation workflows (e.g. for RetryScope).</summary>
    public static bool IsAutomation(IWorkflowModel workflow) => GetRole(workflow) == WorkflowRole.Automation;

    /// <summary>True for Logic / Navigate / Read / Write (e.g. Rethrow, UI check).</summary>
    public static bool IsLogicOrComponent(IWorkflowModel workflow)
    {
        var path = (workflow.Project.Directory + "\\" + workflow.RelativePath).ToLower();
        var role = GetRole(workflow);
        return role == WorkflowRole.Logic || path.Contains("\\navigate\\") || path.Contains("\\read\\") || path.Contains("\\write\\");
    }
}
```

Then in a **workflow rule** you replace repeated conditions with:

```csharp
if (WorkflowRoleHelper.IsExcludedFromStandardRules(CurrentWorkflow))
    return new InspectionResult() { HasErrors = false };
```

And you can branch by role:

```csharp
switch (WorkflowRoleHelper.GetRole(CurrentWorkflow))
{
    case WorkflowRoleHelper.WorkflowRole.Automation:
        // apply automation-only checks
        break;
    case WorkflowRoleHelper.WorkflowRole.Logic:
        // apply logic-only checks (e.g. no UI, must rethrow)
        break;
    default:
        return new InspectionResult() { HasErrors = false };
}
```

### 3.3 Adding a new “role” (e.g. Integration)

1. Add `Integration` to `WorkflowRole` and in `GetRole()` add a condition (e.g. `path.Contains("integration")`).
2. In `IsExcludedFromStandardRules` (or a new helper like `ShouldEnforceIntegrationRules`), decide whether Integration workflows are excluded or included for specific rules.
3. In the rule’s delegate, use `GetRole(workflow)` and apply the new logic only for `WorkflowRole.Integration`.

### 3.4 Role-dependent rules at activity level

Because activity rules receive only `IActivityModel`, you have two patterns:

**Option A – Keep activity rules global**  
Leave rules like `ProhibtedActivity` and `ImageBasedActivities` as they are (apply to all activities). Add **workflow-level** rules for role-specific constraints (e.g. “In Logic workflows, no UI activities” is already in `LogicFileUICheck`).

**Option B – Move “role + activity” checks to workflow rules**  
For a rule that must behave differently by workflow role, implement it as a **workflow rule** and traverse activities:

```csharp
private InspectionResult EvaluateByRole(IWorkflowModel workflow, Rule rule)
{
    var role = WorkflowRoleHelper.GetRole(workflow);
    if (role != WorkflowRoleHelper.WorkflowRole.Logic)
        return new InspectionResult() { HasErrors = false };

    var issues = new List<string>();
    foreach (var activity in EnumerateActivities(workflow.Root))
    {
        if (IsProhibitedInLogic(activity))
            issues.Add($"Logic workflow must not use: {activity.DisplayName}");
    }
    return issues.Count > 0
        ? new InspectionResult() { HasErrors = true, Messages = issues, ... }
        : new InspectionResult() { HasErrors = false };
}
```

So: **more role-dependent need** → implement as workflow-level rule and branch on `WorkflowRoleHelper.GetRole(workflow)` (and optionally a shared “prohibited in role X” list).

### 3.5 Summary table

| Need | Where to implement | How to make it role-dependent |
|------|--------------------|-------------------------------|
| Rule applies to all workflows | Workflow rule | Use `IsExcludedFromStandardRules(workflow)` at start; optional branch by `GetRole(workflow)`. |
| Rule applies only to Automation | Workflow rule | If `!IsAutomation(workflow)` return no errors. |
| Rule applies only to Logic/component | Workflow rule | If `!IsLogicOrComponent(workflow)` return no errors (e.g. RethrowRule, LogicFileUICheck). |
| Different behavior per role | Workflow rule | `switch (GetRole(workflow))` and apply different checks per role. |
| “This activity is always bad” | Activity rule | Keep as today (no role; applies everywhere). |
| “This activity is bad only in role X” | Workflow rule | Traverse `workflow.Root` and check each activity; only report when `GetRole(workflow) == X`. |

---

## 4. Next steps

1. Introduce **WorkflowRoleHelper** (or equivalent) and replace duplicated path/display-name checks in existing workflow rules.
2. Define explicitly which roles you have (Loader, Worker, Process, Logic, Automation, etc.) and document which rules apply to which roles.
3. Add new roles (e.g. Integration) in the helper and then add new rules or branches that use `GetRole(workflow)`.
4. For new “role-dependent activity” needs, implement them as workflow rules that iterate activities and use `GetRole(workflow)` to decide what to check.

This keeps your current flow (UiPath → Initialize → AddRule → run delegate) unchanged and only centralizes and extends the “role” logic so you can add more role-dependent rules in one place.
