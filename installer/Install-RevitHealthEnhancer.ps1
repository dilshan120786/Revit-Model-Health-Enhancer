param(
    [string]$RevitVersion = ""
)

$ErrorActionPreference = "Stop"

function Unblock-AddinFiles {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    Get-ChildItem -LiteralPath $Path -Recurse -File |
        ForEach-Object {
            try {
                Unblock-File -LiteralPath $_.FullName
            }
            catch {
                # Best-effort only. Some execution policies disable Unblock-File.
            }
        }
}

function Get-ReferencedAssemblyVersion {
    param(
        [Parameter(Mandatory = $true)]
        [string]$AssemblyPath,

        [Parameter(Mandatory = $true)]
        [string]$AssemblyName
    )

    try {
        $assembly = [System.Reflection.Assembly]::ReflectionOnlyLoadFrom($AssemblyPath)
        $reference = $assembly.GetReferencedAssemblies() |
            Where-Object { $_.Name -eq $AssemblyName } |
            Select-Object -First 1

        if ($reference) {
            return $reference.Version
        }
    }
    catch {
        return $null
    }

    return $null
}

function Warn-IfRevitApiLooksDifferent {
    param(
        [Parameter(Mandatory = $true)]
        [string]$AssemblyPath,

        [Parameter(Mandatory = $true)]
        [string]$RevitVersion
    )

    $revitApiPath = Join-Path $env:ProgramFiles "Autodesk\Revit $RevitVersion\RevitAPI.dll"

    if (-not (Test-Path -LiteralPath $revitApiPath)) {
        Write-Warning "Revit $RevitVersion was not found at '$revitApiPath'. Make sure this package matches the Revit version being launched."
        return
    }

    try {
        $installedApiVersion = [System.Reflection.AssemblyName]::GetAssemblyName($revitApiPath).Version
        $requiredApiVersion = Get-ReferencedAssemblyVersion -AssemblyPath $AssemblyPath -AssemblyName "RevitAPI"

        if ($requiredApiVersion -and $installedApiVersion.Major -ne $requiredApiVersion.Major) {
            Write-Warning "This add-in references RevitAPI $requiredApiVersion, but installed RevitAPI is $installedApiVersion. Install the matching Revit package."
        }
        elseif ($requiredApiVersion -and $installedApiVersion -lt $requiredApiVersion) {
            Write-Warning "This add-in was built against RevitAPI $requiredApiVersion, but installed RevitAPI is $installedApiVersion. If Revit shows a load error, update Revit or rebuild the package against your Revit patch level."
        }
    }
    catch {
        Write-Warning "Could not verify the installed Revit API version. $($_.Exception.Message)"
    }
}

function ConvertTo-XmlText {
    param(
        [AllowNull()]
        [string]$Value
    )

    return [System.Security.SecurityElement]::Escape($Value)
}

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

$sourceDll = Join-Path $scriptDir "RevitHealthEnhancer.dll"

if (-not (Test-Path -LiteralPath $sourceDll)) {
    throw "RevitHealthEnhancer.dll was not found next to this installer script."
}

Unblock-AddinFiles -Path $scriptDir
Warn-IfRevitApiLooksDifferent -AssemblyPath $sourceDll -RevitVersion $RevitVersion

$addinRoot = Join-Path $env:APPDATA "Autodesk\Revit\Addins\$RevitVersion"
$installDir = Join-Path $addinRoot "RevitHealthEnhancer"
$manifestPath = Join-Path $addinRoot "RevitHealthEnhancer.addin"

if (Test-Path -LiteralPath $installDir) {
    Remove-Item -LiteralPath $installDir -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $installDir | Out-Null

Get-ChildItem -LiteralPath $scriptDir -File |
    Where-Object { $_.Extension -in ".dll", ".pdb", ".config", ".json" } |
    ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $installDir -Force
    }

Unblock-AddinFiles -Path $installDir

$installedDll = Join-Path $installDir "RevitHealthEnhancer.dll"
$installedDllXml = ConvertTo-XmlText $installedDll

$manifest = @"
<?xml version="1.0" encoding="utf-8" standalone="no"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Name>RevitHealthEnhancer</Name>
    <Assembly>$installedDllXml</Assembly>
    <AddInId>8f5a6245-a093-4f5b-9b8b-6579d7b7b29b</AddInId>
    <FullClassName>RevitHealthEnhancer.App</FullClassName>
    <VendorId>DKHN</VendorId>
    <VendorDescription>Dilshan Khan - Revit BIM Automation</VendorDescription>
  </AddIn>
</RevitAddIns>
"@

Set-Content -LiteralPath $manifestPath -Value $manifest -Encoding UTF8

Write-Host "Installed RevitHealthEnhancer for Revit $RevitVersion"
Write-Host "Add-in files: $installDir"
Write-Host "Manifest: $manifestPath"
Write-Host "Restart Revit to load the add-in."
