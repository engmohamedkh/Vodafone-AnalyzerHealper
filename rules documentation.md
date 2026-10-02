# Vodafone AnalyzerHelper — Rules Documentation Suite

> **Suite**: Modern Vodafone AnalyzerHelper (`AnalyzerHelper/Rules`)  
> **Target Environment**: UiPath Studio XAML Projects / Vodafone Intelligent Automation Platform (IAP) Standards  
> **Total Rules**: **54 Rules**  
> **Classification Breakdown**:
> - **Autofix Rules (10)**: Non-interactive, automated AST/XAML transformation
> - **Interactive Fix Rules (5)**: User-assisted batch dialogs (delete, uncomment, rename, customize)
> - **Validate-Only Rules (39)**: Code inspection, audit reporting, and developer guidance

---

## Table of Contents

1. [Architecture & Fix Execution Model](#1-architecture--fix-execution-model)
2. [Quick Reference Index (All 54 Rules)](#2-quick-reference-index-all-54-rules)
3. [Section A: Autofix Rules (10 Rules)](#3-section-a-autofix-rules-10-rules)
   - [VF-008: Log Browser URL](#vf-008-log-browser-url)
   - [VF-012: Missing Catch Block Actions](#vf-012-missing-catch-block-actions)
   - [VF-013: Remove Default Values (Variables & Arguments)](#vf-013-remove-default-values-variables--arguments)
   - [VF-019: Remove Unused Variables/Arguments](#vf-019-remove-unused-variablesarguments)
   - [VF-020: Sync Invoke Arguments](#vf-020-sync-invoke-arguments)
   - [VF-021: Fix Component Name](#vf-021-fix-component-name)
   - [VF-028: Hardcoded Arguments](#vf-028-hardcoded-arguments)
   - [VF-053: CET TimeZone Normalization](#vf-053-cet-timezone-normalization)
   - [VF-071: Unreachable Flowchart Nodes](#vf-071-unreachable-flowchart-nodes)
   - [VF-072: Empty Retry Scope Condition](#vf-072-empty-retry-scope-condition)
4. [Section B: Interactive Fix Rules (5 Rules)](#4-section-b-interactive-fix-rules-5-rules)
   - [VF-018: Branches Logging](#vf-018-branches-logging)
   - [VF-027: Handle Commented Activities](#vf-027-handle-commented-activities)
   - [VF-033: Workflow Annotations](#vf-033-workflow-annotations)
   - [VF-065: Workflow File Naming](#vf-065-workflow-file-naming)
   - [VF-070: Unused Workflow Files](#vf-070-unused-workflow-files)
5. [Section C: Validate-Only Rules (39 Rules)](#5-section-c-validate-only-rules-39-rules)
   - [VF-002: ImageBasedActivities](#vf-002-imagebasedactivities)
   - [VF-003: UndocumentedDelay](#vf-003-undocumenteddelay)
   - [VF-005: HardCodedPasswords](#vf-005-hardcodedpasswords)
   - [VF-009: StartEndLogs](#vf-009-startendlogs)
   - [VF-010: SimulateAndSendWindowMessage](#vf-010-simulateandsendwindowmessage)
   - [VF-014: MissingInOutArgument](#vf-014-missinginoutargument)
   - [VF-015: BusinessSystemException](#vf-015-businesssystemexception)
   - [VF-016: ProhibitedActivities](#vf-016-prohibitedactivities)
   - [VF-022: RetryScope](#vf-022-retryscope)
   - [VF-024: WorkqueueEncryption](#vf-024-workqueueencryption)
   - [VF-025: Archtypes](#vf-025-archtypes)
   - [VF-026: StartEndAppMonitoringLogs](#vf-026-startendappmonitoringlogs)
   - [VF-029: IfElseCheck](#vf-029-ifelsecheck)
   - [VF-030: AddLogFields](#vf-030-addlogfields)
   - [VF-031: DefaultDisplayedName](#vf-031-defaultdisplayedname)
   - [VF-032: LogicFileUICheck](#vf-032-logicfileuicheck)
   - [VF-034: Naming Convention](#vf-034-naming-convention)
   - [VF-035: VariablesNaming](#vf-035-variablesnaming)
   - [VF-036: MicrosoftOfficeActivities](#vf-036-microsoftofficeactivities)
   - [VF-037: UnusedArguments](#vf-037-unusedarguments)
   - [VF-038: Branches Logging](#vf-038-branches-logging-validate-only)
   - [VF-039: PreAndPostConditions](#vf-039-preandpostconditions)
   - [VF-040: Activities Number](#vf-040-activities-number)
   - [VF-041: BEInsideAutomation](#vf-041-beinsideautomation)
   - [VF-042: Multiple Apps Sequence](#vf-042-multiple-apps-sequence)
   - [VF-043: Nested TryCatch](#vf-043-nested-trycatch)
   - [VF-044: HardCoded TimeOut](#vf-044-hardcoded-timeout)
   - [VF-045: HardCodedDelays](#vf-045-hardcodeddelays)
   - [VF-046: Folder Structure](#vf-046-folder-structure)
   - [VF-047: Nested IFs](#vf-047-nested-ifs)
   - [VF-048: OrechestratorActivitesRetry](#vf-048-orechestratoractivitesretry)
   - [VF-049: InputValidation](#vf-049-inputvalidation)
   - [VF-050: WQ Duplication Check](#vf-050-wq-duplication-check)
   - [VF-051: Args Naming Convention](#vf-051-args-naming-convention)
   - [VF-052: ResetPasswordCheck](#vf-052-resetpasswordcheck)
   - [VF-057: static Selector](#vf-057-static-selector)
   - [VF-058: prohibited SetTransactionStatus](#vf-058-prohibited-settransactionstatus)
   - [VF-060: SelectorAttribute](#vf-060-selectorattribute)
   - [VF-073: Config Constants Usage](#vf-073-config-constants-usage)

---

## 1. Architecture & Fix Execution Model

The AnalyzerHelper engine processes UiPath automation projects by inspecting workflow XML structures (`.xaml`), project configurations (`project.json`), and project configuration spreadsheets (`Config.xlsx`). Rules are registered in [StandardRules.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/StandardRules.cs) and orchestrated via [RuleRunner.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/RuleRunner.cs) and [FixRunner.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/FixRunner.cs).

```
+-------------------------------------------------------------------------------+
|                           Vodafone AnalyzerHelper                             |
+-------------------------------------------------------------------------------+
       |                                |                              |
       v                                v                              v
+---------------+              +------------------+            +----------------+
|    Autofix    |              | Interactive Fix  |            | Validate-Only  |
|  (10 Rules)   |              |    (5 Rules)     |            |   (39 Rules)   |
+---------------+              +------------------+            +----------------+
| Direct AST    |              | Batch UI Dialogs |            | Reports & Lints|
| Non-blocking  |              | User-confirmed   |            | Manual fix in  |
| Idempotent    |              | Multi-file apply |            | UiPath Studio  |
+---------------+              +------------------+            +----------------+
```

### Execution Tiers:
1. **Autofix Rules (`IAnalyzerRuleWithFix`)**: Automated, non-interactive XAML refactoring. These rules parse the document tree, inject required activities or strip prohibited attributes/elements, preserve namespace bindings, format XML, and write changes safely.
2. **Interactive Fix Rules (`IBatchAnalyzerRuleWithFix` with `RequiresUserInteraction = true`)**: Rules that require contextual developer intent. They display a unified batch dialog across all project files, pre-populate intelligent defaults, allow row-by-row overrides or selections, and apply edits atomically.
3. **Validate-Only Rules (`IAnalyzerRule`)**: Pure analytical rules that produce detailed error/warning/info findings with line-specific context and exact remediation steps for developers in UiPath Studio.

---

## 2. Quick Reference Index (All 54 Rules)

| Rule ID | Rule Name | Fix Capability | Implementation File | Scope / Folder | Summary |
| :--- | :--- | :---: | :--- | :--- | :--- |
| **VF-002** | `ImageBasedActivities` | Validate-Only | [VF-002_ImageBasedActivitiesRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-002_ImageBasedActivitiesRule.cs) | All XAML | Flags image-based UI automation activities |
| **VF-003** | `UndocumentedDelay` | Validate-Only | [VF-003_UndocumentedDelayRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-003_UndocumentedDelayRule.cs) | All XAML | Flags standalone `Delay` / `DelayUntil` activities |
| **VF-005** | `HardCodedPasswords` | Validate-Only | [VF-005_HardCodedPasswordsRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-005_HardCodedPasswordsRule.cs) | All XAML | Detects plain-text hardcoded passwords |
| **VF-008** | `Log Browser URL` | **Autofix** | [VF-008_LogBrowserUrlRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-008_LogBrowserUrlRule.cs) | All XAML | Inserts `Info_Log` containing browser target URL |
| **VF-009** | `StartEndLogs` | Validate-Only | [VF-009_StartEndLogs.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-009_StartEndLogs.cs) | All XAML | Checks for mandatory `Start_Log` and `End_Log` |
| **VF-010** | `SimulateAndSendWindowMessage` | Validate-Only | [VF-010_SimulateAndSendWindowMessage.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-010_SimulateAndSendWindowMessage.cs) | All XAML | Enforces `SimulateClick` / `SendWindowMessages` |
| **VF-012** | `Missing Catch Block Actions` | **Autofix** | [VF-012_TryCatchRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-012_TryCatchRule.cs) | All XAML | Injects `Error_Log` and `Rethrow` in Catch blocks |
| **VF-013** | `Remove Default Values (Variables & Arguments)` | **Autofix** | [VF-013_RemoveDefaultsVarArgRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-013_RemoveDefaultsVarArgRule.cs) | All XAML | Strips hardcoded default values from variables/arguments |
| **VF-014** | `MissingInOutArgument` | Validate-Only | [VF-014_MissingInOutArgument.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-014_MissingInOutArgument.cs) | All XAML | Validates argument mappings in `InvokeWorkflowFile` |
| **VF-015** | `BusinessSystemException` | Validate-Only | [VF-015_BusinessSystemExceptionRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-015_BusinessSystemExceptionRule.cs) | All XAML | Reports inventory of BRE and SystemException throws |
| **VF-016** | `ProhibitedActivities` | Validate-Only | [VF-016_ProhibitedActivitiesRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-016_ProhibitedActivitiesRule.cs) | All XAML | Disallows `WriteLine`, `MessageBox`, Outlook, etc. |
| **VF-018** | `Branches Logging` | **Interactive Fix** | [VF-018_BranchesLogsRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-018_BranchesLogsRule.cs) | Flowcharts | Batch UI dialog to insert logs in Flowchart branches |
| **VF-019** | `Remove Unused Variables/Arguments` | **Autofix** | [VF-019_RemoveUnusedVarArgRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-019_RemoveUnusedVarArgRule.cs) | All XAML | Purges unused variable and argument declarations |
| **VF-020** | `SyncInvokeArguments` | **Autofix** | [VF-020_SyncInvokeArgumentsRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-020_SyncInvokeArgumentsRule.cs) | All XAML | Synchronizes invoked arguments with target workflow |
| **VF-021** | `FixComponentName` | **Autofix** | [VF-021_FixComponentNameRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-021_FixComponentNameRule.cs) | All XAML | Sets `strComponentName` default to file base name |
| **VF-022** | `RetryScope` | Validate-Only | [VF-022_RetryScopeRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-022_RetryScopeRule.cs) | `Automation\` | Enforces `TryCatch` wrapping `RetryScope` |
| **VF-024** | `WorkqueueEncryption` | Validate-Only | [VF-024_WorkqueueEncryptionRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-024_WorkqueueEncryptionRule.cs) | All XAML | Audits queue item encryption/decryption activities |
| **VF-025** | `Archtypes` | Validate-Only | [VF-025_ArchtypesRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-025_ArchtypesRule.cs) | All XAML | Audits archetype analytics logging activities |
| **VF-026** | `StartEndAppMonitoringLogs` | Validate-Only | [VF-026_StartEndAppMonitoringLogs.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-026_StartEndAppMonitoringLogs.cs) | All XAML | Validates presence of App Monitoring start/end logs |
| **VF-027** | `HandleCommentedActivities` | **Interactive Fix** | [VF-027_HandleCommentedActivitiesRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-027_HandleCommentedActivitiesRule.cs) | All XAML | Batch UI to delete or uncomment `<ui:CommentOut>` |
| **VF-028** | `HardcodedArguments` | **Autofix** | [VF-028_HardcodedArguments.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-028_HardcodedArguments.cs) | All XAML | Clears literal hardcoded argument values |
| **VF-029** | `IfElseCheck` | Validate-Only | [VF-029_IfElse.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-029_IfElse.cs) | All XAML | Flags empty `Then` or `Else` branches in `If` |
| **VF-030** | `AddLogFields` | Validate-Only | [VF-030_AddLogFields.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-030_AddLogFields.cs) | Main / SetTx | Enforces mandatory log fields and types |
| **VF-031** | `DefaultDisplayedName` | Validate-Only | [VF-031_DefaultDisplayedName.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-031_DefaultDisplayedName.cs) | All XAML | Flags activities retaining default UiPath names |
| **VF-032** | `LogicFileUICheck` | Validate-Only | [VF-032_LogicFileUICheck.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-032_LogicFileUICheck.cs) | `Logic\`, `Subprocess\` | Forbids UI automation activities in logic workflows |
| **VF-033** | `Workflow Annotations` | **Interactive Fix** | [VF-033_AnnotationRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-033_AnnotationRule.cs) | All XAML | Interactive batch editor for workflow 5-part header |
| **VF-034** | `Naming Convention` | Validate-Only | [VF-034_NamingConvention.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-034_NamingConvention.cs) | Main workflows | Enforces `PPMID_Vertical_Market_Process` format |
| **VF-035** | `VariablesNaming` | Validate-Only | [VF-035_VariablesNaming.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-035_VariablesNaming.cs) | All XAML | Enforces Hungarian variable prefixes & characters |
| **VF-036** | `MicrosoftOfficeActivities` | Validate-Only | [VF-036_MicrosoftOfficeActivitiesRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-036_MicrosoftOfficeActivitiesRule.cs) | All XAML | Flags desktop Office (Excel/Word) activities |
| **VF-037** | `UnusedArguments` | Validate-Only | [VF-037_UnusedArguments.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-037_UnusedArguments.cs) | All XAML | Validates declared arguments are used in expressions |
| **VF-038** | `Branches Logging` | Validate-Only | [VF-038_BranchesLogMessages.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-038_BranchesLogMessages.cs) | All XAML | Enforces logs at start of If/Switch/Loop branches |
| **VF-039** | `PreAndPostConditions` | Validate-Only | [VF-039_PreAndPostConditions.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-039_PreAndPostConditions.cs) | `Automation\` | Enforces Pre-Condition & Post-Condition blocks |
| **VF-040** | `Activities Number` | Validate-Only | [VF-040_NonUIActivitiesNum.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-040_NonUIActivitiesNum.cs) | `Logic\`, `Subprocess\` | Enforces maximum 100 activities per logic workflow |
| **VF-041** | `BEInsideAutomation` | Validate-Only | [VF-041_BEInsideAutomation.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-041_BEInsideAutomation.cs) | `Automation\` | Forbids throwing `BusinessRuleException` in UI |
| **VF-042** | `Multiple Apps Sequence` | Validate-Only | [VF-042_MultipleAppsSequence.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-042_MultipleAppsSequence.cs) | `Automation\` | Prevents single workflow targeting multiple apps |
| **VF-043** | `Nested TryCatch` | Validate-Only | [VF-043_AutomationNestedTryCatch.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-043_AutomationNestedTryCatch.cs) | `Automation\` | Forbids nested `TryCatch` in automation files |
| **VF-044** | `HardCoded TimeOut` | Validate-Only | [VF-044_HardCodedTimeOut.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-044_HardCodedTimeOut.cs) | All XAML | Flags hardcoded numeric `TimeoutMS` values |
| **VF-045** | `HardCodedDelays` | Validate-Only | [VF-045_HardCodedDelaysRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-045_HardCodedDelaysRule.cs) | All XAML | Flags numeric `DelayBefore` / `DelayAfter` |
| **VF-046** | `Folder Structure` | Validate-Only | [VF-046_FolderStructure.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-046_FolderStructure.cs) | Project-wide | Enforces standardized subfolder hierarchy |
| **VF-047** | `Nested IFs` | Validate-Only | [VF-047_NestedIfsRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-047_NestedIfsRule.cs) | All XAML | Flags `If` statements nested >= 3 levels |
| **VF-048** | `OrechestratorActivitesRetry` | Validate-Only | [VF-048_OrechestratorActivitesRetry.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-048_OrechestratorActivitesRetry.cs) | All XAML | Enforces `RetryScope` around Queue activities |
| **VF-049** | `InputValidation` | Validate-Only | [VF-049_ValidateInput.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-049_ValidateInput.cs) | Entry files | Enforces invocation of `IAP_ValidateInputFiles` |
| **VF-050** | `WQ Duplication Check` | Validate-Only | [VF-050_WQDuplicates.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-050_WQDuplicates.cs) | `UploadItems.xaml` | Enforces invocation of duplicate check workflow |
| **VF-051** | `Args Naming Convention` | Validate-Only | [VF-051_ArgsNamingConvention.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-051_ArgsNamingConvention.cs) | All XAML | Enforces `in_`/`out_`/`io_` prefixes and Hungarian types |
| **VF-052** | `ResetPasswordCheck` | Validate-Only | [VF-052_ResetPasswordCheck.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-052_ResetPasswordCheck.cs) | Init workflows | Enforces invocation of credential validation |
| **VF-053** | `CETTimeZone` | **Autofix** | [VF-053_CetTimeZoneRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-053_CetTimeZoneRule.cs) | All XAML | Normalizes start/end timestamps to CET timezone |
| **VF-057** | `static Selector` | Validate-Only | [VF-057_SelectorAttributeValueFromXaml.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-057_SelectorAttributeValueFromXaml.cs) | All XAML | Enforces proper wildcarding in `title`, `name`, `aaname` |
| **VF-058** | `prohibited SetTransactionStatus` | Validate-Only | [VF-058_SetTransactionStatusOnlyInHandoff.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-058_SetTransactionStatusOnlyInHandoff.cs) | Project-wide | Limits `SetTransactionStatus` to Handoff state |
| **VF-060** | `SelectorAttribute` | Validate-Only | [VF-060_SelectorAttributeRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-060_SelectorAttributeRule.cs) | All XAML | Forbids unstable attributes (`idx`, `sessionid`, etc.) |
| **VF-065** | `WorkflowFileNaming` | **Interactive Fix** | [VF-065_WorkflowFileNamingRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-065_WorkflowFileNamingRule.cs) | `Logic\`, `Subprocess\` | Interactive batch rename for workflow files |
| **VF-070** | `Unused workflow files` | **Interactive Fix** | [VF-070_UnusedWorkflowFilesRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-070_UnusedWorkflowFilesRule.cs) | `Automation\`, etc. | Discovers unreferenced `.xaml` files with batch delete |
| **VF-071** | `Unreachable Flowchart Nodes` | **Autofix** | [VF-071_FlowchartOrphanNodesRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-071_FlowchartOrphanNodesRule.cs) | Flowcharts | Prunes disconnected nodes & dangling references |
| **VF-072** | `Empty Retry Scope Condition` | **Autofix** | [VF-072_AddRetryScopeCheckTrueRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-072_AddRetryScopeCheckTrueRule.cs) | All XAML | Injects `<ui:CheckTrue Expression="True"/>` in empty retry |
| **VF-073** | `Config Constants Usage` | Validate-Only | [VF-073_ConfigConstantsUsageRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-073_ConfigConstantsUsageRule.cs) | All XAML | Validates dictionary keys against Config spreadsheet |

---

## 3. Section A: Autofix Rules (10 Rules)

Autofix rules implement programmatic refactoring without requiring human intervention. When triggered via the AnalyzerHelper UI or CLI, these rules directly modify the workflow AST/XML, apply safe modifications, preserve namespaces and formatting, and immediately persist changes.

---

### VF-008: Log Browser URL

| Attribute | Details |
| :--- | :--- |
| **Rule ID** | `VF-008` |
| **Rule Name** | `Log Browser URL` |
| **Fix Capability** | **Autofix** (Non-interactive) |
| **Implementation File** | [VF-008_LogBrowserUrlRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-008_LogBrowserUrlRule.cs) |
| **Target Scope** | Any `.xaml` containing `<ui:OpenBrowser>` |
| **Default Severity** | `Warning` |

#### Objective & Rationale
In accordance with Vodafone Intelligent Automation Platform (IAP) audit standards, every workflow that launches a web browser via `OpenBrowser` must explicitly log the destination URL before performing subsequent UI actions. This guarantees operational traceability in Orchestrator logs during production runs and facilitates triage when URLs change across environments.

#### Detection Logic
The rule scans for `<ui:OpenBrowser>` activity elements in the XAML AST:
1. Extracts the browser URL value from either the `Url` XML attribute or an inner `<ui:OpenBrowser.Url>` child element.
2. Examines all child activities within the browser sequence body.
3. Searches for an existing `<i:Info_Log>` or `<ui:LogMessage>` activity whose message expression includes or concatenates the target URL.
4. If no such log exists as the first executable activity inside the browser container, a violation is flagged.

#### Recommendation / Error Message
```text
Add Info_Log or Log Message inside Open Browser whose message includes the Url value.
```

#### Autofix Behavior
- Automatically locates the entry point of the inner sequence inside `<ui:OpenBrowser>`.
- Generates an `<i:Info_Log>` activity with `StrMessage` configured to log the destination URL (e.g. `StrMessage="[""Navigating to: "" + strUrl]"`).
- Automatically registers the IAP activity namespace (`xmlns:i="clr-namespace:Vodafone_Logging;assembly=Vodafone.Logging"`) on the root element if not already present.
- Inserts the logging activity at the very top of the sequence body before any navigation or interaction activities.

#### Code Pattern Example
**Non-Compliant:**
```xml
<ui:OpenBrowser BrowserType="Chrome" Url="[in_Config(&quot;PortalUrl&quot;).ToString]">
  <ui:OpenBrowser.Body>
    <ActivityAction x:TypeArguments="x:Object">
      <Sequence DisplayName="Do">
        <ui:Click DisplayName="Click Login" ... />
      </Sequence>
    </ActivityAction>
  </ui:OpenBrowser.Body>
</ui:OpenBrowser>
```

**Compliant (After Autofix):**
```xml
<ui:OpenBrowser BrowserType="Chrome" Url="[in_Config(&quot;PortalUrl&quot;).ToString]">
  <ui:OpenBrowser.Body>
    <ActivityAction x:TypeArguments="x:Object">
      <Sequence DisplayName="Do">
        <i:Info_Log DisplayName="Log Browser URL" StrMessage="[&quot;Navigating to URL: &quot; + in_Config(&quot;PortalUrl&quot;).ToString]" />
        <ui:Click DisplayName="Click Login" ... />
      </Sequence>
    </ActivityAction>
  </ui:OpenBrowser.Body>
</ui:OpenBrowser>
```

---

### VF-012: Missing Catch Block Actions

| Attribute | Details |
| :--- | :--- |
| **Rule ID** | `VF-012` |
| **Rule Name** | `Missing Catch Block Actions` |
| **Fix Capability** | **Autofix** (Non-interactive) |
| **Implementation File** | [VF-012_TryCatchRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-012_TryCatchRule.cs) |
| **Target Scope** | All `.xaml` workflows containing `<TryCatch>` |
| **Default Severity** | `Error` |

#### Objective & Rationale
Consolidated from legacy rules `VF-012` (TryCatch validation), `VF-054` (Activity Counter), and `VF-055` (Rethrow Rule). In Vodafone enterprise automation, swallowed exceptions or inadequately logged errors lead to zombie executions and untraceable incident reports. Every `Catch` block must log the failure using `Error_Log` (an `Info_Log` is strictly prohibited in catch blocks) and terminate with a `Rethrow` to bubble the exception up to the state machine or caller.

#### Detection Logic
For every `<Catch x:TypeArguments="...">` block in the document:
1. Validates that the catch sequence contains an error logging activity (`<i:Error_Log>` or `<ui:LogMessage Level="Error">`).
2. Detects if an `<i:Info_Log>` is mistakenly used instead of `<i:Error_Log>`.
3. Validates that the catch sequence contains a terminal `<Rethrow />` activity.
4. Flags a violation if either error logging or rethrow is missing, or if `Info_Log` is used.

#### Recommendation / Error Message
```text
Catch block must contain Error_Log and Rethrow. Auto-fix will insert missing log/rethrow or upgrade Info_Log to Error_Log.
```

#### Autofix Behavior
- **Upgrade Info_Log**: If `<i:Info_Log>` is present, transforms the tag in-place to `<i:Error_Log>`, preserving existing message bindings and attributes while updating `StrTag="error"`.
- **Insert Error_Log**: If no log activity exists, synthesizes and injects:
  ```xml
  <i:Error_Log StrMessage="[exception.Source + &quot; &quot; + exception.Message]" DisplayName="Error Log" StrTag="error" />
  ```
  at the beginning of the catch sequence body.
- **Insert Rethrow**: If `<Rethrow />` is missing, injects `<Rethrow DisplayName="Rethrow" />` immediately before the closing `</Sequence>` tag of the catch handler.

#### Code Pattern Example
**Non-Compliant:**
```xml
<Catch x:TypeArguments="s:Exception">
  <ActivityAction x:TypeArguments="s:Exception">
    <ActivityAction.Handler>
      <Sequence DisplayName="Catch Handler">
        <i:Info_Log StrMessage="[exception.Message]" />
      </Sequence>
    </ActivityAction.Handler>
  </ActivityAction>
</Catch>
```

**Compliant (After Autofix):**
```xml
<Catch x:TypeArguments="s:Exception">
  <ActivityAction x:TypeArguments="s:Exception">
    <ActivityAction.Handler>
      <Sequence DisplayName="Catch Handler">
        <i:Error_Log StrMessage="[exception.Source + &quot; &quot; + exception.Message]" DisplayName="Error Log" StrTag="error" />
        <Rethrow DisplayName="Rethrow" />
      </Sequence>
    </ActivityAction.Handler>
  </ActivityAction>
</Catch>
```

---

### VF-013: Remove Default Values (Variables & Arguments)

| Attribute | Details |
| :--- | :--- |
| **Rule ID** | `VF-013` |
| **Rule Name** | `Remove Default Values (Variables & Arguments)` |
| **Fix Capability** | **Autofix** (Non-interactive) |
| **Implementation File** | [VF-013_RemoveDefaultsVarArgRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-013_RemoveDefaultsVarArgRule.cs) |
| **Target Scope** | All `.xaml` workflows across the project |
| **Default Severity** | `Warning` |

#### Objective & Rationale
Hardcoded default values assigned directly in variable declarations or argument schemas lead to environment-specific regressions, mask missing arguments in `InvokeWorkflowFile` calls, and bypass centralized configuration in Orchestrator/Config assets. All dynamic data must be passed via arguments or initialized explicitly via Assign activities, never via default field values.

#### Detection Logic
The rule scans:
1. `<Variable>` elements in `<Sequence.Variables>` or `<Flowchart.Variables>` for `Default="..."` XML attributes or `<Variable.Default>` child blocks.
2. Root `x:Members` arguments and companion `<this:WorkflowName.ArgumentName>` default value elements.
3. **Exemptions**: Explicitly exempts framework-mandated variables such as `strComponentName` and `strWorkflowName` (which must be initialized to the file name per `VF-021`).

#### Recommendation / Error Message
```text
Remove default values from Variable and Argument declarations (except exempt names). Auto-fix will clear default values.
```

#### Autofix Behavior
- Strips the `Default` attribute from `<Variable>` declarations.
- Removes any `<Variable.Default>` inner child elements.
- Strips companion `<this:WorkflowName.ArgName>` default argument elements declared on the root `<Activity>`.
- Leaves the variable or argument declaration intact with its correct type and name.

---

### VF-019: Remove Unused Variables/Arguments

| Attribute | Details |
| :--- | :--- |
| **Rule ID** | `VF-019` |
| **Rule Name** | `Remove Unused Variables/Arguments` |
| **Fix Capability** | **Autofix** (Non-interactive) |
| **Implementation File** | [VF-019_RemoveUnusedVarArgRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-019_RemoveUnusedVarArgRule.cs) |
| **Target Scope** | All `.xaml` workflows across the project |
| **Default Severity** | `Warning` |

#### Objective & Rationale
Over time, workflows accumulate declared variables and arguments that are no longer referenced after refactoring. Unused declarations inflate XAML size, confuse developers during maintenance, and increase memory footprint during state machine execution.

#### Detection Logic
1. Reads all declared `<Variable>` names within sequence/flowchart scopes and all `<x:Property>` names within `<x:Members>`.
2. Extracts the execution body of the workflow (starting from the root `Sequence` or `Flowchart`, excluding `x:Members` and declaration headers).
3. Performs whole-word boundary token matching (`VariableName`) across all activity attributes, VisualBasic expressions, conditions, and inner text.
4. Identifies variables and arguments whose declared names never occur in the execution body.

#### Recommendation / Error Message
```text
Variable or Argument is declared but never referenced in workflow execution body. Auto-fix will remove the unused declaration.
```

#### Autofix Behavior
- Removes unused `<Variable ... />` nodes from `<Sequence.Variables>` or `<Flowchart.Variables>`.
- Removes unused `<x:Property Name="..." ... />` entries from `<x:Members>`.
- Removes any companion `<this:...>` default tags corresponding to the deleted argument.
- Cleanly purges empty `<Sequence.Variables>` wrapper tags if no variables remain.

---

### VF-020: Sync Invoke Arguments

| Attribute | Details |
| :--- | :--- |
| **Rule ID** | `VF-020` |
| **Rule Name** | `Sync Invoke Arguments` |
| **Fix Capability** | **Autofix** (Non-interactive) |
| **Implementation File** | [VF-020_SyncInvokeArgumentsRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-020_SyncInvokeArgumentsRule.cs) |
| **Target Scope** | Any workflow invoking other XAMLs via `<ui:InvokeWorkflowFile>` |
| **Default Severity** | `Error` |

#### Objective & Rationale
When a sub-workflow is updated with new arguments or has obsolete arguments removed, caller workflows often fail at runtime with `ArgumentException` or execute with unassigned inputs. This rule automates the equivalent of UiPath Studio's "Import Arguments" feature.

#### Detection Logic
1. Inspects every `<ui:InvokeWorkflowFile>` activity in the caller workflow.
2. Resolves the relative path specified in the `WorkflowFileName` property to locate the target `.xaml` on disk.
3. Parses the target XAML's `<x:Members>` to read its exact declared arguments, their directions (`InArgument`, `OutArgument`, `InOutArgument`), and data types.
4. Compares caller arguments inside `<ui:InvokeWorkflowFile.Arguments>` against the target:
   - Detects missing arguments in caller.
   - Detects stale/obsolete arguments in caller that no longer exist in target.
   - Detects direction/type mismatches.

#### Recommendation / Error Message
```text
InvokeWorkflowFile argument bindings are out of sync with target workflow definition. Auto-fix will synchronize arguments.
```

#### Autofix Behavior
- Prunes obsolete arguments from `<ui:InvokeWorkflowFile.Arguments>`.
- Injects missing arguments matching the exact direction and generic type declared in the target file (leaving value expression blank for developer binding).
- Updates argument direction tags (e.g. from `InArgument` to `InOutArgument`) when modified in the callee.

---

### VF-021: Fix Component Name

| Attribute | Details |
| :--- | :--- |
| **Rule ID** | `VF-021` |
| **Rule Name** | `Fix Component Name` |
| **Fix Capability** | **Autofix** (Non-interactive) |
| **Implementation File** | [VF-021_FixComponentNameRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-021_FixComponentNameRule.cs) |
| **Target Scope** | All `.xaml` workflows across the project |
| **Default Severity** | `Warning` |

#### Objective & Rationale
Vodafone IAP standard requires every workflow to define a variable named `strComponentName` (or `strWorkflowName`) whose default value matches the physical file name of the workflow (without `.xaml` extension). This variable is passed to `Start_Log`, `End_Log`, and exception handlers to ensure accurate component logging in Kibana/Splunk dashboards.

#### Detection Logic
1. Finds any variable declared in the root scope named `strComponentName` or `strWorkflowName`.
2. Computes the expected file base name: `Path.GetFileNameWithoutExtension(filePath)`.
3. Checks whether the default value attribute matches `"[expectedName]"` or `expectedName`.
4. If missing or mismatched, flags a violation.

#### Recommendation / Error Message
```text
Variable 'strComponentName' or 'strWorkflowName' default value must match the workflow file name. Auto-fix will update the default value.
```

#### Autofix Behavior
- Locates `<Variable x:TypeArguments="x:String" Name="strComponentName" ... />`.
- Sets `Default="[filename]"` (or literal `Default="&quot;filename&quot;"`) matching the current file base name.

---

### VF-028: Hardcoded Arguments

| Attribute | Details |
| :--- | :--- |
| **Rule ID** | `VF-028` |
| **Rule Name** | `Hardcoded Arguments` |
| **Fix Capability** | **Autofix** (Non-interactive) |
| **Implementation File** | [VF-028_HardcodedArguments.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-028_HardcodedArguments.cs) |
| **Target Scope** | All workflows containing `<ui:InvokeWorkflowFile>` |
| **Default Severity** | `Warning` |

#### Objective & Rationale
Passing hardcoded literal strings (e.g. `"C:\Temp\Input.xlsx"`, `"True"`, `"5"`) directly into `InvokeWorkflowFile` argument bindings creates hidden configuration dependencies, prevents dynamic parameterization across dev/test/prod environments, and violates Vodafone clean-code guidelines.

#### Detection Logic
1. Scans `<ui:InvokeWorkflowFile.Arguments>` in all workflows.
2. For each argument element (`<InArgument>`, `<OutArgument>`, `<InOutArgument>`), inspects its text or value expression.
3. Detects literal quotes (`"literal"`), hardcoded file paths, plain numbers, or static booleans that are not variables or configuration dictionary lookups.

#### Recommendation / Error Message
```text
Do not pass hardcoded literal values to workflow arguments in InvokeWorkflowFile. Auto-fix will remove hardcoded literals.
```

#### Autofix Behavior
- Clears the hardcoded literal value from the argument entry in the caller's XAML.
- Leaves the argument key intact so the developer can bind a valid variable or Config key in UiPath Studio.

---

### VF-053: CET TimeZone Normalization

| Attribute | Details |
| :--- | :--- |
| **Rule ID** | `VF-053` |
| **Rule Name** | `CET TimeZone Normalization` |
| **Fix Capability** | **Autofix** (Non-interactive) |
| **Implementation File** | [VF-053_CetTimeZoneRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-053_CetTimeZoneRule.cs) |
| **Target Scope** | All `.xaml` workflows across the project |
| **Default Severity** | `Error` |

#### Objective & Rationale
Vodafone Group operates across multiple global markets. To guarantee accurate SLA tracking, reporting, and transaction auditing in Orchestrator and analytics databases, all transaction start and end timestamps (`dtmTransactionStartTime` and `dtmTransactionEndTime`) must be recorded in Central European Time (CET).

#### Detection Logic
1. Inspects all `<Assign>` activities throughout the project.
2. Checks `<Assign.To>`: detects assignments targeting `dtmTransactionStartTime`, `dtmtransactionStartTime`, or `dtmTransactionEndTime`.
3. Checks `<Assign.Value>`: verifies whether the expression explicitly invokes `TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time")`.
4. Flags an error if `DateTime.Now`, `DateTime.UtcNow`, or non-CET conversions are used.

#### Recommendation / Error Message
```text
dtmTransactionStartTime and dtmTransactionEndTime assignments must explicitly convert to CET timezone using Central European Standard Time.
```

#### Autofix Behavior
- Rewrites the assignment value expression to the standard Vodafone CET conversion:
  ```vb
  TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time"))
  ```

---

### VF-071: Unreachable Flowchart Nodes

| Attribute | Details |
| :--- | :--- |
| **Rule ID** | `VF-071` |
| **Rule Name** | `Unreachable Flowchart Nodes` |
| **Fix Capability** | **Autofix** (Non-interactive) |
| **Implementation File** | [VF-071_FlowchartOrphanNodesRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-071_FlowchartOrphanNodesRule.cs) |
| **Target Scope** | All workflows containing `<Flowchart>` |
| **Default Severity** | `Warning` |

#### Objective & Rationale
When flowcharts are edited, deleted transitions or copy-pasted activities often leave orphaned nodes behind. These unreachable nodes remain in the XAML file, bloat project size, confuse code reviews, and may reference deleted variables.

#### Detection Logic
1. Identifies the flowchart's `StartNode` reference.
2. Traverses the graph via Breadth-First Search (BFS), tracking all outgoing connectors: `FlowStep.Next`, `FlowDecision.True`, `FlowDecision.False`, `FlowSwitch.Default`, and case branches (including `<x:Reference>` resolution).
3. Any `FlowStep`, `FlowDecision`, or `FlowSwitch` defined inside the flowchart that is not reachable from `StartNode` is flagged as an orphan.

#### Recommendation / Error Message
```text
Flowchart contains unreachable/disconnected nodes. Auto-fix will prune orphan nodes and remove dangling references.
```

#### Autofix Behavior
- Prunes each disconnected node subtree from the Flowchart.
- Scans for and removes any sibling `<x:Reference>` entries that pointed to the pruned nodes.
- Preserves all valid graph paths intact.

---

### VF-072: Empty Retry Scope Condition

| Attribute | Details |
| :--- | :--- |
| **Rule ID** | `VF-072` |
| **Rule Name** | `Empty Retry Scope Condition` |
| **Fix Capability** | **Autofix** (Non-interactive) |
| **Implementation File** | [VF-072_AddRetryScopeCheckTrueRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-072_AddRetryScopeCheckTrueRule.cs) |
| **Target Scope** | All workflows containing `<ui:RetryScope>` |
| **Default Severity** | `Warning` |

#### Objective & Rationale
In UiPath, a `RetryScope` without a condition activity inside `<ui:RetryScope.Condition>` will retry only if an unhandled exception occurs in its action block. However, if the developer intended to retry based on business/UI state (or needs guaranteed successful condition evaluation), leaving the condition block empty can cause silent failures.

#### Detection Logic
1. Finds all `<ui:RetryScope>` elements in the workflow.
2. Inspects `<ui:RetryScope.Condition>`.
3. Checks if the condition container contains an `<ActivityFunc>` with no executable child activity.

#### Recommendation / Error Message
```text
RetryScope condition is empty. Auto-fix will insert a CheckTrue (Expression='True') activity.
```

#### Autofix Behavior
- Injects `<ui:CheckTrue Expression="True" DisplayName="Check True" />` inside the condition `<ActivityFunc>` block.
- Ensures the retry scope evaluates properly while preventing runtime exceptions.

## 4. Section B: Interactive Fix Rules (5 Rules)

Interactive Fix rules address compliance violations where context-dependent business decisions or naming choices are required from the RPA developer. Instead of applying automated blind edits, AnalyzerHelper displays an interactive batch review interface allowing developers to inspect, customize, and approve changes in bulk across the entire project.

---

### VF-018: Branches Logging

| Attribute | Details |
| :--- | :--- |
| **Rule ID** | `VF-018` |
| **Rule Name** | `Branches Logging` |
| **Fix Capability** | **Interactive Fix** (Batch Dialog) |
| **Implementation File** | [VF-018_BranchesLogsRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-018_BranchesLogsRule.cs) |
| **Target Scope** | Flowcharts containing `FlowDecision` or `FlowSwitch` |
| **Default Severity** | `Warning` |

#### Objective & Rationale
Vodafone IAP operational standards require clear execution traceability through conditional routing. When a process takes a specific branch at a decision point, a corresponding log entry must record which condition was evaluated and which path was taken. This rule ensures every branch of `FlowDecision` and `FlowSwitch` logs its entry before subsequent logic or nested decisions occur.

#### Detection Logic
1. Scans all `Flowchart` activities across the project.
2. Identifies every `FlowDecision` node (`True` and `False` branches) and every `FlowSwitch` node (`Default` branch and individual case targets).
3. Walks each branch forward step-by-step:
   - Validates that an `<i:Info_Log>`, `<i:Message_Log>`, or `<i:Error_Log>` exists at this branch level.
   - Halts evaluation if the branch encounters another nested `FlowDecision` or `FlowSwitch` (logs inside a downstream decision do not satisfy the parent branch requirement).
4. Resolves `<x:Reference>` pointers across flowchart steps.
5. Flags any branch path that lacks an initial logging activity.

#### Recommendation / Error Message
```text
FlowDecision (True/False) or FlowSwitch branch does not begin with an Info_Log or Log Message.
```

#### Interactive Fix Behavior
- Gathers all unlogged branches across the entire solution.
- Opens a unified **Batch Branches Logging Review** window.
- The UI displays:
  - Workflow path
  - Decision activity name (e.g. `FlowDecision "Check Invoices Found?"`)
  - Branch target (`True`, `False`, `Default`, or case value)
  - Pre-populated proposed log message (e.g. `"Invoices found branch entered"`)
- The developer can review, edit the log message text, select/deselect rows, and click **Apply Fixes** to inject all log activities into the XAML files simultaneously.

---

### VF-027: Handle Commented Activities

| Attribute | Details |
| :--- | :--- |
| **Rule ID** | `VF-027` |
| **Rule Name** | `Handle Commented Activities` |
| **Fix Capability** | **Interactive Fix** (Batch Review Dialog) |
| **Implementation File** | [VF-027_HandleCommentedActivitiesRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-027_HandleCommentedActivitiesRule.cs) |
| **Target Scope** | All `.xaml` workflows across the project |
| **Default Severity** | `Error` |

#### Objective & Rationale
Developers frequently use UiPath's "Comment Out" feature during debugging. However, releasing workflows to production with commented-out activities violates Vodafone quality gates: it obscures code clarity, risks retaining dead or sensitive test logic, and degrades performance. Developers must either permanently delete or uncomment the activities prior to deployment.

#### Detection Logic
1. Scans the XAML AST for `<ui:CommentOut>` activities.
2. Tracks the nesting depth of commented activities (top-level vs nested inside other commented blocks).
3. Flags an `Error` for every commented activity found in the project.

#### Recommendation / Error Message
```text
Workflow contains commented-out activities (ui:CommentOut). Remove or restore them before release.
```

#### Interactive Fix Behavior
- Launches the **Commented Activities Manager** dialog displaying all detected `<ui:CommentOut>` instances grouped by file.
- For each item, the developer can select one of three actions:
  - **Delete**: Permanently removes the `<ui:CommentOut>` container and all activities nested inside it.
  - **Uncomment**: Unwraps the inner activities from the comment container and places them back directly into the parent sequence.
  - **Keep**: Leaves the comment intact (for explicit exceptions).
- Includes bulk "Delete All" and "Uncomment All" buttons for rapid resolution.

---

### VF-033: Workflow Annotations

| Attribute | Details |
| :--- | :--- |
| **Rule ID** | `VF-033` |
| **Rule Name** | `Workflow Annotations` |
| **Fix Capability** | **Interactive Fix** (Batch Annotation Editor) |
| **Implementation File** | [VF-033_AnnotationRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-033_AnnotationRule.cs) |
| **Target Scope** | All `.xaml` workflows across the project |
| **Default Severity** | `Warning` |

#### Objective & Rationale
Every workflow in a Vodafone automation must have a structured, standardized header annotation on its root activity (`Sequence` or `Flowchart`). This documentation ensures transparency during code audits, handovers, and support triage. The annotation must include exactly 5 mandatory sections:
1. `Component Name:`
2. `Description:`
3. `Pre Condition:`
4. `Post Condition:`
5. `PDD Section:`

#### Detection Logic
1. Locates the root `<Sequence>` or `<Flowchart>` in the XAML.
2. Checks for the `sap2010:Annotation.AnnotationText` attribute.
3. If the annotation is missing, or if any of the 5 mandatory section headers are absent, a violation is flagged.

#### Recommendation / Error Message
```text
Root sequence/flowchart must have an annotation containing: Component Name, Description, Pre Condition, Post Condition, PDD Section.
```

#### Interactive Fix Behavior
- Opens the **Batch Workflow Annotations Editor** dialog.
- Intelligently pre-populates fields:
  - **Component Name**: Auto-populated from the workflow file name.
  - **Pre Condition**: Auto-detected by inspecting Pre-Condition sequences and dynamic wait activities in the workflow.
  - **Post Condition**: Auto-detected by inspecting Post-Condition sequences.
  - **PDD Section**: Auto-filled or defaulted.
- Allows developers to quickly edit descriptions and conditions across all project files in a single grid and save changes directly to XAML headers.

---

### VF-065: Workflow File Naming

| Attribute | Details |
| :--- | :--- |
| **Rule ID** | `VF-065` |
| **Rule Name** | `Workflow File Naming` |
| **Fix Capability** | **Interactive Fix** (Batch Rename & Refactor) |
| **Implementation File** | [VF-065_WorkflowFileNamingRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-065_WorkflowFileNamingRule.cs) |
| **Target Scope** | Files residing in `Subprocess\` or `Logic\` folders |
| **Default Severity** | `Error` |

#### Objective & Rationale
Enforces Vodafone standard file naming conventions for modular business logic:
- **Subprocess workflows**: Must follow `{ShortName}_{Stage}_{WorkflowName}.xaml` (e.g. `SAP_01_ExtractInvoiceData.xaml`).
- **Logic workflows**: Must follow `{ShortName}_{WorkflowName}.xaml` (e.g. `CRM_ValidateCustomerStatus.xaml`).
- ReFramework template and IAP shared workflows are automatically exempted.

#### Detection Logic
1. Checks if the file path contains a directory named `Subprocess` or `Logic`.
2. Validates the file name against the required underscore-delimited naming patterns.
3. Detects invalid characters, incorrect token counts, or lowercase prefixes.

#### Recommendation / Error Message
```text
Workflow file name in Subprocess or Logic folder does not follow Vodafone naming convention ({ShortName}_{Stage}_{Name}.xaml or {ShortName}_{Name}.xaml).
```

#### Interactive Fix Behavior
- Opens the **Workflow Renaming Refactor** window.
- Suggests normalized, compliant file names for each offending file.
- When confirmed by the developer:
  - Renames the `.xaml` files on the filesystem.
  - Automatically searches all other project workflows for `<ui:InvokeWorkflowFile>` references targeting the old file names and updates them to the new paths atomically.

---

### VF-070: Unused Workflow Files

| Attribute | Details |
| :--- | :--- |
| **Rule ID** | `VF-070` |
| **Rule Name** | `Unused Workflow Files` |
| **Fix Capability** | **Interactive Fix** (Batch Deletion Confirmation) |
| **Implementation File** | [VF-070_UnusedWorkflowFilesRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-070_UnusedWorkflowFilesRule.cs) |
| **Target Scope** | `.xaml` files in `Automation\`, `Subprocess\`, and `Logic\` |
| **Default Severity** | `Warning` |

#### Objective & Rationale
During development, discarded prototypes, abandoned workflows, and obsolete test files often remain in the project repository. These unused files create confusion, violate repository cleanliness, and risk accidental invocation.

#### Detection Logic
1. Scans all project workflows to construct a complete graph of all invoked XAML paths from `<ui:InvokeWorkflowFile WorkflowFileName="...">`.
2. Gathers all physical `.xaml` files located under `Automation\`, `Subprocess\`, and `Logic\`.
3. Exempts entry points (`Main.xaml`, `*_worker.xaml`, `*_loader.xaml`, `*_loaderworker.xaml`, `Process.xaml`).
4. Identifies files that are never referenced anywhere in the invocation graph.

#### Recommendation / Error Message
```text
Unused workflow file detected. It is never invoked by any other workflow in the project.
```

#### Interactive Fix Behavior
- Displays the **Unused Workflow Cleaner** dialog.
- Lists all unreferenced `.xaml` files with their file paths, last modified dates, and sizes.
- Developers can check/uncheck files to confirm whether they should be deleted.
- Upon clicking **Delete Selected**, AnalyzerHelper removes the chosen files from disk safely.

## 5. Section C: Validate-Only Rules (39 Rules)

Validate-Only rules perform automated static analysis, architectural linting, and security audits across UiPath workflows. These rules do not alter the project files automatically; instead, they generate precise inspection reports with line-level context and actionable guidance for developers to remediate in UiPath Studio.

---

### VF-002: ImageBasedActivities

| Attribute | Details |
| :--- | :--- |
| **Rule ID** | `VF-002` |
| **Rule Name** | `ImageBasedActivities` |
| **Fix Capability** | **Validate-Only** |
| **Implementation File** | [VF-002_ImageBasedActivitiesRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-002_ImageBasedActivitiesRule.cs) |
| **Target Scope** | All `.xaml` workflows across the project |
| **Default Severity** | `Warning` |

#### Objective & Rationale
Image-based automation activities rely on screen coordinate matching and pixel rendering, making them inherently fragile across different screen resolutions, DPI scaling, and OS themes. Vodafone RPA guidelines mandate the use of native selector-based UI automation (Active Accessibility, UIAutomation, or Chromium API) whenever possible.

#### Detection Logic
The rule inspects the workflow AST for any activity inheriting from or matching image-based automation types:
- `ClickImage`, `HoverImage`, `ImageFound`, `WaitImageAppear`, `WaitImageVanish`, `FindImageMatches`, `OnImageAppear`.

#### Recommendation / Error Message
```text
Please verify if required or use Windows/UI automation activities instead.
```

#### Remediation Guide
1. Open the flagged workflow in UiPath Studio.
2. Locate the image-based activity.
3. Replace it with a robust selector-based UI activity (e.g. `Click`, `Hover`, `Check App State`, or `Element Exists`).
4. If image automation is genuinely unavoidable (e.g. Citrix virtual desktop without remote runtime), add an explicit justification annotation on the activity.

---

### VF-003: UndocumentedDelay

| Attribute | Details |
| :--- | :--- |
| **Rule ID** | `VF-003` |
| **Rule Name** | `UndocumentedDelay` |
| **Fix Capability** | **Validate-Only** |
| **Implementation File** | [VF-003_UndocumentedDelayRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-003_UndocumentedDelayRule.cs) |
| **Target Scope** | All `.xaml` workflows across the project |
| **Default Severity** | `Warning` |

#### Objective & Rationale
Hardcoded standalone delays (`<Delay>` or `<ui:DelayUntil>`) halt process execution indiscriminately regardless of whether target applications have already responded. This causes significant performance latency and masks underlying race conditions. Dynamic element synchronization must be used instead.

#### Detection Logic
1. Scans for `<Delay>` or `<ui:DelayUntil>` activities in the workflow tree.
2. Exempts delays that have explicit developer annotations justifying the timing or delays configured inside recognized retry loops.

#### Recommendation / Error Message
```text
Please use DelayBefore or DelayAfter instead, or remove unnecessary delays.
```

#### Remediation Guide
1. Remove standalone `Delay` activities.
2. Use dynamic synchronization activities such as `Wait Attribute`, `Element Exists`, or `Check App State`.
3. If brief pacing between keystrokes/clicks is needed, configure the `DelayBefore` or `DelayAfter` properties on the specific UI activity using Config variables.

---

### VF-005: HardCodedPasswords

| Attribute | Details |
| :--- | :--- |
| **Rule ID** | `VF-005` |
| **Rule Name** | `HardCodedPasswords` |
| **Fix Capability** | **Validate-Only** |
| **Implementation File** | [VF-005_HardCodedPasswordsRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-005_HardCodedPasswordsRule.cs) |
| **Target Scope** | All `.xaml` workflows across the project |
| **Default Severity** | `Error` |

#### Objective & Rationale
Storing plain-text passwords or secret credentials in XAML files violates Vodafone Information Security policies, GDPR, and enterprise compliance. All credentials must be stored securely in Orchestrator Credential Assets or CyberArk/HashiCorp vaults and handled in memory strictly as `System.Security.SecureString`. *(Note: This rule also covers legacy rule alias `VF-017`.)*

#### Detection Logic
1. Scans activities handling credentials, including `TypeInto`, `TypeSecureText`, database connection activities, mail activities (`SendOutlookMail`, `SendExchangeMail`, `GetIMAPMailMessages`), and HTTP Request activities.
2. Flags activities where password/token arguments are bound to literal strings (e.g. `"MySecretPassword123"`) instead of variables of type `SecureString` retrieved via `Get Credential` or `Get Robot Credential`.

#### Recommendation / Error Message
```text
Do not use hardcoded passwords. Prefer SecureString from Orchestrator/Config credentials.
```

#### Remediation Guide
1. Provision the target credentials in UiPath Orchestrator as a **Credential Asset**.
2. Retrieve credentials at runtime using the `Get Credential` activity (`in_Config("CredentialAsset").ToString`).
3. Pass the resulting `SecureString` output to `TypeSecureText` or secure connection activities.

---

### VF-009: StartEndLogs

| Attribute | Details |
| :--- | :--- |
| **Rule ID** | `VF-009` |
| **Rule Name** | `StartEndLogs` |
| **Fix Capability** | **Validate-Only** |
| **Implementation File** | [VF-009_StartEndLogs.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-009_StartEndLogs.cs) |
| **Target Scope** | All `.xaml` workflows across the project |
| **Default Severity** | `Error` |

#### Objective & Rationale
Vodafone Intelligent Automation Platform (IAP) standard mandates comprehensive logging lifecycle instrumentation. Every reusable workflow must log its entry with `Start_Log` and its exit with `End_Log`. Furthermore, any exception handler sequence inside Catch blocks must invoke `End_Log` passing `exception.Message` to provide clear tracking of failed components.

#### Detection Logic
1. Checks that the workflow contains an `<i:Start_Log>` activity as one of the first activities in the root sequence.
2. Checks that an `<i:End_Log>` activity is executed at the end of the normal execution path.
3. Checks every `<Catch>` block to ensure it contains an `<i:End_Log>` activity logging the exception details before rethrowing.

#### Recommendation / Error Message
```text
Please add start/end logs for every workflow created.
```

#### Remediation Guide
1. Add an `<i:Start_Log>` activity immediately after the workflow variable initialization, passing `strComponentName`.
2. Add an `<i:End_Log>` activity at the termination of the workflow sequence.
3. In Catch blocks, place `<i:End_Log>` immediately before the `<Rethrow />` activity.

---

### VF-010: SimulateAndSendWindowMessage

| Attribute | Details |
| :--- | :--- |
| **Rule ID** | `VF-010` |
| **Rule Name** | `SimulateAndSendWindowMessage` |
| **Fix Capability** | **Validate-Only** |
| **Implementation File** | [VF-010_SimulateAndSendWindowMessage.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-010_SimulateAndSendWindowMessage.cs) |
| **Target Scope** | All `.xaml` workflows containing UI automation |
| **Default Severity** | `Warning` |

#### Objective & Rationale
Standard hardware driver clicks and key-typing require the target application window to be in the foreground and actively focused. If a popup or lock screen intercepts focus, hardware clicks fail. Enabling `SimulateClick`/`SimulateType` or `SendWindowMessages` allows background automation without requiring OS input focus.

#### Detection Logic
1. Inspects UI interaction activities: `Click`, `TypeInto`, `Hover`, `SelectItem`, `Check`, etc.
2. Validates that either `SimulateClick` / `SimulateType = True` OR `SendWindowMessages = True`.
3. Flags activities where both properties are false or omitted (defaulting to hardware driver).

#### Recommendation / Error Message
```text
Please verify if SimulateClick or SendWindowMessages should be enabled for this activity.
```

#### Remediation Guide
1. Select the flagged activity in UiPath Studio.
2. In the Properties panel, locate the **Input** section.
3. Check either **SimulateClick** (recommended for Web/WPF applications) or **SendWindowMessages** (recommended for legacy Win32/Java applications).

---

### VF-014: MissingInOutArgument

| Attribute | Details |
| :--- | :--- |
| **Rule ID** | `VF-014` |
| **Rule Name** | `MissingInOutArgument` |
| **Fix Capability** | **Validate-Only** |
| **Implementation File** | [VF-014_MissingInOutArgument.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-014_MissingInOutArgument.cs) |
| **Target Scope** | Workflows containing `<ui:InvokeWorkflowFile>` |
| **Default Severity** | `Error` |

#### Objective & Rationale
When invoking sub-workflows, leaving mandatory input arguments unmapped or omitting output argument destinations causes `NullReferenceException` at runtime or drops processed data.

#### Detection Logic
1. Parses `<ui:InvokeWorkflowFile.Arguments>` across all `InvokeWorkflowFile` activities.
2. Inspects all mapped argument tags (`<InArgument>`, `<OutArgument>`, `<InOutArgument>`).
3. Flags arguments where the expression is empty (`""`), null, or missing a variable assignment.

#### Recommendation / Error Message
```text
Please check that all arguments are mapped in the InvokeWorkflowFile activity.
```

#### Remediation Guide
1. Click **Import Arguments** or **Edit Arguments** on the `InvokeWorkflowFile` activity.
2. Ensure every `in_` argument has a valid variable or Config key assigned.
3. Ensure every `out_` argument is bound to an active variable to capture the returned data.

---

### VF-015: BusinessSystemException

| Attribute | Details |
| :--- | :--- |
| **Rule ID** | `VF-015` |
| **Rule Name** | `BusinessSystemException` |
| **Fix Capability** | **Validate-Only** |
| **Implementation File** | [VF-015_BusinessSystemExceptionRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-015_BusinessSystemExceptionRule.cs) |
| **Target Scope** | All `.xaml` workflows across the project |
| **Default Severity** | `Info` |

#### Objective & Rationale
Provides a complete inventory report of all thrown exceptions across the automation solution, distinguishing between `BusinessRuleException` (expected business edge cases that should not trigger retries) and `SystemException` (infrastructure or application failures that require retry or remediation).

#### Detection Logic
1. Scans all `<Throw>` activities in the project.
2. Evaluates the instantiated exception type:
   - `ui:BusinessRuleException`
   - `s:Exception` / `s:SystemException`
3. Produces an audit inventory of all exception categories and throw messages.

#### Recommendation / Error Message
```text
Audit report of thrown BusinessRuleException and SystemException instances across workflows.
```

#### Remediation Guide
Review the generated inventory to verify that business logic violations (e.g. invalid customer ID) throw `BusinessRuleException` and technical errors (e.g. database timeout) throw `SystemException`.

---

### VF-016: ProhibitedActivities

| Attribute | Details |
| :--- | :--- |
| **Rule ID** | `VF-016` |
| **Rule Name** | `ProhibitedActivities` |
| **Fix Capability** | **Validate-Only** |
| **Implementation File** | [VF-016_ProhibitedActivitiesRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-016_ProhibitedActivitiesRule.cs) |
| **Target Scope** | All `.xaml` workflows across the project |
| **Default Severity** | `Error` |

#### Objective & Rationale
Certain legacy, interactive, or destructive activities are strictly prohibited in Vodafone unattended automation:
- `MessageBox`, `InputDialog`: Block unattended robot execution awaiting human interaction.
- `WriteLine`: Outputs only to local Visual Studio output, bypassing Orchestrator logs.
- Legacy Outlook desktop activities (`SendOutlookMail`, etc.): Introduce desktop client dependencies.
- `DeleteQueueItems`: Destroys audit trails in Orchestrator queues.

#### Detection Logic
Scans the XAML AST for prohibited activity types:
- `WriteLine`, `MessageBox`, `InputDialog`
- `SendOutlookMail`, `GetOutlookMailMessages`
- `DeleteQueueItems`

#### Recommendation / Error Message
```text
Prohibited activity detected. Use standard logging, IAP mail activities, or Orchestrator status updates instead.
```

#### Remediation Guide
1. Replace `WriteLine` with `<i:Info_Log>` or `<ui:LogMessage>`.
2. Remove interactive modal activities (`MessageBox`, `InputDialog`).
3. Replace desktop Outlook activities with Vodafone IAP Mail activities or Microsoft Graph API activities.
4. Replace `DeleteQueueItems` with `SetTransactionStatus` or queue postponement.

---

### VF-022: RetryScope

| Attribute | Details |
| :--- | :--- |
| **Rule ID** | `VF-022` |
| **Rule Name** | `RetryScope` |
| **Fix Capability** | **Validate-Only** |
| **Implementation File** | [VF-022_RetryScopeRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-022_RetryScopeRule.cs) |
| **Target Scope** | Workflows in the `Automation\` folder |
| **Default Severity** | `Warning` |

#### Objective & Rationale
Automation components interact with volatile external applications (web browsers, SAP, desktop clients). To handle transient network glitches or lag without failing the entire transaction, all main actions in `Automation\` workflows must be wrapped in a `TryCatch` containing a `RetryScope` configured with an explicit evaluation condition.

#### Detection Logic
1. Checks if the workflow resides under the `Automation\` folder.
2. Verifies whether the sequence contains a top-level `<TryCatch>` wrapping a `<ui:RetryScope>`.
3. Validates that the `RetryScope` defines a non-empty condition.

#### Recommendation / Error Message
```text
Automation workflow must contain a TryCatch wrapping a RetryScope with a valid condition.
```

#### Remediation Guide
1. Wrap the core interaction sequence in a `RetryScope`.
2. Configure `NumberOfRetries` (e.g. `3`) and `RetryInterval` (e.g. `00:00:05`).
3. Place an `Element Exists` or `CheckTrue` condition inside `<ui:RetryScope.Condition>`.
4. Wrap the `RetryScope` inside an outer `TryCatch`.

---

### VF-024: WorkqueueEncryption

| Attribute | Details |
| :--- | :--- |
| **Rule ID** | `VF-024` |
| **Rule Name** | `WorkqueueEncryption` |
| **Fix Capability** | **Validate-Only** |
| **Implementation File** | [VF-024_WorkqueueEncryptionRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-024_WorkqueueEncryptionRule.cs) |
| **Target Scope** | All `.xaml` workflows across the project |
| **Default Severity** | `Info` |

#### Objective & Rationale
Under GDPR and Vodafone Data Privacy policies, personally identifiable information (PII) and sensitive financial data uploaded to Orchestrator Work Queues must be encrypted at rest. This rule provides an audit catalog of encryption and decryption activities.

#### Detection Logic
1. Scans for cryptography activities: `EncryptText`, `DecryptText`, `EncryptFile`, `DecryptFile`.
2. Validates that processes processing sensitive queue payloads implement encryption prior to queue item upload.

#### Recommendation / Error Message
```text
Audit inventory of workqueue encryption and decryption activities.
```

#### Remediation Guide
Ensure all PII fields (national IDs, credit card numbers, passwords) are encrypted via AES cryptography before calling `AddQueueItem`, and decrypted only in memory when processed by the worker.

---

### VF-025: Archtypes

| Attribute | Details |
| :--- | :--- |
| **Rule ID** | `VF-025` |
| **Rule Name** | `Archtypes` |
| **Fix Capability** | **Validate-Only** |
| **Implementation File** | [VF-025_ArchtypesRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-025_ArchtypesRule.cs) |
| **Target Scope** | All `.xaml` workflows across the project |
| **Default Severity** | `Info` |

#### Objective & Rationale
Vodafone enterprise analytics classifies automation processes by architectural archetypes (e.g. Data Ingestion, Cross-System Reconciliation, Screen Scraping, API Gateway). This rule inventories all archetype analytics logging activities (`Analytics_Log`, Archetype IDs).

#### Detection Logic
Scans for `<i:Analytics_Log>` activities and checks configured Archetype properties and metadata attributes.

#### Recommendation / Error Message
```text
Audit report of archetype analytics logging activities across workflows.
```

#### Remediation Guide
Ensure that archetype tracking IDs configured in project assets match the registered archetype category in the process design document (PDD).

---

### VF-026: StartEndAppMonitoringLogs

| Attribute | Details |
| :--- | :--- |
| **Rule ID** | `VF-026` |
| **Rule Name** | `StartEndAppMonitoringLogs` |
| **Fix Capability** | **Validate-Only** |
| **Implementation File** | [VF-026_StartEndAppMonitoringLogs.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-026_StartEndAppMonitoringLogs.cs) |
| **Target Scope** | All `.xaml` workflows across the project |
| **Default Severity** | `Warning` |

#### Objective & Rationale
Application Performance Monitoring (APM) in Vodafone's operational ecosystem requires dedicated telemetry to measure external application response times and downtime. Workflows interacting with enterprise systems must log application monitoring start and end events.

#### Detection Logic
Checks for the presence of IAP Application Monitoring logging activities:
- `<i:Start_App_Monitoring_Log>` and `<i:End_App_Monitoring_Log>`.

#### Recommendation / Error Message
```text
Please check that Start and End App Monitoring logs are added for application performance tracking.
```

#### Remediation Guide
Add `<i:Start_App_Monitoring_Log>` before launching or attaching to target applications, and `<i:End_App_Monitoring_Log>` upon completing the application interaction sequence.

---

### VF-029: IfElseCheck

| Attribute | Details |
| :--- | :--- |
| **Rule ID** | `VF-029` |
| **Rule Name** | `IfElseCheck` |
| **Fix Capability** | **Validate-Only** |
| **Implementation File** | [VF-029_IfElse.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-029_IfElse.cs) |
| **Target Scope** | All `.xaml` workflows across the project |
| **Default Severity** | `Warning` |

#### Objective & Rationale
Empty branches in `If` activities indicate unfinished development, dead code, or inverted logic. Leaving either `Then` or `Else` completely empty when both are expected, or creating an `If` with no activities in either branch, degrades maintainability.

#### Detection Logic
1. Inspects every `<If>` activity element.
2. Checks whether `<If.Then>` and `<If.Else>` contain executable activities.
3. Flags an warning if an `If` activity has empty or missing branch blocks.

#### Recommendation / Error Message
```text
If activity contains empty Then or Else branch. Ensure logic is handled or invert condition.
```

#### Remediation Guide
1. If the `Else` branch is unnecessary, invert the `Condition` expression (e.g. `Not (condition)`) so that logic resides in `Then`.
2. If the branch represents an unhandled state, add explicit logging or exception handling.

---

### VF-030: AddLogFields

| Attribute | Details |
| :--- | :--- |
| **Rule ID** | `VF-030` |
| **Rule Name** | `AddLogFields` |
| **Fix Capability** | **Validate-Only** |
| **Implementation File** | [VF-030_AddLogFields.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-030_AddLogFields.cs) |
| **Target Scope** | Entry workflows (`*_loader.xaml`, `*_worker.xaml`, `Main.xaml`) and `SetTransactionStatus.xaml` |
| **Default Severity** | `Error` |

#### Objective & Rationale
Vodafone centralized logging infrastructure (Splunk/Elasticsearch) requires standardized custom log fields to be attached to the robot thread context via `AddLogFields`. Without these fields, log queries and business metric dashboards fail.

#### Detection Logic
1. **Entry Workflows**: Verifies presence of `<ui:AddLogFields>` containing all 6 mandatory keys:
   - `VOISLM`, `projectName`, `processIdentifier`, `processStage`, `function`, `PPMID`.
2. **SetTransactionStatus.xaml**: Verifies presence of `<ui:AddLogFields>` containing:
   - `executionTime`, `Case Duration`, `exceptionMessage`, `ItemEndTime`, `BE_Category`, `TransactionStatus`.

#### Recommendation / Error Message
```text
Mandatory AddLogFields activity missing or missing required log fields.
```

#### Remediation Guide
Insert an `<ui:AddLogFields>` activity at the entry point of the workflow and configure all required key-value pairs matching the process configuration.

---

### VF-031: DefaultDisplayedName

| Attribute | Details |
| :--- | :--- |
| **Rule ID** | `VF-031` |
| **Rule Name** | `DefaultDisplayedName` |
| **Fix Capability** | **Validate-Only** |
| **Implementation File** | [VF-031_DefaultDisplayedName.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-031_DefaultDisplayedName.cs) |
| **Target Scope** | All `.xaml` workflows across the project |
| **Default Severity** | `Info` |

#### Objective & Rationale
Retaining UiPath's default activity display names (e.g. `"Sequence"`, `"Click"`, `"Type Into"`, `"Assign"`, `"If"`) obscures execution logs and makes debugging difficult. When an error occurs at runtime, the log reports the `DisplayName`; descriptive names pinpoint the exact failing action immediately.

#### Detection Logic
Compares `DisplayName` attributes against standard UiPath default names:
- `"Sequence"`, `"Click"`, `"Type Into"`, `"Assign"`, `"If"`, `"Delay"`, `"Get Text"`, `"Attach Window"`, etc.

#### Recommendation / Error Message
```text
Activity retains default UiPath display name. Rename to describe its functional purpose.
```

#### Remediation Guide
Update the `DisplayName` property to describe the functional intent (e.g. replace `"Click"` with `"Click Submit Order Button"`).

---

### VF-032: LogicFileUICheck

| Attribute | Details |
| :--- | :--- |
| **Rule ID** | `VF-032` |
| **Rule Name** | `LogicFileUICheck` |
| **Fix Capability** | **Validate-Only** |
| **Implementation File** | [VF-032_LogicFileUICheck.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-032_LogicFileUICheck.cs) |
| **Target Scope** | Workflows under `Logic\` and `Subprocess\` folders |
| **Default Severity** | `Error` |

#### Objective & Rationale
Enforces Vodafone's 3-tier architectural separation of concerns:
- **Automation Tier**: Directly interacts with applications and screens.
- **Logic / Subprocess Tier**: Executes pure business calculations, validations, filtering, and data transformations.
- Mixing direct UI automation inside `Logic\` workflows couples business logic to screen implementations, severely impairing maintainability and testability.

#### Detection Logic
1. Checks if the file path contains `Logic\` or `Subprocess\`.
2. Scans for UI automation activities: `Click`, `TypeInto`, `AttachWindow`, `AttachBrowser`, `OpenBrowser`, `GetValue`, `SelectItem`, `Hover`, etc.
3. Flags an `Error` if any direct UI activity is detected.

#### Recommendation / Error Message
```text
Workflows in Logic or Subprocess folders must not contain direct UI automation activities.
```

#### Remediation Guide
1. Move the UI interaction activities into a dedicated component under the `Automation\` folder.
2. In the `Logic\` workflow, invoke the new automation component via `InvokeWorkflowFile`.

---

### VF-034: Naming Convention

| Attribute | Details |
| :--- | :--- |
| **Rule ID** | `VF-034` |
| **Rule Name** | `Naming Convention` |
| **Fix Capability** | **Validate-Only** |
| **Implementation File** | [VF-034_NamingConvention.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-034_NamingConvention.cs) |
| **Target Scope** | Main workflows (`*_worker.xaml`, `*_loader.xaml`, `*_loaderworker.xaml`, `Main.xaml`) & `project.json` |
| **Default Severity** | `Error` |

#### Objective & Rationale
Enforces Vodafone Group's enterprise naming standard for all deployed processes. Process identifiers, root workflow file names, and `project.json` names must follow the standardized quadruple format: `PPMID_BusinessVertical_LocalMarket_ProcessName`.

#### Detection Logic
1. Scans main entry workflows:
   - Validates the naming pattern: `^[0-9]+_[A-Za-z0-9-]+_[A-Za-z0-9-]+_[A-Za-z0-9-_]+$`.
   - Validates that the **Business Vertical** matches recognized Vodafone verticals (`CFSL`, `CARE`, `Enterprise`, `HR`, `TES`, `Audit`, `Finance-Operations`, `SCM`, `Network-Operations`, etc.).
   - Validates that the **Local Market** matches approved market codes (`IT`, `DE`, `SPAIN`, `UK`, `VNO`, `Group`, `VOIS`, `AL`, `RO`, `CZ`, `ES`, `GR`, `SA`, `IE`, etc.).
2. Checks that the variable `strProcessIdentifier` exists and its default value matches the naming convention.
3. Checks that the `name` attribute in `project.json` matches the root workflow name.

#### Recommendation / Error Message
```text
Main workflow, strProcessIdentifier, or project.json does not adhere to Vodafone naming convention (PPMID_BusinessVertical_LocalMarket_ProcessName).
```

#### Remediation Guide
1. Rename the entry workflow and project name in `project.json` to match `PPMID_BusinessVertical_LocalMarket_ProcessName` (e.g. `12345_Care_UK_BillingExtraction`).
2. Update the default value of `strProcessIdentifier` in the main workflow.

---

### VF-035: VariablesNaming

| Attribute | Details |
| :--- | :--- |
| **Rule ID** | `VF-035` |
| **Rule Name** | `VariablesNaming` |
| **Fix Capability** | **Validate-Only** |
| **Implementation File** | [VF-035_VariablesNaming.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-035_VariablesNaming.cs) |
| **Target Scope** | All `.xaml` workflows across the project |
| **Default Severity** | `Warning` |

#### Objective & Rationale
Enforces Hungarian notation variable naming conventions across all workflows to improve readability and type safety during development. Variable names must begin with the appropriate lowercase type prefix and must not contain special characters, hyphens, or spaces.

#### Detection Logic
Inspects all `<Variable>` elements in the workflow:
- Validates that variable names start with an approved prefix matching their declared data type:
  - `String` -> `str`
  - `Int32` / `Int64` -> `int`
  - `Boolean` -> `bool`
  - `DateTime` -> `dtm`
  - `Dictionary` -> `dct`
  - `DataTable` -> `dt`
  - `DataRow` -> `dr`
  - `List` -> `lst`
  - `Array` -> `arr`
  - `QueueItem` -> `qi`
  - `UiElement` -> `ui`
- Ensures variable names contain only alphanumeric characters with no spaces or symbols.

#### Recommendation / Error Message
```text
Variable name does not adhere to Hungarian prefix naming conventions or contains invalid characters.
```

#### Remediation Guide
Rename the variable in UiPath Studio's Variables panel to include the correct type prefix (e.g. rename `customerName` to `strCustomerName`).

---

### VF-036: MicrosoftOfficeActivities

| Attribute | Details |
| :--- | :--- |
| **Rule ID** | `VF-036` |
| **Rule Name** | `MicrosoftOfficeActivities` |
| **Fix Capability** | **Validate-Only** |
| **Implementation File** | [VF-036_MicrosoftOfficeActivitiesRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-036_MicrosoftOfficeActivitiesRule.cs) |
| **Target Scope** | All `.xaml` workflows across the project |
| **Default Severity** | `Info` |

#### Objective & Rationale
Using desktop Microsoft Office activities (e.g. `ExcelApplicationScope`, `WordApplicationScope`) requires Microsoft Office desktop licenses and installed software on the robot VM. Whenever possible, developers should prefer background workbook activities (`ReadRange`, `WriteRange` under `UiPath.Excel.Activities.Workbook`) or Microsoft 365 / Graph API integrations.

#### Detection Logic
Scans for activities requiring desktop Office:
- `ExcelApplicationScope`, `WordApplicationScope`, `ExecuteMacro`, `ExportToPDF`.

#### Recommendation / Error Message
```text
Desktop Office activity detected. Verify that Office is licensed on the robot VM, or prefer Workbook/M365 activities.
```

#### Remediation Guide
If COM interaction or macros are not strictly required, replace `ExcelApplicationScope` with `UiPath.Excel.Activities.Workbook.ReadRangeWorkbook` and `WriteRangeWorkbook`.

---

### VF-037: UnusedArguments

| Attribute | Details |
| :--- | :--- |
| **Rule ID** | `VF-037` |
| **Rule Name** | `UnusedArguments` |
| **Fix Capability** | **Validate-Only** |
| **Implementation File** | [VF-037_UnusedArguments.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-037_UnusedArguments.cs) |
| **Target Scope** | All `.xaml` workflows across the project |
| **Default Severity** | `Warning` |

#### Objective & Rationale
Identifies arguments declared in a workflow's `<x:Members>` collection that are never referenced in any activity expression, condition, or property inside the workflow body. *(Companion rule to Autofix rule VF-019).*

#### Detection Logic
1. Reads all arguments from `<x:Members>`.
2. Inspects all activity expressions in the workflow body using token boundary matching.
3. Flags any argument name that never appears in the body.

#### Recommendation / Error Message
```text
Argument is declared but never referenced in workflow body.
```

#### Remediation Guide
Delete the unused argument from UiPath Studio's Arguments panel, or execute Autofix rule `VF-019` to remove all unused variables and arguments in batch.

---

### VF-038: Branches Logging (Validate-Only)

| Attribute | Details |
| :--- | :--- |
| **Rule ID** | `VF-038` |
| **Rule Name** | `Branches Logging` |
| **Fix Capability** | **Validate-Only** |
| **Implementation File** | [VF-038_BranchesLogMessages.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-038_BranchesLogMessages.cs) |
| **Target Scope** | All `.xaml` workflows containing conditional logic or loops |
| **Default Severity** | `Warning` |

#### Objective & Rationale
Ensures that all branching paths and iteration loops begin with a descriptive logging statement. Applies to sequence-based `If` activities, `Switch` activities, `FlowDecision`, `FlowSwitch`, and loops (`While`, `DoWhile`, `ForEach`, `ForEachRow`).

#### Detection Logic
Inspects each branch container:
- Checks if the first executable activity inside the branch is an `<i:Info_Log>`, `<i:Message_Log>`, or `<ui:LogMessage>`.

#### Recommendation / Error Message
```text
Branch or loop sequence does not start with a log activity.
```

#### Remediation Guide
Insert an `Info_Log` or `LogMessage` at the start of each `Then`, `Else`, `Case`, or loop body sequence.

---

### VF-039: PreAndPostConditions

| Attribute | Details |
| :--- | :--- |
| **Rule ID** | `VF-039` |
| **Rule Name** | `PreAndPostConditions` |
| **Fix Capability** | **Validate-Only** |
| **Implementation File** | [VF-039_PreAndPostConditions.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-039_PreAndPostConditions.cs) |
| **Target Scope** | Workflows under `Automation\` and `Read\` folders |
| **Default Severity** | `Error` |

#### Objective & Rationale
Vodafone automation components must be deterministic. To guarantee an application is in the expected state before interacting with it, and to verify the operation succeeded afterward, every `Automation\` workflow must include explicit Pre-Condition and Post-Condition validation sequences. `Read` workflows require Pre-Condition validation.

#### Detection Logic
1. **Automation workflows**: Verifies the presence of both a **Pre-Condition** sequence and a **Post-Condition** sequence.
2. **Read workflows**: Verifies the presence of a **Pre-Condition** sequence.
3. Each condition sequence must contain dynamic element synchronization (e.g. `UiElementExists`, `WaitUiElementAppear`, `FindElement`) followed by an `If` activity throwing a `SystemException` if the condition fails.

#### Recommendation / Error Message
```text
Automation workflow must contain both Pre-Condition and Post-Condition sequences with dynamic element checks and conditional throws.
```

#### Remediation Guide
1. Add a Sequence named `"Pre-Condition"` at the start of the workflow containing `UiElementExists` and an `If` activity throwing `SystemException("Pre-Condition failed")`.
2. Add a Sequence named `"Post-Condition"` at the end containing similar verification logic.

---

### VF-040: Activities Number

| Attribute | Details |
| :--- | :--- |
| **Rule ID** | `VF-040` |
| **Rule Name** | `Activities Number` |
| **Fix Capability** | **Validate-Only** |
| **Implementation File** | [VF-040_NonUIActivitiesNum.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-040_NonUIActivitiesNum.cs) |
| **Target Scope** | Workflows under `Logic\` and `Subprocess\` folders |
| **Default Severity** | `Warning` |

#### Objective & Rationale
Monolithic workflows with excessive activity counts become unmaintainable, difficult to debug, and prone to compiler performance degradation. Vodafone standards cap the total activity count in `Logic` and `Subprocess` workflows at **100 activities**.

#### Detection Logic
1. Recursively counts all activity elements in the workflow tree (excluding variables, arguments, and metadata wrappers).
2. Flags a warning if the activity count exceeds 100.

#### Recommendation / Error Message
```text
Workflow exceeds maximum allowed activity threshold (100 activities). Refactor into modular sub-workflows.
```

#### Remediation Guide
Extract cohesive subsets of logic (e.g. data validation, calculation, filtering) into dedicated sub-workflows and invoke them via `InvokeWorkflowFile`.

---

### VF-041: BEInsideAutomation

| Attribute | Details |
| :--- | :--- |
| **Rule ID** | `VF-041` |
| **Rule Name** | `BEInsideAutomation` |
| **Fix Capability** | **Validate-Only** |
| **Implementation File** | [VF-041_BEInsideAutomation.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-041_BEInsideAutomation.cs) |
| **Target Scope** | Workflows under `Automation\` folder |
| **Default Severity** | `Error` |

#### Objective & Rationale
Components in the `Automation\` tier must only handle technical UI/Application automation. They must never make business decisions or throw `BusinessRuleException`. If an application element or business datum is missing, the automation component must throw a technical `SystemException` or return a status flag; business exception logic belongs strictly in the `Logic\` tier.

#### Detection Logic
1. Checks if the workflow resides under `Automation\`.
2. Scans for `<Throw>` activities throwing `ui:BusinessRuleException`.
3. Flags an error if found.

#### Recommendation / Error Message
```text
BusinessRuleException must not be thrown inside Automation workflows. Use status output flags or throw SystemException.
```

#### Remediation Guide
Replace the `BusinessRuleException` throw with a boolean output argument (e.g. `out_boolRecordFound`), and throw the `BusinessRuleException` within the parent `Logic` or `Subprocess` workflow.

---

### VF-042: Multiple Apps Sequence

| Attribute | Details |
| :--- | :--- |
| **Rule ID** | `VF-042` |
| **Rule Name** | `Multiple Apps Sequence` |
| **Fix Capability** | **Validate-Only** |
| **Implementation File** | [VF-042_MultipleAppsSequence.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-042_MultipleAppsSequence.cs) |
| **Target Scope** | Workflows under `Automation\` folder |
| **Default Severity** | `Error` |

#### Objective & Rationale
Enforces single-responsibility modularity: a single workflow in the `Automation\` folder must interact with only **one target application or screen**. Interacting with multiple applications (e.g. attaching to SAP and then opening Chrome within the same workflow) tightly couples systems and violates modular architecture.

#### Detection Logic
1. Counts the number of application scope activities: `OpenBrowser`, `AttachBrowser`, `OpenApplication`, `AttachWindow`.
2. Flags an error if more than one distinct application scope is present in an Automation workflow.

#### Recommendation / Error Message
```text
Automation workflow must not attach to or interact with more than one target application or screen.
```

#### Remediation Guide
Split the workflow into separate modular files (e.g. `SAP_EnterOrder.xaml` and `Chrome_UploadOrderDoc.xaml`).

---

### VF-043: Nested TryCatch

| Attribute | Details |
| :--- | :--- |
| **Rule ID** | `VF-043` |
| **Rule Name** | `Nested TryCatch` |
| **Fix Capability** | **Validate-Only** |
| **Implementation File** | [VF-043_AutomationNestedTryCatch.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-043_AutomationNestedTryCatch.cs) |
| **Target Scope** | Workflows under `Automation\` folder |
| **Default Severity** | `Warning` |

#### Objective & Rationale
Nesting `TryCatch` activities within Automation workflows conceals root-cause application failures, creates unpredictable error bubbling, and conflicts with outer retry scopes.

#### Detection Logic
Scans for any `<TryCatch>` activity that resides as a child or descendant inside another `<TryCatch>` activity within `Automation\` workflows.

#### Recommendation / Error Message
```text
Nested TryCatch detected in Automation workflow. Remove nested TryCatch blocks.
```

#### Remediation Guide
Remove inner `TryCatch` blocks and allow exceptions to be handled by the top-level workflow `TryCatch` and outer retry scope.

---

### VF-044: HardCoded TimeOut

| Attribute | Details |
| :--- | :--- |
| **Rule ID** | `VF-044` |
| **Rule Name** | `HardCoded TimeOut` |
| **Fix Capability** | **Validate-Only** |
| **Implementation File** | [VF-044_HardCodedTimeOut.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-044_HardCodedTimeOut.cs) |
| **Target Scope** | All `.xaml` workflows containing UI activities |
| **Default Severity** | `Warning` |

#### Objective & Rationale
Hardcoding numeric milliseconds in `TimeoutMS` properties (e.g. `30000`, `5000`) prevents adjusting timeouts across different environments (e.g. slow QA servers vs fast Prod infrastructure) without modifying code.

#### Detection Logic
1. Inspects `TimeoutMS` attributes on UI activities (`Click`, `TypeInto`, `ElementExists`, `WaitUiElementAppear`, etc.).
2. Flags activities where `TimeoutMS` is assigned a literal numeric integer rather than a Config lookup or argument (`CInt(in_Config("TimeoutShort"))`).

#### Recommendation / Error Message
```text
Hardcoded TimeoutMS value detected. Bind timeout properties to Config variables.
```

#### Remediation Guide
Bind the `TimeoutMS` property to a Config key, e.g. `CInt(in_Config("TimeoutMedium"))`.

---

### VF-045: HardCodedDelays

| Attribute | Details |
| :--- | :--- |
| **Rule ID** | `VF-045` |
| **Rule Name** | `HardCodedDelays` |
| **Fix Capability** | **Validate-Only** |
| **Implementation File** | [VF-045_HardCodedDelaysRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-045_HardCodedDelaysRule.cs) |
| **Target Scope** | All `.xaml` workflows containing UI activities |
| **Default Severity** | `Warning` |

#### Objective & Rationale
Similar to `VF-044`, hardcoding numeric values in `DelayBefore` or `DelayAfter` properties (e.g. `300`, `1000`) creates hidden delays that degrade performance and cannot be tuned via configuration.

#### Detection Logic
Scans `DelayBefore` and `DelayAfter` attributes on UI activities and flags literal numeric constants.

#### Recommendation / Error Message
```text
Hardcoded DelayBefore or DelayAfter property detected. Use Config variables.
```

#### Remediation Guide
Bind the delay property to a Config parameter or set it to `0` if dynamic element synchronization is sufficient.

---

### VF-046: Folder Structure

| Attribute | Details |
| :--- | :--- |
| **Rule ID** | `VF-046` |
| **Rule Name** | `Folder Structure` |
| **Fix Capability** | **Validate-Only** |
| **Implementation File** | [VF-046_FolderStructure.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-046_FolderStructure.cs) |
| **Target Scope** | All `.xaml` workflow files project-wide |
| **Default Severity** | `Error` |

#### Objective & Rationale
Enforces Vodafone standard folder organization across the project repository. All workflows must be structured into designated subdirectories according to their technical function:
- `Navigate\`
- `Read\`
- `Write\`
- `Logic\`
- `Subprocess\`
- `Other\`
- `Testing\`
- *(Entry workflows `Main.xaml`, `*_worker.xaml`, `*_loader.xaml`, and `*_loaderworker.xaml` are exempted.)*

#### Detection Logic
1. Checks the parent directory path of every `.xaml` file.
2. Flags files residing directly in the project root or in non-standard folders.

#### Recommendation / Error Message
```text
Workflow file resides in a non-standard folder. Organize files into approved folders: Navigate, Read, Write, Logic, Subprocess, Other, Testing.
```

#### Remediation Guide
Move the workflow file into the appropriate recognized subfolder in UiPath Studio's Project panel.

---

### VF-047: Nested IFs

| Attribute | Details |
| :--- | :--- |
| **Rule ID** | `VF-047` |
| **Rule Name** | `Nested IFs` |
| **Fix Capability** | **Validate-Only** |
| **Implementation File** | [VF-047_NestedIfsRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-047_NestedIfsRule.cs) |
| **Target Scope** | All `.xaml` workflows across the project |
| **Default Severity** | `Warning` |

#### Objective & Rationale
Deeply nested `If` activities (nested 3 or more levels deep) create cyclomatic complexity, drastically increase cognitive load during maintenance, and make unit testing difficult. Complex multi-branch logic should be structured as Flowcharts or State Machines.

#### Detection Logic
Recursively calculates the nesting depth of `<If>` activities. Flags any `If` activity nested 3 or more levels inside parent `If` activities.

#### Recommendation / Error Message
```text
Deeply nested If activities detected (>= 3 levels). Refactor logic using a Flowchart or Switch activity.
```

#### Remediation Guide
Refactor the nested sequence into a `Flowchart` with `FlowDecision` nodes or use a `Switch` activity.

---

### VF-048: OrechestratorActivitesRetry

| Attribute | Details |
| :--- | :--- |
| **Rule ID** | `VF-048` |
| **Rule Name** | `OrechestratorActivitesRetry` |
| **Fix Capability** | **Validate-Only** |
| **Implementation File** | [VF-048_OrechestratorActivitesRetry.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-048_OrechestratorActivitesRetry.cs) |
| **Target Scope** | All `.xaml` workflows containing Orchestrator activities |
| **Default Severity** | `Error` |

#### Objective & Rationale
Orchestrator API calls communicate over HTTPS and are susceptible to brief network blips, TLS handshakes, or server throttling. To ensure resilience, all Orchestrator queue activities must be wrapped in a `RetryScope`.

#### Detection Logic
1. Identifies Orchestrator queue activities:
   - `AddQueueItem`, `AddTransactionItem`, `BulkAddQueueItems`, `DeleteQueueItems`, `GetQueueItems`, `GetQueueItem`, `PostponeTransactionItem`, `SetTransactionProgress`, `SetTransactionStatus`.
2. Checks whether the activity is wrapped inside an ancestor `<ui:RetryScope>`.

#### Recommendation / Error Message
```text
Orchestrator queue activity must be wrapped inside a RetryScope.
```

#### Remediation Guide
Wrap the Orchestrator activity inside a `RetryScope` configured with 3 retries and a 5-second interval.

---

### VF-049: InputValidation

| Attribute | Details |
| :--- | :--- |
| **Rule ID** | `VF-049` |
| **Rule Name** | `InputValidation` |
| **Fix Capability** | **Validate-Only** |
| **Implementation File** | [VF-049_ValidateInput.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-049_ValidateInput.cs) |
| **Target Scope** | Entry workflows (`*_loader.xaml`, `*_worker.xaml`) |
| **Default Severity** | `Error` |

#### Objective & Rationale
To prevent processes from failing midway through execution due to missing input files, network share permissions, or corrupted templates, all entry workflows must invoke `IAP_ValidateInputFiles.xaml` at startup with all input arguments properly populated.

#### Detection Logic
1. Inspects entry workflows for an `InvokeWorkflowFile` referencing `IAP_ValidateInputFiles.xaml`.
2. Validates that arguments passed to `IAP_ValidateInputFiles.xaml` are not null or empty.

#### Recommendation / Error Message
```text
Entry workflow must invoke IAP_ValidateInputFiles.xaml with all required arguments mapped.
```

#### Remediation Guide
Add an `InvokeWorkflowFile` targeting `Logic\IAP_ValidateInputFiles.xaml` in the initialization sequence and map all file path arguments.

---

### VF-050: WQ Duplication Check

| Attribute | Details |
| :--- | :--- |
| **Rule ID** | `VF-050` |
| **Rule Name** | `WQ Duplication Check` |
| **Fix Capability** | **Validate-Only** |
| **Implementation File** | [VF-050_WQDuplicates.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-050_WQDuplicates.cs) |
| **Target Scope** | Queue loader workflows (`UploadItems.xaml`) |
| **Default Severity** | `Error` |

#### Objective & Rationale
Uploading duplicate items to Orchestrator Work Queues causes duplicate transactions, double payments, or contradictory customer communications. Loader workflows must verify item uniqueness prior to upload.

#### Detection Logic
1. Inspects `UploadItems.xaml` workflows.
2. Checks for an `InvokeWorkflowFile` calling `Logic\IAP_CheckReferenceinQueueItems.xaml` before any `AddQueueItem` activity.

#### Recommendation / Error Message
```text
UploadItems workflow must invoke Logic\IAP_CheckReferenceinQueueItems.xaml to verify duplicate queue references.
```

#### Remediation Guide
Invoke `Logic\IAP_CheckReferenceinQueueItems.xaml` before adding items to the queue, and only proceed if the reference does not already exist.

---

### VF-051: Args Naming Convention

| Attribute | Details |
| :--- | :--- |
| **Rule ID** | `VF-051` |
| **Rule Name** | `Args Naming Convention` |
| **Fix Capability** | **Validate-Only** |
| **Implementation File** | [VF-051_ArgsNamingConvention.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-051_ArgsNamingConvention.cs) |
| **Target Scope** | All `.xaml` workflows across the project |
| **Default Severity** | `Warning` |

#### Objective & Rationale
Enforces directional and type prefixes for all workflow arguments:
- Direction prefixes: `in_`, `out_`, `io_` (or `inout_`)
- Type prefixes: `str`, `int`, `bool`, `dtm`, `dct`, `dt`, `dr`, `lst`, `arr`, `qi`, `ui`
- No spaces or special characters.

#### Detection Logic
Inspects all `<x:Property>` elements in `<x:Members>` and validates naming format against `^(in|out|io|inout)_[a-z]{2,4}[A-Z0-9][A-Za-z0-9]*$`.

#### Recommendation / Error Message
```text
Argument name does not adhere to directional and Hungarian type prefix conventions (e.g. in_strFilePath).
```

#### Remediation Guide
Rename arguments in UiPath Studio's Arguments panel (e.g. rename `filePath` to `in_strFilePath`).

---

### VF-052: ResetPasswordCheck

| Attribute | Details |
| :--- | :--- |
| **Rule ID** | `VF-052` |
| **Rule Name** | `ResetPasswordCheck` |
| **Fix Capability** | **Validate-Only** |
| **Implementation File** | [VF-052_ResetPasswordCheck.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-052_ResetPasswordCheck.cs) |
| **Target Scope** | Workflows named `InitialiseApplications.xaml` |
| **Default Severity** | `Error` |

#### Objective & Rationale
When application passwords expire or become locked, robots must detect credential invalidity gracefully during initialization rather than repeatedly failing transactions. `InitialiseApplications.xaml` must invoke `IAP_ValidateApplicationsCredentials` to verify credentials.

#### Detection Logic
Inspects `InitialiseApplications.xaml` workflows and verifies an `InvokeWorkflowFile` targeting `IAP_ValidateApplicationsCredentials`.

#### Recommendation / Error Message
```text
InitialiseApplications workflow must invoke IAP_ValidateApplicationsCredentials to validate credentials.
```

#### Remediation Guide
Add an `InvokeWorkflowFile` activity calling `Logic\IAP_ValidateApplicationsCredentials.xaml` during application launch.

---

### VF-057: static Selector

| Attribute | Details |
| :--- | :--- |
| **Rule ID** | `VF-057` |
| **Rule Name** | `static Selector` |
| **Fix Capability** | **Validate-Only** |
| **Implementation File** | [VF-057_SelectorAttributeValueFromXaml.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-057_SelectorAttributeValueFromXaml.cs) |
| **Target Scope** | All `.xaml` workflows containing UI activities |
| **Default Severity** | `Warning` |

#### Objective & Rationale
Consolidated from legacy rules `VF-057` and `VF-061`. Dynamic window titles and web page names frequently include variable document IDs, dates, or version numbers (e.g. `"Invoice #12345 - SAP GUI"`). Hardcoded static strings in `title`, `name`, or `aaname` cause selectors to break when data changes. These attributes must include wildcards (`*`, `?`, `#`) to accommodate dynamic values, but must not consist entirely of wildcards.

#### Detection Logic
1. Parses selector XML strings across all UI interaction activities.
2. Inspects attributes: `title`, `name`, `aaname`.
3. Flags attributes where:
   - The value is completely static without wildcards where dynamic text is expected.
   - OR the value consists solely of wildcards (e.g. `title='*'` or `name='?'`).

#### Recommendation / Error Message
```text
Selector title, name, or aaname attribute should use wildcards (*, ?) for dynamic portions, but must not be entirely wildcard.
```

#### Remediation Guide
Open the selector in UiPath Studio's Selector Editor, replace dynamic text (e.g. invoice numbers or timestamps) with an asterisk wildcard `*`, or bind it to a variable: `title='Invoice * - SAP GUI'`.

---

### VF-058: prohibited SetTransactionStatus

| Attribute | Details |
| :--- | :--- |
| **Rule ID** | `VF-058` |
| **Rule Name** | `prohibited SetTransactionStatus` |
| **Fix Capability** | **Validate-Only** |
| **Implementation File** | [VF-058_SetTransactionStatusOnlyInHandoff.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-058_SetTransactionStatusOnlyInHandoff.cs) |
| **Target Scope** | Project-wide |
| **Default Severity** | `Error` |

#### Objective & Rationale
In Vodafone's enhanced ReFramework architecture, updating the Orchestrator queue item status via `SetTransactionStatus` must be strictly centralized. Calling `SetTransactionStatus` from arbitrary business logic workflows causes premature transaction termination and corrupts ReFramework retry state. It is permitted only inside `SetTransactionStatus.xaml`, and `SetTransactionStatus.xaml` must be invoked strictly within the `Handoff` state.

#### Detection Logic
1. Checks that `<ui:SetTransactionStatus>` activity is used **only** inside `SetTransactionStatus.xaml`.
2. Checks that `SetTransactionStatus.xaml` is invoked **only** inside the `Handoff` state of the Main state machine.

#### Recommendation / Error Message
```text
SetTransactionStatus is only permitted inside SetTransactionStatus.xaml, and SetTransactionStatus.xaml must only be invoked in Handoff state.
```

#### Remediation Guide
Remove any ad-hoc `SetTransactionStatus` calls from business workflows; allow ReFramework's `Handoff` state to set transaction success, business exception, or system exception.

---

### VF-060: SelectorAttribute

| Attribute | Details |
| :--- | :--- |
| **Rule ID** | `VF-060` |
| **Rule Name** | `SelectorAttribute` |
| **Fix Capability** | **Validate-Only** |
| **Implementation File** | [VF-060_SelectorAttributeRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-060_SelectorAttributeRule.cs) |
| **Target Scope** | All `.xaml` workflows containing UI selectors |
| **Default Severity** | `Warning` |

#### Objective & Rationale
Consolidated from legacy rules `VF-056`, `VF-060`, and `VF-063`. Fragile and dynamic selector attributes (such as ordinal indexes, internal Windows control IDs, or session IDs) change unpredictably between sessions, DOM updates, and virtual environments. Selectors relying on these attributes fail frequently in production.

#### Detection Logic
Inspects all selector XML strings for prohibited fragile attributes:
- `idx`
- `ctrlid`
- `sessionid` / `session_id`
- `processid` / `process_id`
- `dynamicid` / `dynamic_id`
- `runtimeid` / `runtime_id`

#### Recommendation / Error Message
```text
Fragile selector attribute detected (idx, ctrlid, sessionid, etc.). Use stable visual anchors or robust UI attributes.
```

#### Remediation Guide
1. Open the Selector Editor in UiPath Studio.
2. Uncheck fragile attributes (`idx`, `ctrlid`).
3. Select stable semantic attributes (`automationid`, `name`, `parentid`) or anchor the element to a stable nearby label using `Anchor Base` or modern Fuzzy Selectors.

---

### VF-073: Config Constants Usage

| Attribute | Details |
| :--- | :--- |
| **Rule ID** | `VF-073` |
| **Rule Name** | `Config Constants Usage` |
| **Fix Capability** | **Validate-Only** |
| **Implementation File** | [VF-073_ConfigConstantsUsageRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-073_ConfigConstantsUsageRule.cs) |
| **Target Scope** | All `.xaml` workflows and project `Config.xlsx` |
| **Default Severity** | `Error` |

#### Objective & Rationale
In ReFramework projects, configuration constants and robot texts are retrieved from central dictionary collections (`in_Config("KeyName")`, `dctConfig("KeyName")`, `dctroboText("KeyName")`). If a developer typos a key name in XAML that does not exist in the project's `Config.xlsx` spreadsheet (under Settings, Constants, or Assets sheets), the process will fail in production with a `KeyNotFoundException`.

#### Detection Logic
1. Locates and parses the project's configuration Excel file (`Data\Config.xlsx`).
2. Extracts all declared configuration keys across all sheets.
3. Scans all activity expressions across all project workflows for dictionary lookups:
   - `in_Config("...")`, `Config("...")`, `dctConfig("...")`, `dctroboText("...")`.
4. Cross-references every lookup key against the Excel master list.
5. Flags an `Error` for any key referenced in XAML that is missing from `Config.xlsx`.

#### Recommendation / Error Message
```text
Config key referenced in XAML expression does not exist in Config.xlsx sheets.
```

#### Remediation Guide
1. Check the spelling of the key in the workflow expression.
2. Open `Data\Config.xlsx` and add the missing key and its value under the `Settings` or `Constants` sheet.
