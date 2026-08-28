# Revit Model Health Enhancer

[![Revit API](https://img.shields.io/badge/Revit%20API-2022%20%7C%202023%20%7C%202024%20%7C%202025%20%7C%202026-blue.svg)](https://www.autodesk.com/products/revit/overview)
[![.NET](https://img.shields.io/badge/.NET-Framework%204.8%20%7C%208.0--windows-purple.svg)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows-lightgrey.svg)]()
[![Developer](https://img.shields.io/badge/Developer-Dilshan%20Khan-orange.svg)]()

**Revit Model Health Enhancer** is an independently developed C# Revit add-in by Dilshan Khan, built with the Autodesk Revit API to automate BIM model health audits, element cleanups, standards enforcement, and quality assurance reporting.

**Developed by Dilshan Khan**

---

## Overview

Over the lifecycle of complex BIM projects, Autodesk Revit models accumulate redundant data, unplaced elements, unpinned datum controls, duplicate type/instance warnings, and unreferenced styles. These issues degrade model performance, increase file size, cause view lag, and introduce coordination errors.

**Revit Model Health Enhancer** provides a robust, transaction-safe, single-click solution to audit and remediate model degradation while generating comprehensive HTML health reports.

---

## Key Capabilities

The add-in automates **10 core health and cleanup modules**:

| Module | Description | Implementation Details |
| :--- | :--- | :--- |
| **1. Control Element Pinning** | Pins critical datum and coordination geometry to prevent accidental movement. | Automatically identifies and pins all `Level`, `Grid`, `RevitLinkInstance`, and `ImportInstance` (CAD link) elements. |
| **2. Datum Workset Alignment** | Enforces workset organization standards on workshared models. | Moves all Levels and Grids to the standard `"Shared Levels and Grids"` workset (`ELEM_PARTITION_PARAM`). |
| **3. Invalid Room Cleanup** | Removes unplaced, unbounded, and redundant room objects. | Deletes `SpatialElement` / `Room` instances with missing spatial location (`Location == null`) or zero/invalid computed area. |
| **4. Invalid MEP Space Cleanup** | Removes unplaced, unbounded, and redundant MEP spaces. | Deletes `Space` elements with missing location or zero area. |
| **5. Unused View Template Cleanup** | Removes obsolete view templates cluttering project standards. | Identifies non-template views (`View`), audits assigned template IDs (`view.ViewTemplateId`), and deletes unreferenced templates. |
| **6. Unused Parameter Filter Cleanup** | Removes unassigned view filters. | Analyzes view graphics overrides across all eligible views (`view.GetOrderedFilters()`) and deletes unapplied `ParameterFilterElement` items. |
| **7. Unused Text Style Cleanup** | Purges redundant text note types. | Audits placed `TextNote` instances and deletes unreferenced `TextNoteType` styles while safely preserving internal/system types (e.g., `<Schedule Default>`). |
| **8. Duplicate Mark Resolution** | Automatically resolves duplicate parameter warnings. | Scans active Revit warnings (`FailureMessage`), identifies duplicate Mark / Type Mark conflicts, retains the primary mark, and assigns deterministic suffixes (e.g., `_A`, `_B`, `_C`) to conflicting elements. |
| **9. Duplicate Instance Cleanup** | Safely deletes identical element instances placed in the same location. | Evaluates duplicate instance warnings using a candidate scoring heuristic (evaluating tag associations, hosted dependencies, and parameter richness) to delete redundant duplicates while preserving model integrity. |
| **10. Navisworks 3D View Creation** | Ensures standard coordination views are available for export. | Verifies the existence of a coordination 3D view; if absent, creates an isometric `View3D` named `"Naviswork Export"`. |

---

## User Interface & Confirmation Safety

To ensure complete control and prevent unintended modifications:
* **Selective Execution**: The WPF modal window allows users to toggle individual health operations on or off.
* **Explicit User Confirmation**: Operations cannot execute until the user checks the safety acknowledgement box (*"I understand the selected fixes will modify the active Revit model"*).
* **Transaction Rollback Safety**: All operations run within a managed `Autodesk.Revit.DB.Transaction`. If an unhandled exception occurs, the transaction safely aborts without corrupting project data.

---

## Automated HTML Audit Reporting

Upon completion, the tool exports a clean, standalone HTML audit report containing:
* Execution summary metrics (counts of pinned elements, deleted rooms, resolved warnings, workset modifications).
* Detailed tabular breakdown of all modified or deleted elements with their corresponding **Revit Element IDs**.
* Duplicate element resolution log (showing which candidate was retained and which was deleted).
* Full failure and operational logs for quality tracking.

---

## Technical Architecture

```text
Revit-Model-Health-Enhancer/
├── src/
│   └── RevitHealthEnhancer/
│       ├── Commands/
│       │   ├── AboutCommand.cs            # Informational dialog and version metadata
│       │   └── BimHealthImproverCommand.cs # External command entry point & file dialog
│       ├── Core/
│       │   ├── BimHealthImprover.cs       # Core business logic & health engine
│       │   └── HealthImproverOptions.cs   # Data model storing active check flags
│       ├── Properties/                    # Assembly settings & metadata
│       ├── UI/
│       │   ├── HealthImproverOptionsWindow.cs # Pure WPF code-behind configuration UI
│       │   └── RibbonIcons.cs             # Procedural WPF vector icon generation
│       ├── App.cs                         # IExternalApplication ribbon registration
│       ├── RevitHealthEnhancer.addin      # Add-in manifest template
│       └── RevitHealthEnhancer.csproj     # Multi-targeting SDK-style project file
├── installer/                             # Automated user deployment scripts
├── scripts/                               # Multi-version build automation scripts
├── docs/                                  # Project documentation & screenshots
└── RevitHealthEnhancer.slnx               # Visual Studio solution
```

### Key Architectural Highlights
* **Zero External Dependencies**: Button icons are drawn dynamically using WPF vector geometry (`DrawingGroup`, `DrawingContext`), eliminating external `.png` asset dependencies.
* **API Modernization & Multi-Targeting**: Supports both legacy `.NET Framework 4.8` (Revit 2022–2024) and modern `.NET 8.0-windows` (Revit 2025–2026) using conditional compilation symbols (`REVIT2024_OR_GREATER`, `REVIT2025_OR_GREATER`, `REVIT2026_OR_GREATER`).
* **ElementId Compatibility**: Automatically accommodates Revit API changes, such as the transition from 32-bit `ElementId.IntegerValue` to 64-bit `ElementId.Value`.

---

## Revit Compatibility Matrix

| Revit Version | .NET Target | Compilation Symbol | API Architecture |
| :--- | :--- | :--- | :--- |
| **Revit 2022** | `.NET Framework 4.8` | Default | 32-bit `ElementId.IntegerValue` |
| **Revit 2023** | `.NET Framework 4.8` | Default | 32-bit `ElementId.IntegerValue` |
| **Revit 2024** | `.NET Framework 4.8` | `REVIT2024_OR_GREATER` | 64-bit `ElementId.Value` |
| **Revit 2025** | `.NET 8.0 Windows` | `REVIT2024_OR_GREATER;REVIT2025_OR_GREATER` | .NET 8 Runtime |
| **Revit 2026** | `.NET 8.0 Windows` | `REVIT2024_OR_GREATER;REVIT2025_OR_GREATER;REVIT2026_OR_GREATER` | .NET 8 Runtime |

---

## Installation & Deployment

### Quick Install for Users
1. Download or extract the compiled release package for your Revit version.
2. Close Autodesk Revit.
3. Run `Install-RevitHealthEnhancer.bat` (or right-click `Install-RevitHealthEnhancer.ps1` and select **Run with PowerShell**).
4. Launch Revit. The **Model Health** tab will appear on the ribbon.

---

## Developer Setup & Build Instructions

### Prerequisites
* Visual Studio 2022 (v17.8 or newer) or Visual Studio Code with C# Dev Kit.
* [.NET SDK 8.0](https://dotnet.microsoft.com/download) and [.NET Framework 4.8 Developer Pack](https://dotnet.microsoft.com/download/dotnet-framework/net48).
* Autodesk Revit (2022, 2023, 2024, 2025, or 2026) installed locally for API reference assemblies.

### Build via Command Line
To build for a specific Revit version (e.g., Revit 2024):
```powershell
dotnet build src/RevitHealthEnhancer/RevitHealthEnhancer.csproj -c Release -p:RevitVersion=2024
```

To build for Revit 2025 (.NET 8):
```powershell
dotnet build src/RevitHealthEnhancer/RevitHealthEnhancer.csproj -c Release -p:RevitVersion=2025
```

To build all available installed Revit versions automatically:
```powershell
.\scripts\Build-Revit-Packages.ps1
```

---

## Technical Limitations

* **Workshared Level/Grid Movement**: Requires the target workset (`"Shared Levels and Grids"`) to exist in the workshared model prior to execution. If absent, the workset step is safely skipped and logged.
* **Element Ownership in Worksharing**: In multi-user workshared models, elements currently checked out or locked by other team members cannot be modified and will be recorded in the failure log without interrupting other cleanup tasks.
* **Grouped Elements**: Redundant elements inside Revit Model Groups are intentionally excluded from deletion to preserve group definition integrity.

---

## Screenshots & Visual Documentation

Visual documentation assets (such as UI selection dialogs, Revit ribbon integration, and HTML audit report previews) can be placed in [`docs/images/`](docs/images/).

---

## Disclaimer

Autodesk and Revit are registered trademarks of Autodesk, Inc. This project is an independent third-party development and is not affiliated with, sponsored by, or endorsed by Autodesk, Inc.
