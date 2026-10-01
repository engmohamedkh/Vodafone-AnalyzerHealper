# Vodafone Rules Statistics & Coverage Report

> **Generated from**: [new rules.md](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/new%20rules.md) · [old rules.md](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/old%20rules.md) · [rules comparison.md](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/rules%20comparison.md)

---

## 1. At-a-Glance Statistics

| Statistic | Value |
| :--- | :---: |
| **Legacy Rules (Old Suite)** | 49 |
| **Modern Rules (AnalyzerHelper)** | **54** |
| **Legacy Rules Fully Ported** | **49 / 49 (100%)** |
| **Modern Rules with Autofix** | 10 |
| **Modern Rules with Interactive Fix** | 5 |
| **Modern Rules — Validate Only** | 39 |
| **New Rules Unique to AnalyzerHelper** | 8 |
| **Old Metadata-Only Aliases (not real rules)** | 4 (`VF-017`, `VF-062`, `VF-063`, `VF-064`) |
| **Legacy Rules Merged/Consolidated in Modern** | 5 (`VF-054→VF-012`, `VF-055→VF-012`, `VF-056→VF-060`, `VF-061→VF-057`, `VF-063→VF-060`) |

---

## 2. Rule Count Breakdown

### 2.1 Modern AnalyzerHelper — By Fix Capability

| Fix Capability | Count | Rule IDs |
| :--- | :---: | :--- |
| **Autofix** (non-interactive, instant) | 10 | `VF-008`, `VF-012`, `VF-013`, `VF-019`, `VF-020`, `VF-021`, `VF-028`, `VF-053`, `VF-071`, `VF-072` |
| **Interactive Fix** (batch dialogs / user-assisted) | 5 | `VF-018`, `VF-027`, `VF-033`, `VF-065`, `VF-070` |
| **Validate-Only** (report / flag, no code change) | 39 | `VF-002`, `VF-003`, `VF-005`, `VF-009`, `VF-010`, `VF-014`, `VF-015`, `VF-016`, `VF-022`, `VF-024`, `VF-025`, `VF-026`, `VF-029`, `VF-030`, `VF-031`, `VF-032`, `VF-034`, `VF-035`, `VF-036`, `VF-037`, `VF-038`, `VF-039`, `VF-040`, `VF-041`, `VF-042`, `VF-043`, `VF-044`, `VF-045`, `VF-046`, `VF-047`, `VF-048`, `VF-049`, `VF-050`, `VF-051`, `VF-052`, `VF-057`, `VF-058`, `VF-060`, `VF-073` |
| **Total** | **54** | |

### 2.2 Legacy Analyzer — By Category

| Category | Count | Notes |
| :--- | :---: | :--- |
| **Activity Rules** | 20 | Rules inspecting individual activity properties |
| **Workflow Rules** | 28 | Rules inspecting whole-workflow structure |
| **Shared (Activity & Workflow)** | 1 | `VF-038` (BranchesLogMessages) |
| **Total** | **49** | Excluding metadata-only aliases |

---

## 3. Modern Rule → Covered Legacy Rules Mapping

Each row shows the **new AnalyzerHelper rule** and all **legacy rules it covers or replaces**.

