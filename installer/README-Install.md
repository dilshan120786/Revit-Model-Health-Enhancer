# Revit Model Health Enhancer — Installation Guide

This folder contains the automated installer scripts for **Revit Model Health Enhancer**.

## Package Contents

When distributing a compiled release, the package contains:

- `RevitHealthEnhancer.dll`
- `RevitHealthEnhancer.pdb` (optional)
- `RevitHealthEnhancer.deps.json` (for Revit 2025/2026)
- `RevitVersion.txt`
- `Install-RevitHealthEnhancer.bat`
- `Install-RevitHealthEnhancer.ps1`
- `Uninstall-RevitHealthEnhancer.bat`
- `Uninstall-RevitHealthEnhancer.ps1`

## Installation

### Method 1: Batch Installer (Recommended)

1. Close Autodesk Revit.
2. Double-click `Install-RevitHealthEnhancer.bat`.
3. Launch Autodesk Revit.

### Method 2: PowerShell Installer

1. Close Autodesk Revit.
2. Right-click `Install-RevitHealthEnhancer.ps1` and select **Run with PowerShell** (or run from terminal).
3. Launch Autodesk Revit.

The installer deploys the add-in files to:
`%APPDATA%\Autodesk\Revit\Addins\<version>\RevitHealthEnhancer`

And creates the `.addin` manifest at:
`%APPDATA%\Autodesk\Revit\Addins\<version>\RevitHealthEnhancer.addin`

## Running The Tool

1. Open a model in Autodesk Revit.
2. Navigate to the **Model Health** tab on the Revit ribbon.
3. In the **Health Enhancer** panel, click **Health Enhancer**.
4. Select the desired checks and fixes in the confirmation window, check the acknowledgement box, and click **Run Selected Fixes**.
5. When prompted, choose where to save the generated HTML audit report.

## Uninstallation

1. Close Autodesk Revit.
2. Run `Uninstall-RevitHealthEnhancer.bat` or `Uninstall-RevitHealthEnhancer.ps1`.
