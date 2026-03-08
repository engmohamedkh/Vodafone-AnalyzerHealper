# Analyzer Helper (desktop app)

Desktop app to view and apply **role-based fixes** aligned with the Vodafone Workflow Analyzer rules. Project is arranged into **Roles** and **View** folders so you can edit the UI easily and add new role implementations in one place.

## Project layout

```
AnalyzerHelper/
├── App.xaml, App.xaml.cs
├── Roles/                          ← Role logic: add new roles here
│   ├── FixCategory.cs             (AutoFix | RequiresUserInteraction)
│   ├── RoleFixItem.cs             (single fix item model)
│   ├── RoleReference.cs           (workflow role names, aligned with analyzer)
│   └── RoleFixRegistry.cs         ← Add new role implementations here
│
└── View/                           ← UI: edit views and themes here
    ├── MainWindow.xaml
    ├── MainWindow.xaml.cs
    └── Themes/
        └── Colors.xaml
```

- **Roles folder** – All role types and the list of fixes. To add a new role: open `Roles/RoleFixRegistry.cs` and add one entry to `GetNoInteractionRoles()` or `GetNeedInteractionRoles()`.
- **View folder** – All screens and theme. Edit `View/MainWindow.xaml` (or add new windows under View) and `View/Themes/Colors.xaml` without touching role logic.

## Features

1. **Optimized, attractive UI** – Clean layout with teal accent, card-based lists, two folders (No interaction / Need interaction).
2. **More roles with autofix** – Naming, selector attributes, annotations, log fields, CET timezone, delay documentation.
3. **Categories by user interaction** – No interaction (auto fix) vs Need interaction (requires your input).

## Run

```bash
dotnet run --project AnalyzerHelper
```

Or from Visual Studio: set **AnalyzerHelper** as startup project and F5.

## Add to solution

```bash
dotnet sln ../AnalyzerUpgrade.sln add AnalyzerHelper/AnalyzerHelper.csproj
```

Or in Visual Studio: Solution → Add → Existing Project → `AnalyzerHelper.csproj`.

## Optional: reference analyzer rules

To use `WorkflowRoleHelper` from the analyzer directly, uncomment the `ProjectReference` in `AnalyzerHelper.csproj`. The app uses `Roles/RoleReference.cs` for role names so it builds without the analyzer reference.
