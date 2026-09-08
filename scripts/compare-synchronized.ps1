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

function Read-UiObservations {
    param([string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return @()
    }

    if ([System.IO.Path]::IsPathRooted($Path)) {
        $resolvedPath = [System.IO.Path]::GetFullPath($Path)
    }
    else {
        $resolvedPath = [System.IO.Path]::GetFullPath((Join-Path $root $Path))
    }

    if (-not (Test-Path -LiteralPath $resolvedPath -PathType Leaf)) {
        throw "UI observation file not found: $resolvedPath"
    }

    $rows = @()
    $lineNumber = 0
    foreach ($line in Get-Content -LiteralPath $resolvedPath) {
        $lineNumber++
        if ([string]::IsNullOrWhiteSpace($line)) {
            continue
        }

        try {
            $row = $line | ConvertFrom-Json
        }
        catch {
            throw "Invalid UI observation JSON at $resolvedPath line $lineNumber`: $($_.Exception.Message)"
        }

        foreach ($propertyName in @("observed_at_utc", "application", "surface", "classification", "summary")) {
            if ($null -eq $row.PSObject.Properties[$propertyName] -or
                [string]::IsNullOrWhiteSpace([string]$row.$propertyName)) {
                throw "UI observation at $resolvedPath line $lineNumber is missing '$propertyName'."
            }
        }

        $rows += $row
    }

    return [pscustomobject]@{
        path = $resolvedPath
        rows = @($rows)
    }
}

function Read-CaptureBoundary {
    param([string]$Path)

    $line = Get-Content -LiteralPath $Path -TotalCount 1
    if ([string]::IsNullOrWhiteSpace($line)) {
        return [pscustomobject]@{
            mode = "none"
            quality = "unavailable"
            reason = "capture has no session header"
            session_uuid = ""
            message_id = 0
        }
    }

    try {
        $header = $line | ConvertFrom-Json
    }
    catch {
        return [pscustomobject]@{
            mode = "none"
            quality = "unavailable"
            reason = "capture session header is invalid"
            session_uuid = ""
            message_id = 0
        }
    }

    if ([string]$header.type -ne "kparser2.session") {
        return [pscustomobject]@{
            mode = "none"
            quality = "unavailable"
            reason = "capture session header is missing"
            session_uuid = ""
            message_id = 0
        }
    }

    return [pscustomobject]@{
        mode = if ($header.boundary_mode) { [string]$header.boundary_mode } else { "none" }
        quality = if ($header.boundary_quality) { [string]$header.boundary_quality } else { "unavailable" }
        reason = if ($header.boundary_reason) { [string]$header.boundary_reason } else { "" }
        session_uuid = if ($header.boundary_session_uuid) { [string]$header.boundary_session_uuid } else { "" }
        message_id = if ($null -ne $header.boundary_message_id) { [UInt64]$header.boundary_message_id } else { [UInt64]0 }
    }
}

function New-UiComparison {
    param(
        [string]$Path,
        [object[]]$Rows,
        [string]$ResetId,
        [string]$ResetBoundaryUtc,
        [string]$SessionUuid,
        [UInt64]$BoundaryMessageId,
        [string]$BoundaryMode,
        [string]$BoundaryQuality,
        [string]$BoundaryReason
    )

    $semanticClassifications = @(
        "kparser-only",
        "kparser2-missing",
        "kparser2-extra"
    )
    $renderingRows = @($Rows | Where-Object {
        [string]$_.classification -ieq "rendering-only"
    })
    $semanticRows = @($Rows | Where-Object {
        $semanticClassifications -contains ([string]$_.classification).ToLowerInvariant()
    })
    $deferredRows = @($Rows | Where-Object {
        [string]$_.classification -ieq "deferred"
    })
    $unclassifiedRows = @($Rows | Where-Object {
        [string]$_.classification -ieq "unclassified"
    })

    $byClassification = [ordered]@{}
    foreach ($row in @($Rows)) {
        $classification = [string]$row.classification
        if (-not $byClassification.Contains($classification)) {
            $byClassification[$classification] = 0
        }
        $byClassification[$classification]++
    }

    return [ordered]@{
        schema_version = 1
        observations_path = $Path
        reset_id = $ResetId
        reset_boundary_utc = $ResetBoundaryUtc
        session_uuid = $SessionUuid
        boundary_message_id = $BoundaryMessageId
        boundary_mode = $BoundaryMode
        boundary_quality = $BoundaryQuality
        boundary_reason = $BoundaryReason
        observation_count = @($Rows).Count
        rendering_only_count = $renderingRows.Count
        semantic_count = $semanticRows.Count
        deferred_count = $deferredRows.Count
        unclassified_count = $unclassifiedRows.Count
        by_classification = $byClassification
        rendering_only = @($renderingRows)
        semantic = @($semanticRows)
        deferred = @($deferredRows)
        unclassified = @($unclassifiedRows)
    }
}

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
if ($RequireExactBoundary -and $captureBoundary.mode -ne "exact") {
    throw "Exact comparison requires an exact capture boundary; found '$($captureBoundary.mode)': $($captureBoundary.reason)"
}
if ($RequireExactBoundary -and $BoundaryMode -ne "exact") {
    throw "Exact comparison requires reset boundary_mode=exact; found '$BoundaryMode'."
}
if ($RequireExactBoundary -and $captureBoundary.quality -ne "exact") {
    throw "Exact comparison requires exact boundary quality; found '$($captureBoundary.quality)': $($captureBoundary.reason)"
}
if ($RequireExactBoundary -and
    -not [string]::IsNullOrWhiteSpace($SessionUuid) -and
    $captureBoundary.session_uuid -ne $SessionUuid) {
    throw "Capture boundary session_uuid '$($captureBoundary.session_uuid)' does not match reset session_uuid '$SessionUuid'."
}
if ($RequireExactBoundary -and
    $BoundaryMessageId -gt 0 -and
    [UInt64]$captureBoundary.message_id -ne $BoundaryMessageId) {
    throw "Capture boundary message_id '$($captureBoundary.message_id)' does not match reset after_message_id '$BoundaryMessageId'."
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

$uiExit = 0
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
Write-Host "parity exit=$parityExit report exit=$reportExit ui exit=$uiExit"

if ($parityExit -ne 0 -or $reportExit -ne 0) {
    exit 1
}

exit 0
