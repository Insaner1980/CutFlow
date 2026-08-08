[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$developmentRoot = [System.IO.Path]::GetFullPath(
    (Join-Path $repositoryRoot "src\CutFlow\bin"))
$developmentPrefix = $developmentRoot.TrimEnd("\", "/") + [System.IO.Path]::DirectorySeparatorChar
$manifestPath = Join-Path $developmentRoot "x64\$Configuration\net10.0-windows10.0.26100.0\AppxManifest.xml"

if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw "CutFlow $Configuration output was not found. Build the x64 configuration before registering it."
}

$conflictingPackages = @(
    Get-AppxPackage -Name CutFlow | Where-Object {
        if ([string]::IsNullOrWhiteSpace($_.InstallLocation)) {
            return $true
        }

        $installLocation = [System.IO.Path]::GetFullPath($_.InstallLocation).TrimEnd("\", "/")
        return -not $installLocation.StartsWith(
            $developmentPrefix,
            [System.StringComparison]::OrdinalIgnoreCase)
    }
)

if ($conflictingPackages.Count -gt 0) {
    $packageNames = ($conflictingPackages.PackageFullName -join ", ")
    throw "Refusing to register the CutFlow development layout because a CutFlow package is installed outside this repository's build output: $packageNames"
}

Add-AppxPackage -Register $manifestPath

$layoutPath = [System.IO.Path]::GetFullPath((Split-Path -Parent $manifestPath)).TrimEnd("\", "/")
$registeredPackages = @(
    Get-AppxPackage -Name CutFlow | Where-Object {
        -not [string]::IsNullOrWhiteSpace($_.InstallLocation) -and
        [System.IO.Path]::GetFullPath($_.InstallLocation).TrimEnd("\", "/").Equals(
            $layoutPath,
            [System.StringComparison]::OrdinalIgnoreCase)
    }
)

if ($registeredPackages.Count -ne 1) {
    throw "Could not resolve the registered CutFlow development layout."
}

Start-Process "shell:AppsFolder\$($registeredPackages[0].PackageFamilyName)!App"
