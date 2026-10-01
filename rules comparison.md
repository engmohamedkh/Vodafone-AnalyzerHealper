# Vodafone Custom Rules: Validation & Comparison Report

A comprehensive validation and comparison between the legacy Studio analyzer rules (**[old rules.md](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/old%20rules.md)** located in `VodafoneActivitiesCustomRules` and `VodafoneWorkFlowsCustomRules`) and the modern standalone AnalyzerHelper suite (**[new rules.md](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/new%20rules.md)** located in `AnalyzerHelper/Rules`).

---

## 1. Executive Summary

| Metric | [new rules.md](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/new%20rules.md) (`AnalyzerHelper/Rules`) | [old rules.md](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/old%20rules.md) (Legacy Projects) |
| :--- | :---: | :---: |
| **Total Rules Documented** | **30** | **48** (45 unique rules) |
| **Execution Environment** | Standalone WPF Desktop App (`.NET 6.0`) | UiPath Studio Workflow Analyzer Plugins (`.NET Framework 4.6.1`) |
| **Fix Capabilities** | **15 rules feature Autofix / Interactive Fix** | Check & Report only (No automated fixing) |
| **Direct 1-to-1 Matches** | 16 rules | 16 rules |
| **ID Conflicts / Remappings** | 9 rules | 9 rules |
| **Old Rules Not in New** | — | **19 rules** |
| **New Rules Not in Old** | **4 rules** | — |

---

## 2. Master Comparison & Cross-Reference Table

The table below maps all rules from both suites, sorted by Rule ID:

