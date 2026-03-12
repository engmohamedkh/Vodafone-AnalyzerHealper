# Test case: ProcessFixer_LaunchPad_OpenSR_Navigatetest.xaml

Use this workflow to verify Analyzer Helper rules and fixes.

## File location

- **Path:** `ProcessFixer_LaunchPad_OpenSR_Navigatetest.xaml` (project root or your solution folder)

## Current state (reference)

| Item | Value |
|------|--------|
| **File name** | `ProcessFixer_LaunchPad_OpenSR_Navigatetest.xaml` |
| **x:Class** | `ProcessFixer_LaunchPad_OpenSR_Navigatetest` |
| **DisplayName** | `ProcessFixer_LaunchPad_OpenSR_Navigatetest` |
| **strComponentName** | `ProcessFixer_LaunchPad_OpenSR_Navigatetest` |
| **Annotation** | All 5 sections present: Component Name, Description, Pre Condition, Post Condition, PDD Section |
| **TryCatch** | Present; Catch has Error_Log (exception.Message, exception.Source) and Rethrow |

## How to use as test case

1. **Load in Analyzer Helper**
   - Point the app to a solution/folder that contains this file (e.g. copy it into `C:\Analyser\TestProject\Subprocess\` if you want to test **WorkflowFileNaming** on a Subprocess path).
   - Select the file in the file list.

2. **Run rules**
   - **Run rules (validate)** to see which rules report findings for this file.

3. **Rule-specific checks**

   - **VF-012 TryCatch**  
     This file already has TryCatch with exception logging and Rethrow → no finding expected.

   - **VF-015 WorkflowFileNaming**  
     Only runs for files under a folder named `Subprocess` or `Logic`.  
     - If the file is under **Subprocess**: filename has 3 underscores; x:Class/DisplayName match the file name → may pass or fail depending on ShortName_Stage_WorkflowName convention.  
     - Use **Apply fix** to open the rename dialog and change Short Name / Process Stage, then confirm file rename and annotation/strComponentName update.

   - **VF-016 Annotation**  
     Annotation has all 5 sections → no finding expected.  
     - You can still use **Apply fix** to open the **Workflow Annotation** dialog and edit Component Name, Description, Pre Condition, Post Condition, PDD Section; OK will update the workflow.

4. **Apply fix (Need interaction)**
   - Select **WorkflowFileNaming** and/or **Annotation** in the **Need interaction** tab.
   - Click **Apply fix to selected files** and confirm.
   - For **WorkflowFileNaming**: enter Short Name and Process Stage in the rename dialog → file is renamed and x:Class, DisplayName, strComponentName are updated.
   - For **Annotation**: edit the five annotation fields in the popup → workflow annotation is updated.

## Summary

This file is a suitable test case because it:

- Has a **full annotation** (all 5 sections) for testing the Annotation rule and fix dialog.
- Has **TryCatch with exception logging and Rethrow** for validating the TryCatch rule.
- Has **consistent naming** (x:Class, DisplayName, strComponentName) for testing the WorkflowFileNaming rule when the file is under Subprocess/Logic.