| # | New Rule ID | New Rule Name | New File | Covers Old Rule(s) | Old Rule Name(s) | Old File(s) | Relationship |
| :-: | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| 1 | **VF-002** | `ImageBasedActivities` | [VF-002_ImageBasedActivitiesRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-002_ImageBasedActivitiesRule.cs) | **VF-002** | `ImageBasedActivities` | [ImageBasedActivities.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/VodafoneActivitiesCustomRules/ImageBasedActivities.cs) | Exact Match |
| 2 | **VF-003** | `UndocumentedDelay` | [VF-003_UndocumentedDelayRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-003_UndocumentedDelayRule.cs) | **VF-003** | `UndocumentedDelay` | [UndocumentedDelay.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/VodafoneActivitiesCustomRules/UndocumentedDelay.cs) | Exact Match |
| 3 | **VF-005** | `HardCodedPasswords` | [VF-005_HardCodedPasswordsRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-005_HardCodedPasswordsRule.cs) | **VF-005** · **VF-017** *(alias)* | `HardCodedPasswords` | [HardCodedPasswords.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/VodafoneActivitiesCustomRules/HardCodedPasswords.cs) | Exact Match + Alias |
| 4 | **VF-008** | `Log Browser URL` | [VF-008_LogBrowserUrlRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-008_LogBrowserUrlRule.cs) | **VF-008** | `Log Browser URL` | [LogBrowserURL.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/VodafoneActivitiesCustomRules/LogBrowserURL.cs) | Enhanced + **Autofix** |
| 5 | **VF-009** | `StartEndLogs` | [VF-009_StartEndLogs.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-009_StartEndLogs.cs) | **VF-009** | `StartEndLogs` | [StartEndLogs.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/VodafoneWorkFlowsCustomRules/StartEndLogs.cs) | Exact Match |
| 6 | **VF-010** | `SimulateAndSendWindowMessage` | [VF-010_SimulateAndSendWindowMessage.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-010_SimulateAndSendWindowMessage.cs) | **VF-010** | `SimulateAndSendWindowMessage` | [SimulateAndSendWindowMessage.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/VodafoneActivitiesCustomRules/SimulateAndSendWindowMessage.cs) | Exact Match |
| 7 | **VF-012** | `Missing Catch Block Actions` | [VF-012_TryCatchRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-012_TryCatchRule.cs) | **VF-012** · **VF-054** · **VF-055** | `TryCatch` · `ActivityCounterRule` · `RethrowRule` | [TryCatchRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/VodafoneWorkFlowsCustomRules/TryCatchRule.cs) · [CatchBlock.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/VodafoneWorkFlowsCustomRules/CatchBlock.cs) · [RethrowRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/VodafoneWorkFlowsCustomRules/RethrowRule.cs) | **Consolidated** 3→1 + **Autofix** |
| 8 | **VF-013** | `Remove Default Values` | [VF-013_RemoveDefaultsVarArgRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-013_RemoveDefaultsVarArgRule.cs) | *(none)* | — | — | **New in AnalyzerHelper** |
| 9 | **VF-014** | `MissingInOutArgument` | [VF-014_MissingInOutArgument.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-014_MissingInOutArgument.cs) | **VF-014** | `MissingInOutArgument` | [MissingInOutArgument.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/VodafoneActivitiesCustomRules/MissingInOutArgument.cs) | Exact Match |
| 10 | **VF-015** | `BusinessSystemException` | [VF-015_BusinessSystemExceptionRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-015_BusinessSystemExceptionRule.cs) | **VF-015** | `BusinessSystemException` | [SystemAndBusinessExceptions.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/VodafoneActivitiesCustomRules/SystemAndBusinessExceptions.cs) | Exact Match |
| 11 | **VF-016** | `ProhibitedActivities` | [VF-016_ProhibitedActivitiesRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-016_ProhibitedActivitiesRule.cs) | **VF-016** | `ProhibtedActivity` | [ProhibtedActivity.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/VodafoneActivitiesCustomRules/ProhibtedActivity.cs) | Exact Match |
| 12 | **VF-018** | `Branches Logging (Interactive)` | [VF-018_BranchesLogsRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-018_BranchesLogsRule.cs) | **VF-038** *(old ID)* | `Branches Logging` | [BranchesLogMessages.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/VodafoneActivitiesCustomRules/BranchesLogMessages.cs) · [WorflowsBranchesLogMessages.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/VodafoneWorkFlowsCustomRules/WorflowsBranchesLogMessages.cs) | **ID Reassigned** + **Interactive Fix** |
| 13 | **VF-019** | `Remove Unused Variables/Arguments` | [VF-019_RemoveUnusedVarArgRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-019_RemoveUnusedVarArgRule.cs) | *(none)* | — | — | **New in AnalyzerHelper** |
| 14 | **VF-020** | `SyncInvokeArguments` | [VF-020_SyncInvokeArgumentsRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-020_SyncInvokeArgumentsRule.cs) | *(none — evolved from VF-014)* | — | — | **New in AnalyzerHelper** |
| 15 | **VF-021** | `FixComponentName` | [VF-021_FixComponentNameRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-021_FixComponentNameRule.cs) | **VF-021** | `ValidateWorkFlowsName` | [ValidateWorkFlowsName.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/VodafoneWorkFlowsCustomRules/ValidateWorkFlowsName.cs) | **Separated** + **Autofix** |
| 16 | **VF-022** | `RetryScope` | [VF-022_RetryScopeRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-022_RetryScopeRule.cs) | **VF-022** | `RetryScope` | [RetryScope.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/VodafoneWorkFlowsCustomRules/RetryScope.cs) | Exact Match |
| 17 | **VF-024** | `WorkqueueEncryption` | [VF-024_WorkqueueEncryptionRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-024_WorkqueueEncryptionRule.cs) | **VF-024** | `WorkqueueEncryption` | [WorkqueueEncryption.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/VodafoneActivitiesCustomRules/WorkqueueEncryption.cs) | Exact Match |
| 18 | **VF-025** | `Archtypes` | [VF-025_ArchtypesRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-025_ArchtypesRule.cs) | **VF-025** | `Archtypes` | [ArchetypeList.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/VodafoneActivitiesCustomRules/ArchetypeList.cs) | Exact Match |
| 19 | **VF-026** | `StartEndAppMonitoringLogs` | [VF-026_StartEndAppMonitoringLogs.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-026_StartEndAppMonitoringLogs.cs) | **VF-026** | `StartEndAppMonitoringLogs` | [StartEndAppMonitoringLogs.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/VodafoneWorkFlowsCustomRules/StartEndAppMonitoringLogs.cs) | Exact Match |
| 20 | **VF-027** | `HandleCommentedActivities` | [VF-027_HandleCommentedActivitiesRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-027_HandleCommentedActivitiesRule.cs) | **VF-027** | `CommentOutActivity` | [CommentOut.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/VodafoneActivitiesCustomRules/CommentOut.cs) | Enhanced + **Interactive Fix** |
| 21 | **VF-028** | `HardcodedArguments` | [VF-028_HardcodedArguments.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-028_HardcodedArguments.cs) | **VF-028** | `HardcodedArguments` | [HardcodedArguments.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/VodafoneWorkFlowsCustomRules/HardcodedArguments.cs) | Enhanced + **Autofix** |
| 22 | **VF-029** | `IfElseCheck` | [VF-029_IfElse.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-029_IfElse.cs) | **VF-029** | `IfElseCheck` | [IfElse.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/VodafoneActivitiesCustomRules/IfElse.cs) | Enhanced (branch detail) |
| 23 | **VF-030** | `AddLogFields` | [VF-030_AddLogFields.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-030_AddLogFields.cs) | **VF-030** | `AddLogFields` | [AddLogFields.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/VodafoneWorkFlowsCustomRules/AddLogFields.cs) | Exact Match |
| 24 | **VF-031** | `DefaultDisplayedName` | [VF-031_DefaultDisplayedName.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-031_DefaultDisplayedName.cs) | **VF-031** | `DefaultDisplayedName` | [DefaultDisplayedName.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/VodafoneActivitiesCustomRules/DefaultDisplayedName.cs) | Exact Match |
| 25 | **VF-032** | `LogicFileUICheck` | [VF-032_LogicFileUICheck.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-032_LogicFileUICheck.cs) | **VF-032** | `LogicFileUICheck` | [LogicFileUICheck.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/VodafoneWorkFlowsCustomRules/LogicFileUICheck.cs) | Exact Match |
| 26 | **VF-033** | `Annotations` | [VF-033_AnnotationRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-033_AnnotationRule.cs) | **VF-033** | `Annotations` | [Annotations.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/VodafoneWorkFlowsCustomRules/Annotations.cs) | Enhanced + **Interactive Fix** |
| 27 | **VF-034** | `Naming Convention` | [VF-034_NamingConvention.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-034_NamingConvention.cs) | **VF-034** | `Naming Convention` | [NamingConvention.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/VodafoneWorkFlowsCustomRules/NamingConvention.cs) | Exact Match |
| 28 | **VF-035** | `VariablesNaming` | [VF-035_VariablesNaming.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-035_VariablesNaming.cs) | **VF-035** | `VariablesNaming` | [VariablesNaming.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/VodafoneActivitiesCustomRules/VariablesNaming.cs) | Exact Match |
| 29 | **VF-036** | `MicrosoftOfficeActivities` | [VF-036_MicrosoftOfficeActivitiesRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-036_MicrosoftOfficeActivitiesRule.cs) | **VF-036** | `MicrosoftOfficeActivities` | [MicrosoftOfficeActivities.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/VodafoneActivitiesCustomRules/MicrosoftOfficeActivities.cs) | Exact Match |
| 30 | **VF-037** | `UnusedArguments` | [VF-037_UnusedArguments.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-037_UnusedArguments.cs) | **VF-037** | `UnusedArguments` | [UnusedArguments.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/VodafoneWorkFlowsCustomRules/UnusedArguments.cs) | Exact Match |
| 31 | **VF-038** | `Branches Logging` | [VF-038_BranchesLogMessages.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-038_BranchesLogMessages.cs) | **VF-038** | `Branches Logging` | [BranchesLogMessages.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/VodafoneActivitiesCustomRules/BranchesLogMessages.cs) · [WorflowsBranchesLogMessages.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/VodafoneWorkFlowsCustomRules/WorflowsBranchesLogMessages.cs) | Exact Match (VF-018 handles batch fix) |
| 32 | **VF-039** | `PreAndPostConditions` | [VF-039_PreAndPostConditions.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-039_PreAndPostConditions.cs) | **VF-039** | `PreAndPostConditions` | [PreAndPostConditions.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/VodafoneWorkFlowsCustomRules/PreAndPostConditions.cs) | Exact Match |
| 33 | **VF-040** | `Activities Number` | [VF-040_NonUIActivitiesNum.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-040_NonUIActivitiesNum.cs) | **VF-040** | `Activities Number` | [NonUIActivitiesNum.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/VodafoneWorkFlowsCustomRules/NonUIActivitiesNum.cs) | Exact Match |
| 34 | **VF-041** | `BEInsideAutomation` | [VF-041_BEInsideAutomation.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-041_BEInsideAutomation.cs) | **VF-041** | `BEInsideAutomation` | [BEInsideAutomation.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/VodafoneWorkFlowsCustomRules/BEInsideAutomation.cs) | Exact Match |
| 35 | **VF-042** | `Multiple Apps Sequence` | [VF-042_MultipleAppsSequence.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-042_MultipleAppsSequence.cs) | **VF-042** | `Multiple Apps Sequence` | [MultipleAppsSequence.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/VodafoneWorkFlowsCustomRules/MultipleAppsSequence.cs) | Exact Match |
| 36 | **VF-043** | `Nested TryCatch` | [VF-043_AutomationNestedTryCatch.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-043_AutomationNestedTryCatch.cs) | **VF-043** | `Nested TryCatch` | [AutomationNestedTryCatch.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/VodafoneWorkFlowsCustomRules/AutomationNestedTryCatch.cs) | Exact Match |
| 37 | **VF-044** | `HardCoded TimeOut` | [VF-044_HardCodedTimeOut.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-044_HardCodedTimeOut.cs) | **VF-044** | `HardCoded TimeOut` | [HardCodedTimeOut.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/VodafoneWorkFlowsCustomRules/HardCodedTimeOut.cs) | Exact Match |
| 38 | **VF-045** | `HardCodedDelays` | [VF-045_HardCodedDelaysRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-045_HardCodedDelaysRule.cs) | **VF-045** | `HardCodedDelays` | [HardCodedDelays.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/VodafoneActivitiesCustomRules/HardCodedDelays.cs) | Exact Match |
| 39 | **VF-046** | `Folder Structure` | [VF-046_FolderStructure.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-046_FolderStructure.cs) | **VF-046** | `Folder Structure` | [FolderStructure.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/VodafoneWorkFlowsCustomRules/FolderStructure.cs) | Exact Match |
| 40 | **VF-047** | `Nested IFs` | [VF-047_NestedIfsRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-047_NestedIfsRule.cs) | **VF-047** | `Nested IFs` | [NestedIfs-Loops.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/VodafoneActivitiesCustomRules/NestedIfs-Loops.cs) | Exact Match |
| 41 | **VF-048** | `OrechestratorActivitesRetry` | [VF-048_OrechestratorActivitesRetry.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-048_OrechestratorActivitesRetry.cs) | **VF-048** | `OrechestratorActivitesRetry` | [OrechestratorActivitesRetry.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/VodafoneActivitiesCustomRules/OrechestratorActivitesRetry.cs) | Exact Match |
| 42 | **VF-049** | `InputValidation` | [VF-049_ValidateInput.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-049_ValidateInput.cs) | **VF-049** | `InputValidation` | [ValidateInput.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/VodafoneWorkFlowsCustomRules/ValidateInput.cs) | Exact Match |
| 43 | **VF-050** | `WQ Duplication Check` | [VF-050_WQDuplicates.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-050_WQDuplicates.cs) | **VF-050** | `WQ Duplication Check` | [WQDuplicates.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/VodafoneWorkFlowsCustomRules/WQDuplicates.cs) | Exact Match |
| 44 | **VF-051** | `Args Naming Convention` | [VF-051_ArgsNamingConvention.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-051_ArgsNamingConvention.cs) | **VF-051** | `Args Naming Convention` | [ArgsNamingConvention.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/VodafoneWorkFlowsCustomRules/ArgsNamingConvention.cs) | Exact Match |
| 45 | **VF-052** | `ResetPasswordCheck` | [VF-052_ResetPasswordCheck.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-052_ResetPasswordCheck.cs) | **VF-052** | `ResetPasswordCheck` | [ResetPasswordCheck.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/VodafoneWorkFlowsCustomRules/ResetPasswordCheck.cs) | Exact Match |
| 46 | **VF-053** | `CETTimeZone` | [VF-053_CetTimeZoneRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-053_CetTimeZoneRule.cs) | **VF-053** | `CETTimeZone` | [CheckCETTimeZone.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/VodafoneWorkFlowsCustomRules/CheckCETTimeZone.cs) | Enhanced (project-wide) + **Autofix** |
| 47 | **VF-057** | `static Selector` | [VF-057_SelectorAttributeValueFromXaml.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-057_SelectorAttributeValueFromXaml.cs) | **VF-057** · **VF-061** · **VF-064** *(alias)* | `static Selector` · `SelectorAttributeValue` | [SelectorAttributeValueFromXaml.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/VodafoneWorkFlowsCustomRules/SelectorAttributeValueFromXaml.cs) · [SelectorAttributeValue.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/VodafoneActivitiesCustomRules/SelectorAttributeValue.cs) | **Standardized** 2→1 |
| 48 | **VF-058** | `prohibited SetTransactionStatus` | [VF-058_SetTransactionStatusOnlyInHandoff.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-058_SetTransactionStatusOnlyInHandoff.cs) | **VF-058** · **VF-062** *(alias)* | `prohibited SetTransactionStatus` | [SetTransactionStatusOnlyInHandoff.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/VodafoneWorkFlowsCustomRules/SetTransactionStatusOnlyInHandoff.cs) | Exact Match + Alias |
| 49 | **VF-060** | `SelectorAttribute` | [VF-060_SelectorAttributeRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-060_SelectorAttributeRule.cs) | **VF-056** · **VF-060** · **VF-063** *(alias)* | `Selector Attribute` · `SelectorAttribute` | [SelectorAttributeFromXaml.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/VodafoneWorkFlowsCustomRules/SelectorAttributeFromXaml.cs) · [SelectorAttribute.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/VodafoneActivitiesCustomRules/SelectorAttribute.cs) | **Standardized** 2→1 + Alias |
| 50 | **VF-065** | `WorkflowFileNaming` | [VF-065_WorkflowFileNamingRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-065_WorkflowFileNamingRule.cs) | *(split from VF-021)* | — | — | **New in AnalyzerHelper** |
| 51 | **VF-070** | `Unused workflow files` | [VF-070_UnusedWorkflowFilesRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-070_UnusedWorkflowFilesRule.cs) | *(none)* | — | — | **New in AnalyzerHelper** |
| 52 | **VF-071** | `Unreachable Flowchart Nodes` | [VF-071_FlowchartOrphanNodesRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-071_FlowchartOrphanNodesRule.cs) | *(none)* | — | — | **New in AnalyzerHelper** |
| 53 | **VF-072** | `Empty Retry Scope Condition` | [VF-072_AddRetryScopeCheckTrueRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-072_AddRetryScopeCheckTrueRule.cs) | *(none)* | — | — | **New in AnalyzerHelper** |
| 54 | **VF-073** | `Config Constants Usage` | [VF-073_ConfigConstantsUsageRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-073_ConfigConstantsUsageRule.cs) | *(none)* | — | — | **New in AnalyzerHelper** |

