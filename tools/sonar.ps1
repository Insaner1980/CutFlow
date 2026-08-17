#Requires -Version 5.1

[CmdletBinding()]
param(
    [switch]$PlanOnly,
    [switch]$AllowExternalUpload,
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$SonarArgs
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new()
$OutputEncoding = [Console]::OutputEncoding

function Invoke-DotNetCommand {
    param(
        [Parameter(Mandatory)]
        [string[]]$Arguments,
        [Parameter(Mandatory)]
        [string]$ReportPath
    )

    & dotnet @Arguments 2>&1 |
        Tee-Object -FilePath $ReportPath -Append |
        Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet-komento epäonnistui (exit $LASTEXITCODE)."
    }
}

if ($SonarArgs.Count -gt 0) {
    $cli = Get-Command sonar.exe -CommandType Application -ErrorAction SilentlyContinue
    if ($null -eq $cli) {
        throw 'sonar.exe ei löytynyt PATHista.'
    }

    & $cli.Source @SonarArgs
    exit $(if ($null -ne $global:LASTEXITCODE) { [int]$global:LASTEXITCODE } else { 0 })
}

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$reportsDir = Join-Path $repoRoot 'reports'
$reportPath = Join-Path $reportsDir 'sonar.txt'
$testResultsDir = Join-Path $reportsDir 'sonar-test-results'
$projectKey = 'Insaner1980_CutFlow'
$organization = 'insaner1980'

if ($PlanOnly) {
    Write-Output @(
        'sonar'
        '  - dotnet restore + Release/x64 build + MSTest/OpenCover + SonarQube Cloud upload'
        '  - requires SONAR_TOKEN'
        '  - actual external upload requires -AllowExternalUpload'
        "  - project: $projectKey"
        '  - host: https://sonarcloud.io'
    )
    exit 0
}

if (-not $AllowExternalUpload) {
    throw 'Sonar-analyysi lähettää analyysituloksen SonarQube Cloudiin. Käytä -AllowExternalUpload.'
}

if ([string]::IsNullOrWhiteSpace($env:SONAR_TOKEN)) {
    throw 'SONAR_TOKEN ei ole asetettu tälle PowerShell-istunnolle.'
}

New-Item -ItemType Directory -Force -Path $reportsDir | Out-Null
if ((Split-Path -Parent $testResultsDir) -ne $reportsDir) {
    throw 'Sonar-testitulosten polku ei ole reports-kansion alla.'
}
if (Test-Path -LiteralPath $testResultsDir) {
    Remove-Item -LiteralPath $testResultsDir -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $testResultsDir | Out-Null
Set-Content -LiteralPath $reportPath -Encoding utf8 -Value @(
    'sonar'
    "Root: $repoRoot"
    "Project: $projectKey"
    "Started: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    ''
)

Push-Location -LiteralPath $repoRoot
try {
    Invoke-DotNetCommand -ReportPath $reportPath -Arguments @(
        'tool', 'restore'
    )
    Invoke-DotNetCommand -ReportPath $reportPath -Arguments @(
        'restore', 'CutFlow.slnx', '-p:Platform=x64'
    )
    Invoke-DotNetCommand -ReportPath $reportPath -Arguments @(
        'tool', 'run', 'dotnet-sonarscanner', '--', 'begin'
        "/k:$projectKey"
        "/o:$organization"
        "/d:sonar.token=$env:SONAR_TOKEN"
        '/d:sonar.cs.opencover.reportsPaths=reports/sonar-test-results/**/coverage.opencover.xml'
        '/d:sonar.cs.vstest.reportsPaths=reports/sonar-test-results/*.trx'
        '/d:sonar.exclusions=**/Assets/**,**/TestMedia/**'
        '/d:sonar.coverage.exclusions=**/*.xaml.cs,**/EditorView.Export.cs,**/FilePickerHelper.cs,**/*.ps1'
    )
    Invoke-DotNetCommand -ReportPath $reportPath -Arguments @(
        'build', 'CutFlow.slnx', '-c', 'Release', '-p:Platform=x64', '--no-restore', '--no-incremental'
    )
    Invoke-DotNetCommand -ReportPath $reportPath -Arguments @(
        'test', 'tests\CutFlow.Tests\CutFlow.Tests.csproj'
        '-c', 'Release'
        '-p:Platform=x64'
        '--no-restore'
        '--no-build'
        '--results-directory', 'reports\sonar-test-results'
        '--logger', 'trx;LogFileName=sonar.trx'
        '--collect', 'XPlat Code Coverage'
        '--'
        'DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover'
    )
    Invoke-DotNetCommand -ReportPath $reportPath -Arguments @(
        'tool', 'run', 'dotnet-sonarscanner', '--', 'end'
        "/d:sonar.token=$env:SONAR_TOKEN"
    )
}
finally {
    Pop-Location
}
