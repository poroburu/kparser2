[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$CapturePath,
    [string]$ObservationPath = "",
    [string]$Timestamp = "",
    [string]$ResetId = "",
    [string]$ResetBoundaryUtc = "",
    [string]$SessionUuid = "",
    [string]$BoundaryMessageId = "",
    [ValidateSet("exact", "degraded", "none")]
    [string]$BoundaryMode = "none",
    [string]$BoundaryQuality = "unavailable",
    [string]$BoundaryReason = "",
    [Parameter(Mandatory = $true)]
    [ValidateSet("kparser", "kparser2")]
    [string]$Application,
    [Parameter(Mandatory = $true)]
    [string]$Surface,
    [Parameter(Mandatory = $true)]
    [ValidateSet(
        "kparser-only",
        "kparser2-missing",
        "kparser2-extra",
        "rendering-only",
        "deferred",
        "unclassified"
    )]
    [string]$Classification,
    [Parameter(Mandatory = $true)]
    [string]$Summary,
    [string]$ActorName = "",
    [string]$TargetName = "",
    [string]$Category = "",
    [string]$Amount = "",
    [string]$Success = "",
    [string]$Chat = "",
    [string]$ReportTotal = "",
    [string]$Notes = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path

function Resolve-RepoPath([string]$Path) {
    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $repoRoot $Path))
}

function Convert-OptionalNumber([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) {
        return $null
    }

    $number = 0
    if (-not [int]::TryParse($Value, [Globalization.NumberStyles]::Integer, [Globalization.CultureInfo]::InvariantCulture, [ref]$number)) {
        throw "Expected an integer value, got '$Value'."
    }

    return $number
}

function Convert-OptionalUInt64([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) {
        return $null
    }

    $number = [UInt64]0
    if (-not [UInt64]::TryParse($Value, [Globalization.NumberStyles]::Integer, [Globalization.CultureInfo]::InvariantCulture, [ref]$number)) {
        throw "Expected an unsigned integer value, got '$Value'."
    }

    return $number
}

$captureFullPath = Resolve-RepoPath $CapturePath
if (-not (Test-Path -LiteralPath $captureFullPath -PathType Leaf)) {
    throw "Capture file not found: $captureFullPath"
}

if ([string]::IsNullOrWhiteSpace($ObservationPath)) {
    $observationFullPath = "$captureFullPath.ui-observations.jsonl"
}
else {
    $observationFullPath = Resolve-RepoPath $ObservationPath
}

if ([string]::IsNullOrWhiteSpace($Timestamp)) {
    $observedAt = [DateTimeOffset]::UtcNow
}
else {
    try {
        $observedAt = [DateTimeOffset]::Parse(
            $Timestamp,
            [Globalization.CultureInfo]::InvariantCulture,
            [Globalization.DateTimeStyles]::AssumeUniversal)
    }
    catch {
        throw "Timestamp must be an ISO-8601 date/time: $Timestamp"
    }
}

$record = [ordered]@{
    schema_version = 1
    observed_at_utc = $observedAt.ToUniversalTime().ToString("O")
    observed_at_local = $observedAt.ToLocalTime().ToString("O")
    capture_path = $captureFullPath
    reset_id = $ResetId
    reset_boundary_utc = $ResetBoundaryUtc
    session_uuid = $SessionUuid
    boundary_message_id = Convert-OptionalUInt64 $BoundaryMessageId
    boundary_mode = $BoundaryMode
    boundary_quality = $BoundaryQuality
    boundary_reason = $BoundaryReason
    application = $Application
    surface = $Surface
    classification = $Classification
    summary = $Summary
    displayed = [ordered]@{
        actor_name = $ActorName
        target_name = $TargetName
        category = $Category
        amount = Convert-OptionalNumber $Amount
        success = $Success
        chat = $Chat
        report_total = Convert-OptionalNumber $ReportTotal
    }
    notes = $Notes
}

$directory = Split-Path -Parent $observationFullPath
if (-not [string]::IsNullOrWhiteSpace($directory)) {
    New-Item -ItemType Directory -Force -Path $directory | Out-Null
}

$json = $record | ConvertTo-Json -Compress -Depth 8
$utf8 = New-Object System.Text.UTF8Encoding -ArgumentList $false
[System.IO.File]::AppendAllText(
    $observationFullPath,
    $json + [Environment]::NewLine,
    $utf8)

Write-Host "ui observation appended: $observationFullPath"
Write-Host "classification=$Classification application=$Application surface=$Surface"
