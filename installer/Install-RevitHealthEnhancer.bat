@echo off
setlocal

set "REVIT_VERSION=%~1"

set "SCRIPT_DIR=%~dp0"
if "%REVIT_VERSION%"=="" (
    if exist "%SCRIPT_DIR%RevitVersion.txt" (
        set /p REVIT_VERSION=<"%SCRIPT_DIR%RevitVersion.txt"
    )
)
if "%REVIT_VERSION%"=="" set "REVIT_VERSION=2024"

set "SOURCE_DLL=%SCRIPT_DIR%RevitHealthEnhancer.dll"

where powershell.exe > nul 2> nul
if not errorlevel 1 (
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%Install-RevitHealthEnhancer.ps1" -RevitVersion "%REVIT_VERSION%"
    if not errorlevel 1 goto done
    echo.
    echo PowerShell installer failed. Trying basic batch install instead...
    echo.
)

if not exist "%SOURCE_DLL%" (
    echo RevitHealthEnhancer.dll was not found next to this installer.
    echo.
    pause
    exit /b 1
)

set "ADDIN_ROOT=%APPDATA%\Autodesk\Revit\Addins\%REVIT_VERSION%"
set "INSTALL_DIR=%ADDIN_ROOT%\RevitHealthEnhancer"
set "MANIFEST_PATH=%ADDIN_ROOT%\RevitHealthEnhancer.addin"
set "INSTALLED_DLL=%INSTALL_DIR%\RevitHealthEnhancer.dll"

if not exist "%ADDIN_ROOT%" mkdir "%ADDIN_ROOT%"
if exist "%INSTALL_DIR%" rmdir /S /Q "%INSTALL_DIR%"
if not exist "%INSTALL_DIR%" mkdir "%INSTALL_DIR%"

copy /Y "%SCRIPT_DIR%*.dll" "%INSTALL_DIR%\" > nul
copy /Y "%SCRIPT_DIR%*.pdb" "%INSTALL_DIR%\" > nul 2> nul
copy /Y "%SCRIPT_DIR%*.config" "%INSTALL_DIR%\" > nul 2> nul
copy /Y "%SCRIPT_DIR%*.json" "%INSTALL_DIR%\" > nul 2> nul

where powershell.exe > nul 2> nul
if not errorlevel 1 (
    powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "Get-ChildItem -LiteralPath '%INSTALL_DIR%' -Recurse -File | Unblock-File" > nul 2> nul
)

(
echo ^<?xml version="1.0" encoding="utf-8" standalone="no"?^>
echo ^<RevitAddIns^>
echo   ^<AddIn Type="Application"^>
echo     ^<Name^>RevitHealthEnhancer^</Name^>
echo     ^<Assembly^>%INSTALLED_DLL%^</Assembly^>
echo     ^<AddInId^>8f5a6245-a093-4f5b-9b8b-6579d7b7b29b^</AddInId^>
echo     ^<FullClassName^>RevitHealthEnhancer.App^</FullClassName^>
echo     ^<VendorId^>DKHN^</VendorId^>
echo     ^<VendorDescription^>Dilshan Khan - Revit BIM Automation^</VendorDescription^>
echo   ^</AddIn^>
echo ^</RevitAddIns^>
) > "%MANIFEST_PATH%"

echo Installed RevitHealthEnhancer for Revit %REVIT_VERSION%
echo Add-in files: %INSTALL_DIR%
echo Manifest: %MANIFEST_PATH%
echo.
echo Restart Revit to load the add-in.
echo.

:done
pause
