@echo off
setlocal

set "SCRIPT_DIR=%~dp0"
set "PROJECT_FILE=%SCRIPT_DIR%..\src\RevitHealthEnhancer\RevitHealthEnhancer.csproj"
set "VERSIONS=2022 2023 2024 2025 2026"

for %%V in (%VERSIONS%) do (
    set "REVIT_API=C:\Program Files\Autodesk\Revit %%V\RevitAPI.dll"
    call :BuildVersion %%V
    if errorlevel 1 exit /b 1
)

echo.
echo Finished building available Revit packages.
echo Distribution packages are under:
echo %SCRIPT_DIR%..\TeamPackage
echo.
pause
exit /b 0

:BuildVersion
set "VERSION=%~1"
set "REVIT_API=C:\Program Files\Autodesk\Revit %VERSION%\RevitAPI.dll"

if not exist "%REVIT_API%" (
    echo Skipping Revit %VERSION% because RevitAPI.dll was not found at "%REVIT_API%".
    exit /b 0
)

echo.
echo Building RevitHealthEnhancer for Revit %VERSION%...
dotnet build "%PROJECT_FILE%" -c Release --no-incremental -p:RevitVersion=%VERSION% -p:InstallAddinAfterBuild=false -p:CreateTeamPackageAfterBuild=true

if errorlevel 1 (
    echo Build failed for Revit %VERSION%.
    exit /b 1
)

exit /b 0