---

## 4. Legacy Rules Consolidated / Merged in Modern Suite

Some legacy rules were redundant duplicates (Activity vs. Workflow variants of the same check). The modern suite merges them into single, unified rules:

| Modern Rule | Modern Name | Legacy Rules Merged | Legacy Names |
| :--- | :--- | :--- | :--- |
| [VF-012_TryCatchRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-012_TryCatchRule.cs) | `Missing Catch Block Actions` | **VF-012** + **VF-054** + **VF-055** | `TryCatch` + `ActivityCounterRule` + `RethrowRule` |
| [VF-057_SelectorAttributeValueFromXaml.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-057_SelectorAttributeValueFromXaml.cs) | `static Selector` | **VF-057** + **VF-061** | `static Selector` + `SelectorAttributeValue` |
| [VF-060_SelectorAttributeRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-060_SelectorAttributeRule.cs) | `SelectorAttribute` | **VF-056** + **VF-060** | `Selector Attribute` + `SelectorAttribute` |

> **Net effect**: 49 legacy rules → 46 active modern rules (3 pair-merges), plus 8 brand-new rules = **54 total**.

---

## 5. Legacy Metadata-Only Aliases (Not Real Rules)

These legacy IDs existed only as `RuleReportMetadata.cs` dictionary entries, never as rule class implementations:

