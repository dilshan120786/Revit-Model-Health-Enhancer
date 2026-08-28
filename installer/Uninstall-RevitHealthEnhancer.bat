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

set "ADDIN_ROOT=%APPDATA%\Autodesk\Revit\Addins\%REVIT_VERSION%"
set "INSTALL_DIR=%ADDIN_ROOT%\RevitHealthEnhancer"
set "MANIFEST_PATH=%ADDIN_ROOT%\RevitHealthEnhancer.addin"

if exist "%MANIFEST_PATH%" del /F /Q "%MANIFEST_PATH%"
if exist "%INSTALL_DIR%" rmdir /S /Q "%INSTALL_DIR%"

echo Uninstalled RevitHealthEnhancer for Revit %REVIT_VERSION%
echo.
pause
