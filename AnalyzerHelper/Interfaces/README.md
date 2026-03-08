# Interfaces

**Purpose:** Contract definitions for rules and fix behavior.

- **IAnalyzerRule** – Report path: `Check(filePath, content)` returns findings. Used when user runs "Run rules (validate)".
- **IAnalyzerRuleWithFix** – Fix path: `DefineAndFix(filePath, content, out newContent)`. Used when user clicks "Apply fix" from No/Need interaction tab.

Add new rule contracts here. Implementation lives in **Rules/**.
