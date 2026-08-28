param(
    [string]$RevitVersion = ""
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

if ([string]::IsNullOrWhiteSpace($RevitVersion)) {
    $versionFile = Join-Path $scriptDir "RevitVersion.txt"

    if (Test-Path -LiteralPath $versionFile) {
        $RevitVersion = (Get-Content -LiteralPath $versionFile -First 1).Trim()
    }
}

if ([string]::IsNullOrWhiteSpace($RevitVersion)) {
    $RevitVersion = "2024"
}

$addinRoot = Join-Path $env:APPDATA "Autodesk\Revit\Addins\$RevitVersion"
$installDir = Join-Path $addinRoot "RevitHealthEnhancer"
$manifestPath = Join-Path $addinRoot "RevitHealthEnhancer.addin"

if (Test-Path -LiteralPath $manifestPath) {
    Remove-Item -LiteralPath $manifestPath -Force
}

if (Test-Path -LiteralPath $installDir) {
    Remove-Item -LiteralPath $installDir -Recurse -Force
}

Write-Host "Uninstalled RevitHealthEnhancer for Revit $RevitVersion"
