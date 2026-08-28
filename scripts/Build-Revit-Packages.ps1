$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectFile = Join-Path $scriptDir "..\src\RevitHealthEnhancer\RevitHealthEnhancer.csproj"
$versions = @("2022", "2023", "2024", "2025", "2026")

foreach ($version in $versions) {
    $revitApi = "C:\Program Files\Autodesk\Revit $version\RevitAPI.dll"

    if (-not (Test-Path -LiteralPath $revitApi)) {
        Write-Host "Skipping Revit $version because RevitAPI.dll was not found."
        continue
    }

    Write-Host ""
    Write-Host "Building RevitHealthEnhancer for Revit $version..."
    dotnet build $projectFile -c Release --no-incremental -p:RevitVersion=$version -p:InstallAddinAfterBuild=false -p:CreateTeamPackageAfterBuild=true

    if ($LASTEXITCODE -ne 0) {
        throw "Build failed for Revit $version."
    }
}

Write-Host ""
Write-Host "Finished building available Revit packages."
Write-Host "Distribution packages are under:"
Write-Host (Join-Path $scriptDir "..\TeamPackage")
