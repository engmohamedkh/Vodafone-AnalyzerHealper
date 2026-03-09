# Test file for VF-015 WorkflowFileNaming rule and fix

**File:** `Fiori_LaunchPad_OpenSR_Navigatetest.xaml`

This workflow is used to verify that the **WorkflowFileNaming (VF-015)** rule detects issues and that **Apply fix** correctly updates the file name, x:Class, DisplayName, and `strComponentName` variable.

## Current state (before fix)

| Item            | Current value                          |
|-----------------|----------------------------------------|
| File name       | `Fiori_LaunchPad_OpenSR_Navigatetest.xaml` |
| x:Class         | `FixedProcess_Worker_OpenSR_Navigatetest`  |
| DisplayName     | `FixedProcess_Worker_OpenSR_Navigatetest`  |
| strComponentName| `Fiori_LaunchPad_OpenSR_Navigate`          |

The rule flags this because x:Class and DisplayName do not match the file name.

## How to verify

1. **Use a path that contains "Subprocess" or "Logic"**  
   The rule runs only for files under a folder named `Subprocess` or `Logic`. For example:
   - Copy this file to: `C:\Analyser\TestProject\Subprocess\Fiori_LaunchPad_OpenSR_Navigatetest.xaml`
   - Or use any solution where this file lives under a `Subprocess` (or `Logic`) folder.

2. **Run the rule**
   - Open Analyzer Helper and load the solution (e.g. `C:\Analyser\TestProject`).
   - Select the file under Subprocess.
   - Open the **Need interaction** tab and select **WorkflowFileNaming (VF-015)**.
   - Click **Run rules (validate)**.  
   You should see one finding, e.g.  
   `x:Class attribute ('FixedProcess_Worker_OpenSR_Navigatetest') does not match filename ('Fiori_LaunchPad_OpenSR_Navigatetest').`

3. **Apply the fix**
   - Click **Apply fix to selected files** and confirm.
   - In the **Rename Workflow File — Configuration** dialog enter:
     - **Project Short Name:** e.g. `MyShort`
     - **Process Stage:** e.g. `Stage1` (Subprocess)
   - Click **OK**.

4. **Check after fix**
   - **File name:** `MyShort_Stage1_OpenSR_Navigatetest.xaml` (in the same folder).
   - **x:Class:** `MyShort_Stage1_OpenSR_Navigatetest`
   - **DisplayName:** `MyShort_Stage1_OpenSR_Navigatetest`
   - **strComponentName** variable Default: `MyShort_Stage1_OpenSR_Navigatetest`
   - Original file path should no longer exist (file was renamed, not duplicated).

## Subprocess naming convention

Subprocess files must follow: **{ShortName}_{Stage}_{WorkflowName}.xaml**  
From the current file name, the workflow name part is `OpenSR_Navigatetest` (after the second underscore).
