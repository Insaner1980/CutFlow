[CmdletBinding()]
param(
    [switch]$RegisterAndLaunch
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$repositoryPrefix = $repositoryRoot.TrimEnd("\", "/") + [System.IO.Path]::DirectorySeparatorChar
$solutionPath = Join-Path $repositoryRoot "CutFlow.slnx"

function Invoke-DotNet {
    param([string[]]$CommandArguments)

    & dotnet @CommandArguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($CommandArguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

$generatedDirectories = @(
    ".sonarqube",
    "src\CutFlow\bin",
    "src\CutFlow\obj",
    "tests\CutFlow.Tests\bin",
    "tests\CutFlow.Tests\obj"
)

foreach ($relativePath in $generatedDirectories) {
    $generatedPath = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $relativePath))
    if (-not $generatedPath.StartsWith($repositoryPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove a generated directory outside the CutFlow repository: $generatedPath"
    }

    if (Test-Path -LiteralPath $generatedPath) {
        Remove-Item -LiteralPath $generatedPath -Recurse -Force
    }
}

Push-Location $repositoryRoot
try {
    Invoke-DotNet @("restore", $solutionPath, "-p:Platform=x64")
    Invoke-DotNet @("test", $solutionPath, "-c", "Release", "-p:Platform=x64", "--no-restore")
    Invoke-DotNet @("build", $solutionPath, "-c", "Release", "-p:Platform=x64", "--no-restore", "--no-incremental")

    if ($RegisterAndLaunch) {
        & (Join-Path $PSScriptRoot "Register-CutFlowDevelopment.ps1") -Configuration Release
    }
}
finally {
    Pop-Location
}
