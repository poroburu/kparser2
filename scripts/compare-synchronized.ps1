[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$KparserChatLines,
    [Parameter(Mandatory = $true)]
    [string]$Kparser2Capture,
    [string]$OutputDir = "",
    [string]$KparserCliPath = "kparser\kparser.Cli\bin\x86\Debug\kparser.cli.exe",
    [string]$Kparser2Project = "kparser2\kparser2.Cli\kparser2.Cli.fsproj",
    [string]$UiObservations = "",
    [string]$UiRun = "",
    [switch]$RequireUiEvidence,
    [string]$ResetId = "",
    [string]$ResetBoundaryUtc = "",
    [string]$SessionUuid = "",
    [UInt64]$BoundaryMessageId = 0,
    [ValidateSet("exact", "degraded", "none")]
    [string]$BoundaryMode = "none",
    [string]$BoundaryQuality = "unavailable",
    [string]$BoundaryReason = "",
    [switch]$RequireExactBoundary
)

$ErrorActionPreference = "Stop"

$root = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path

function Resolve-RootPath([string]$Path) {
    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $root $Path))
}

. (Join-Path $PSScriptRoot "parity-evidence.ps1")

$chatPath = Resolve-RootPath $KparserChatLines
$capturePath = Resolve-RootPath $Kparser2Capture
$kparserCli = Resolve-RootPath $KparserCliPath
$kparser2ProjectPath = Resolve-RootPath $Kparser2Project

foreach ($path in @($chatPath, $capturePath, $kparserCli, $kparser2ProjectPath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required input not found: $path"
    }
}

$captureBoundary = Read-CaptureBoundary $capturePath
if ($RequireExactBoundary) {
    Assert-ExactBoundary -CaptureBoundary $captureBoundary -SessionUuid $SessionUuid `
        -BoundaryMessageId $BoundaryMessageId -BoundaryMode $BoundaryMode `
        -BoundaryQuality $BoundaryQuality -HasMessageId ($PSBoundParameters.ContainsKey("BoundaryMessageId"))
}
if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Split-Path -Parent $capturePath
}
else {
    $OutputDir = Resolve-RootPath $OutputDir
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
$base = [System.IO.Path]::GetFileNameWithoutExtension($capturePath)
$kparserJson = Join-Path $OutputDir "$base.kparser.json"
$kparser2Json = Join-Path $OutputDir "$base.kparser2.json"
$parityJson = Join-Path $OutputDir "$base.parity.json"
$reportJson = Join-Path $OutputDir "$base.report-compare.json"
$uiComparisonJson = Join-Path $OutputDir "$base.ui-compare.json"
$uiInput = Read-UiObservations $UiObservations
$uiRunInput = if ($UiRun) { Read-UiRun -Path (Resolve-RootPath $UiRun) -CapturePath $capturePath } else { $null }
if ($RequireUiEvidence -and $null -eq $uiRunInput) { throw 'RequireUiEvidence needs a WPF replay manifest supplied with -UiRun.' }
Assert-ObservationWindow -Rows @($uiInput.rows) -ResetId $ResetId -SessionUuid $SessionUuid -BoundaryMessageId $BoundaryMessageId -BoundaryMode $BoundaryMode -CapturePath $capturePath

& $kparserCli snapshot $chatPath --json --output $kparserJson
if ($LASTEXITCODE -ne 0) {
    throw "kparser snapshot failed with exit code $LASTEXITCODE"
}

& dotnet run --project $kparser2ProjectPath --no-build -- analytics snapshot $capturePath --json --output $kparser2Json
if ($LASTEXITCODE -ne 0) {
    throw "kparser2 snapshot failed with exit code $LASTEXITCODE"
}

& dotnet run --project $kparser2ProjectPath --no-build -- analytics snapshot $capturePath --parity --output $parityJson
if ($LASTEXITCODE -ne 0) {
    throw "kparser2 parity projection failed with exit code $LASTEXITCODE"
}

& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "compare-parity.ps1") `
    -KparserJson $kparserJson -Kparser2Json $parityJson `
    -OutputPath (Join-Path $OutputDir "$base.parity-compare.json")
$parityExit = $LASTEXITCODE

& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "compare-reports.ps1") `
    -KparserJson $kparserJson -Kparser2Json $kparser2Json `
    -OutputPath $reportJson
$reportExit = $LASTEXITCODE

$uiComparison = $null
if (-not [string]::IsNullOrWhiteSpace($UiObservations)) {
    $uiComparison = New-UiComparison `
        -Path $uiInput.path `
        -Rows $uiInput.rows `
        -ResetId $ResetId `
        -ResetBoundaryUtc $ResetBoundaryUtc `
        -SessionUuid $SessionUuid `
        -BoundaryMessageId $BoundaryMessageId `
        -BoundaryMode $BoundaryMode `
        -BoundaryQuality $BoundaryQuality `
        -BoundaryReason $BoundaryReason
    $uiComparison | ConvertTo-Json -Depth 16 | Set-Content -LiteralPath $uiComparisonJson -Encoding utf8
    Write-Host "ui observations:  $($uiInput.path)"
    Write-Host "ui comparison:    $uiComparisonJson"
    Write-Host "ui semantic=$($uiComparison.semantic_count) rendering-only=$($uiComparison.rendering_only_count) deferred=$($uiComparison.deferred_count) unclassified=$($uiComparison.unclassified_count)"
}

Write-Host "kparser snapshot:  $kparserJson"
Write-Host "kparser2 snapshot: $kparser2Json"
Write-Host "parity projection: $parityJson"
Write-Host "report comparison:  $reportJson"
$qa = New-QaSummary -ParityExit $parityExit -ReportExit $reportExit -Rows @($uiInput.rows) -Boundary $captureBoundary -UiRun $uiRunInput
$qa.sources = [ordered]@{ capture = $capturePath; chatlines = $chatPath; observations = $uiInput.path }
$qa.reset_id = $ResetId
$qa.reset_boundary_utc = $ResetBoundaryUtc
$qa | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $OutputDir "$base.qa.json") -Encoding utf8
Write-Host "state=$($qa.state.status) ui=$($qa.ui.status) human=$($qa.human.status)"
Write-Host "parity exit=$parityExit report exit=$reportExit require-ui=$RequireUiEvidence"

if ($parityExit -ne 0 -or $reportExit -ne 0 -or ($RequireUiEvidence -and $qa.ui.status -ne 'passed')) {
    exit 1
}

exit 0