| Rule ID | In Old? | In New? | Old Rule Name | New Rule Name | Status / Comparison |
| :--- | :---: | :---: | :--- | :--- | :--- |
| **VF-002** | Yes | Yes | `ImageBasedActivities` | `ImageBasedActivities` | **Exact Match**: Flags image-based UI automation activities. |
| **VF-003** | Yes | Yes | `UndocumentedDelay` | `UndocumentedDelay` | **Exact Match**: Flags standalone `Delay` activities. |
| **VF-005** | Yes | Yes | `HardCodedPasswords` | `HardCodedPasswords` | **Exact Match**: Flags plain-text passwords in variables and properties. |
| **VF-008** | Yes | Yes | `Log Browser URL` | `Log Browser URL` | **Exact Match**: Validates target URL logging (New has **autofix**). |
| **VF-009** | Yes | **No** | `StartEndLogs` | — | **Missing in New**: Mandatory Start/End IAP logging activities. |
| **VF-010** | Yes | Yes | `SimulateAndSendWindowMessage` | `SimulateAndSendWindowMessage` | **Exact Match**: Ensures `SimulateClick`/`SimulateType` or `SendWindowMessages`. |
| **VF-012** | Yes | Yes | `TryCatch` | `Missing Catch Block Actions` | **Evolved**: New rule adds **autofix** to ensure `Catch` blocks contain error logging and `Rethrow`. |
| **VF-013** | **No** | Yes | — | `Remove Default Values (Variables & Arguments)` | **New in AnalyzerHelper**: Autofix rule removing default values from declarations. |
| **VF-014** | Yes | Yes | `MissingInOutArgument` (Invoke arguments) | `Empty Retry Scope Condition` (CheckTrue in RetryScope) | **ID Reassigned**: In old, `VF-014` checked invoke arguments (now `VF-020`). In new, `VF-014` **autofixes** empty RetryScope conditions with `CheckTrue`. |
| **VF-015** | Yes | Yes | `BusinessSystemException` | `BusinessSystemException` | **Exact Match**: Inventories thrown `BusinessRuleException` and `SystemException`. |
| **VF-016** | Yes | Yes | `ProhibitedActivities` | `ProhibitedActivities` | **Exact Match**: Flags forbidden activities (Outlook, WriteLine, MessageBox, DeleteQueueItems). |
| **VF-017** | Yes | Yes | `HardCodedPasswords` (Metadata) | `Workflow Annotations` | **ID Reassigned**: Old rule was `VF-033`. In new rules, `VF-017` provides a **batch annotation dialog**. |
| **VF-018** | Yes | Yes | `Branches Logging` (was VF-038 in code) | `Branches Logging` | **ID Reassigned**: Old was `VF-038`. In new rules, `VF-018` provides a **batch branch logging dialog**. |
| **VF-019** | **No** | Yes | — | `Remove Unused Variables/Arguments` | **New in AnalyzerHelper**: Autofix rule that removes unreferenced variables and arguments. |
| **VF-020** | **No** | Yes | — | `SyncInvokeArguments` | **New in AnalyzerHelper**: Autofix rule synchronizing invoke arguments with target XAML (evolved from old `VF-014`). |
| **VF-021** | Yes | Yes | `ValidateWorkFlowName` (filename check) | `FixComponentName` (`strComponentName` variable) | **Separated**: Old checked filenames. New rule **auto-updates** `strComponentName` to file base name; file renaming moved to `VF-065`. |
| **VF-022** | Yes | Yes | `RetryScope` (RetryScope condition) | `Config Constants Usage` (Config Excel lookup) | **ID Reassigned**: Old checked RetryScope (now `VF-014`). New rule **validates Config dictionary keys against the Excel Config file**. |
| **VF-024** | Yes | Yes | `WorkqueueEncryption` | `WorkqueueEncryption` | **Exact Match**: Inventories queue encryption activities (`EncryptText`, `DecryptText`). |
| **VF-025** | Yes | Yes | `Archtypes` | `Archtypes` | **Exact Match**: Inventories archetype analytics logging (`Analytics_Log`). |
| **VF-026** | Yes | **No** | `StartEndAppMonitoringLogs` | — | **Missing in New**: Start/End IAP App Monitoring logs around applications. |
| **VF-027** | Yes | Yes | `CommentOutActivity` | `HandleCommentedActivities` | **Enhanced in New**: Old reported comments; new provides **interactive Delete/Uncomment** dialog. |
| **VF-028** | Yes | Yes | `HardcodedArguments` | `HardcodedArguments` | **Enhanced in New**: Old reported hardcoded values; new provides **automated removal fix**. |
| **VF-029** | Yes | Yes | `IfElseCheck` | `IfElseCheck` | **Enhanced in New**: Old checked empty branches; new **identifies which branch (Then vs Else) is empty** and filters ViewState. |
| **VF-030** | Yes | Yes | `AddLogFields` | `AddLogFields` | **Exact Match**: Checks mandatory `AddLogFields` in Main (`Entry Sequence`) and `SetTransactionStatus`. |
| **VF-031** | Yes | **No** | `DefaultDisplayedName` | — | **Missing in New**: Flags activities keeping default UiPath names (e.g., "Assign", "Click"). |
| **VF-032** | Yes | **No** | `LogicFileUICheck` | — | **Missing in New**: Ensures Logic/Subprocess workflows contain no direct UI activities. |
| **VF-033** | Yes | (See VF-017) | `Annotations` | (Mapped to VF-017) | **ID Remapped**: Old `VF-033` mapped to modern `VF-017` with batch editor UI. |
| **VF-034** | Yes | **No** | `Naming Convention` | — | **Partially Covered**: Validates Main XAML naming and `strProcessIdentifier`. |
| **VF-035** | Yes | **No** | `VariablesNaming` | — | **Missing in New**: Enforces prefix-based Hungarian notation on variables (`str`, `int`, etc.). |
| **VF-036** | Yes | Yes | `MicrosoftOfficeActivities` | `MicrosoftOfficeActivities` | **Exact Match**: Flags Microsoft Office desktop activities (license tracking). |
| **VF-037** | Yes | (See VF-019) | `UnusedArguments` | (Mapped to VF-019) | **Superseded**: Old checked unused arguments; new `VF-019` cleans up both unused variables and arguments. |
| **VF-038** | Yes | (See VF-018) | `Branches Logging` | (Mapped to VF-018) | **ID Remapped**: Old `VF-038` mapped to modern `VF-018`. |
| **VF-039** | Yes | Yes | `PreAndPostConditions` | `Unused workflow files` | **ID Reassigned**: Old checked Pre/Post conditions. New rule **batch-deletes unreferenced `.xaml` files**. |
| **VF-040** | Yes | Yes | `Activities Number` | `Unreachable Flowchart Nodes` | **ID Reassigned**: Old limited activity count. New rule **auto-prunes orphan Flowchart nodes**. |
| **VF-041** | Yes | **No** | `BEInsideAutomation` | — | **Missing in New**: Prohibits throwing `BusinessRuleException` inside automation component workflows. |
| **VF-042** | Yes | **No** | `Multiple Apps Sequence` | — | **Missing in New**: Ensures a workflow interacts with only one target application. |
| **VF-043** | Yes | **No** | `Nested TryCatch` | — | **Missing in New**: Flags nested `TryCatch` activities within automation workflows. |
| **VF-044** | Yes | **No** | `HardCoded TimeOut` | — | **Missing in New**: Flags hardcoded numerical `TimeoutMS` values in UI activities. |
| **VF-045** | Yes | Yes | `HardCodedDelays` | `HardCodedDelays` | **Exact Match**: Flags hardcoded `DelayBefore` and `DelayAfter` numeric values. |
| **VF-046** | Yes | **No** | `Folder Structure` | — | **Missing in New**: Validates project folder structure compliance. |
| **VF-047** | Yes | Yes | `Nested IFs` | `Nested IFs` | **Exact Match**: Flags deeply nested `If` and loop conditions. |
| **VF-048** | Yes | **No** | `OrechestratorActivitesRetry` | — | **Missing in New**: Enforces that queue activities are enclosed in a `RetryScope`. |
| **VF-049** | Yes | **No** | `InputValidation` | — | **Missing in New**: Ensures workflows validate input arguments at start. |
| **VF-050** | Yes | **No** | `WQ Duplication Check` | — | **Missing in New**: Ensures queue item addition includes duplicate prevention logic. |
| **VF-051** | Yes | **No** | `Args Naming Convention` | — | **Missing in New**: Enforces argument directional prefixes (`in_`, `out_`, `io_`). |
| **VF-052** | Yes | **No** | `ResetPasswordCheck` | — | **Missing in New**: Checks for reset password workflow invocations. |
| **VF-053** | Yes | Yes | `CheckCETTimeZone` | `CETTimeZone` | **Enhanced in New**: Old checked SetTransactionStatus. New enforces CET on timestamps **project-wide with autofix**. |
| **VF-054** | Yes | (See VF-012) | `ActivityCounterRule` (`CatchBlock.cs`) | — | **Covered by VF-012**: Validates catch block contents. |
| **VF-055** | Yes | (See VF-012) | `RethrowRule` | — | **Covered by VF-012**: Validates rethrow placement. |
| **VF-056 / 060 / 063** | Yes | Yes (`VF-060`) | `Selector Attribute` | `SelectorAttribute` | **Standardized**: Flags unstable selector attributes (`idx`, `ctrlid`, etc.). |
| **VF-057 / 061 / 064** | Yes | **No** | `Selector Attribute Value` | — | **Missing in New**: Validates wildcards (`*`, `?`, `#`) in selector attribute values. |
| **VF-058 / 062** | Yes | **No** | `SetTransactionStatusOnlyInHandoff` | — | **Missing in New**: Prohibits `SetTransactionStatus` outside `SetTransactionStatus.xaml`. |
| **VF-065** | **No** | Yes | — | `WorkflowFileNaming` | **Reassigned in New**: Subprocess/Logic file naming with **batch rename and reference update**. |