| Metadata-Only ID | What It Pointed To | Real Modern Rule |
| :--- | :--- | :--- |
| **VF-017** | Alias for `HardCodedPasswords` | [VF-005_HardCodedPasswordsRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-005_HardCodedPasswordsRule.cs) |
| **VF-062** | Alias for `prohibited SetTransactionStatus` | [VF-058_SetTransactionStatusOnlyInHandoff.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-058_SetTransactionStatusOnlyInHandoff.cs) |
| **VF-063** | Comment-only in `SelectorAttribute.cs` (no class) | [VF-060_SelectorAttributeRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-060_SelectorAttributeRule.cs) |
| **VF-064** | Alias for `static Selector` | [VF-057_SelectorAttributeValueFromXaml.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-057_SelectorAttributeValueFromXaml.cs) |

---

## 6. New Rules Unique to AnalyzerHelper (No Legacy Counterpart)

These 8 rules have **no equivalent** in the legacy UiPath Studio plugin suite:

| New Rule ID | New Rule Name | File | Fix Type | Purpose |
| :--- | :--- | :--- | :---: | :--- |
| **VF-013** | `Remove Default Values` | [VF-013_RemoveDefaultsVarArgRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-013_RemoveDefaultsVarArgRule.cs) | **Autofix** | Clears hardcoded default values on Variable and Argument declarations |
| **VF-019** | `Remove Unused Variables/Arguments` | [VF-019_RemoveUnusedVarArgRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-019_RemoveUnusedVarArgRule.cs) | **Autofix** | Removes unreferenced variable/argument declarations project-wide |
| **VF-020** | `SyncInvokeArguments` | [VF-020_SyncInvokeArgumentsRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-020_SyncInvokeArgumentsRule.cs) | **Autofix** | Synchronizes InvokeWorkflowFile argument bindings with the target XAML |
| **VF-065** | `WorkflowFileNaming` | [VF-065_WorkflowFileNamingRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-065_WorkflowFileNamingRule.cs) | **Interactive Fix** | Renames workflow files per Vodafone conventions and updates all references |
| **VF-070** | `Unused workflow files` | [VF-070_UnusedWorkflowFilesRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-070_UnusedWorkflowFilesRule.cs) | **Interactive Fix** | Detects and batch-deletes unreferenced `.xaml` files |
| **VF-071** | `Unreachable Flowchart Nodes` | [VF-071_FlowchartOrphanNodesRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-071_FlowchartOrphanNodesRule.cs) | **Autofix** | Prunes orphaned/disconnected nodes from Flowchart diagrams |
| **VF-072** | `Empty Retry Scope Condition` | [VF-072_AddRetryScopeCheckTrueRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-072_AddRetryScopeCheckTrueRule.cs) | **Autofix** | Injects `CheckTrue` into empty `RetryScope.Condition` blocks |
| **VF-073** | `Config Constants Usage` | [VF-073_ConfigConstantsUsageRule.cs](file:///c:/Users/pc/Desktop/Vois%20Analyzer/Vodafone-AnalyzerHealper/AnalyzerHelper/Rules/VF-073_ConfigConstantsUsageRule.cs) | Validate-Only | Validates Config dictionary key references against the Excel Config file |

---

## 7. Parity & Coverage Summary Tree

```
Legacy Rules (old suite):      49 unique rules
                                ├── 46 ported to modern AnalyzerHelper
                                │    ├── 41 direct 1-to-1 matches
                                │    ├──  3 merged/consolidated (VF-012, VF-057, VF-060)
                                │    └──  2 evolved/separated (VF-018, VF-021+VF-065)
                                └──  0 unported  ✅  (100% parity)

Modern Rules (AnalyzerHelper):  54 total rules
                                ├── 46 covering legacy rules
                                └──  8 brand-new value-add rules
                                     (VF-013, VF-019, VF-020, VF-065,
                                      VF-070, VF-071, VF-072, VF-073)

Fix Capabilities (Modern):      54 total
                                ├── 10 Autofix       (instant, non-interactive)
                                ├──  5 Interactive   (batch dialogs)
                                └── 39 Validate-Only (report/flag)

Fix Capabilities (Legacy):      49 total
                                └── 49 Validate/Report only  (0 with any fix)
```