---

## 3. Direct 1-to-1 Matches (Ported Rules)

These 16 rules have identical or direct parity between both suites:

1. **VF-002** (`ImageBasedActivities`): Flags image-based UI automation activities.
2. **VF-003** (`UndocumentedDelay`): Flags arbitrary `Delay` activities.
3. **VF-005** (`HardCodedPasswords`): Enforces secure credentials over hardcoded plain-text passwords.
4. **VF-008** (`Log Browser URL`): Validates target URL logging in `Open Browser` (New includes **autofix**).
5. **VF-010** (`SimulateAndSendWindowMessage`): Ensures UI activities have `SimulateClick`/`SimulateType` or `SendWindowMessages`.
6. **VF-015** (`BusinessSystemException`): Inventories thrown `SystemException` and `BusinessRuleException`.
7. **VF-016** (`ProhibitedActivities`): Flags forbidden activities (Outlook, WriteLine, MessageBox, DeleteQueueItems).
8. **VF-024** (`WorkqueueEncryption`): Inventories queue encryption activities (`EncryptText`, `DecryptText`).
9. **VF-025** (`Archtypes`): Inventories archetype analytics logging (`Analytics_Log`).
10. **VF-027** (`HandleCommentedActivities`): Finds commented activities (New provides **interactive Delete/Uncomment** dialog).
11. **VF-028** (`HardcodedArguments`): Checks static/hardcoded arguments (New provides **automated removal fix**).
12. **VF-029** (`IfElseCheck`): Ensures both Then and Else branches are populated (New reports **which branch is empty**).
13. **VF-030** (`AddLogFields`): Checks mandatory log fields in Main (`Entry Sequence`) and `SetTransactionStatus`.
14. **VF-036** (`MicrosoftOfficeActivities`): Flags Microsoft Office desktop activities (license tracking).
15. **VF-045** (`HardCodedDelays`): Flags hardcoded `DelayBefore` and `DelayAfter` numeric values.
16. **VF-047** (`Nested IFs`): Flags deeply nested `If` and loop conditions.

---

## 4. Rules Upgraded with Automated Fixes in AnalyzerHelper

In the legacy Studio plugins, rules could only inspect and return error/warning strings. In `AnalyzerHelper`, 15 rules have been upgraded to provide direct code transformation:

- **Autofix Rules (Non-interactive)**:
  - **VF-008** (`LogBrowserUrlRule`): Injects `Info_Log` with the target URL into `Open Browser`.
  - **VF-012** (`TryCatchRule`): Injects `Error_Log` and `Rethrow` into catch blocks.
  - **VF-013** (`RemoveDefaultsVarArgRule`): Clears default values from variables and arguments.
  - **VF-014** (`AddRetryScopeCheckTrueRule`): Injects `CheckTrue` into empty `RetryScope.Condition`.
  - **VF-019** (`RemoveUnusedVarArgRule`): Removes dead variables and arguments across the workflow.
  - **VF-020** (`SyncInvokeArgumentsRule`): Refreshes and binds arguments on `InvokeWorkflowFile` calls.
  - **VF-021** (`FixComponentNameRule`): Updates `strComponentName`/`strWorkflowName` default to match filename.
  - **VF-028** (`HardcodedArguments`): Deletes hardcoded values from invoke argument mappings.
  - **VF-040** (`FlowchartOrphanNodesRule`): Prunes disconnected nodes from flowcharts.
  - **VF-053** (`CetTimeZoneRule`): Converts timestamp assignments to CET timezone.

- **Interactive Fix Rules (Batch Dialogs)**:
  - **VF-017** (`AnnotationRule`): Interactive grid to edit and apply annotations across all files.
  - **VF-018** (`BranchesLogsRule`): Interactive grid to insert missing branch logs.
  - **VF-027** (`HandleCommentedActivitiesRule`): Interactive dialog to Delete or Uncomment activities.
  - **VF-039** (`UnusedWorkflowFilesRule`): Batch selection dialog to safely delete unused `.xaml` files.
  - **VF-065** (`WorkflowFileNamingRule`): Config dialog to rename files and update all `InvokeWorkflowFile` references project-wide.

---

## 5. Old Rules Not Yet Ported to AnalyzerHelper (19 Rules)

These rules exist only in the legacy folders (`VodafoneActivitiesCustomRules` and `VodafoneWorkFlowsCustomRules`) and represent candidates for future migration:

1. **VF-009** (`StartEndLogs`): Mandatory Start and End IAP logging activities.
2. **VF-026** (`StartEndAppMonitoringLogs`): Start and End IAP App Monitoring logs around applications.
3. **VF-031** (`DefaultDisplayedName`): Flags activities keeping default UiPath names (e.g., "Assign", "Click").
4. **VF-032** (`LogicFileUICheck`): Ensures Logic/Subprocess workflows contain no direct UI activities.
5. **VF-034** (`Naming Convention`): Main XAML naming and `strProcessIdentifier` variable validation.
6. **VF-035** (`VariablesNaming`): Prefix-based Hungarian notation naming standards on variables (`str`, `int`, etc.).
7. **VF-039** (Old) (`PreAndPostConditions`): Mandatory Pre-Condition and Post-Condition validations.
8. **VF-040** (Old) (`Activities Number`): Maximum allowed number of activities per sequence/workflow.
9. **VF-041** (`BEInsideAutomation`): Prohibits throwing `BusinessRuleException` inside low-level automation components.
10. **VF-042** (`Multiple Apps Sequence`): Ensures a workflow interacts with only one target application.
11. **VF-043** (`Nested TryCatch`): Flags nested `TryCatch` activities within automation workflows.
12. **VF-044** (`HardCoded TimeOut`): Flags hardcoded numerical `TimeoutMS` values in UI activities.
13. **VF-046** (`Folder Structure`): Verifies `.xaml` placement in designated project folders.
14. **VF-048** (`OrechestratorActivitesRetry`): Ensures queue activities are inside a `RetryScope`.
15. **VF-049** (`InputValidation`): Enforces input validation logic at workflow start.
16. **VF-050** (`WQ Duplication Check`): Enforces queue item duplicate prevention logic.
17. **VF-051** (`Args Naming Convention`): Enforces argument directional prefixes (`in_`, `out_`, `io_`).
18. **VF-052** (`ResetPasswordCheck`): Checks for reset password workflow invocations.
19. **VF-057 / VF-061 / VF-064** (`Selector Attribute Value`): Enforces wildcards (`*`, `?`, `#`) in selector attribute values (`title`, `name`, `aaname`).
20. **VF-058 / VF-062** (`SetTransactionStatusOnlyInHandoff`): Prohibits `SetTransactionStatus` outside `SetTransactionStatus.xaml`.

---

## 6. Next Steps for Full Parity

To bring `AnalyzerHelper` to 100% parity with the legacy rules, the following rules are recommended for immediate implementation:
- **VF-031** (`DefaultDisplayedName`): High impact, flags activities retaining default display names.
- **VF-032** (`LogicFileUICheck`): High impact, architectural rule ensuring UI activities are not in Logic/Subprocess files.
- **VF-041** (`BEInsideAutomation`): Architecture rule preventing Business Exceptions from low-level automation layers.
- **VF-044** (`HardCoded TimeOut`): Quality rule preventing hardcoded `TimeoutMS` in UI activities.
- **VF-051** (`Args Naming Convention`): Quality rule enforcing `in_`, `out_`, `io_` argument prefixes.
