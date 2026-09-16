[CmdletBinding()]
param(
    [string]$OutputDir = "ffxi-captures\ndjson",
    [int]$DurationMs = 120000,
    [int]$CheckpointMs = 120000,
    [int]$IdleMs = 180000,
    [string]$KparserCliPath = "kparser\kparser.Cli\bin\x86\Debug\kparser.cli.exe",
    [string]$Kparser2Project = "kparser2\kparser2.Cli\kparser2.Cli.fsproj",
    [string]$UiObservations = ""
)

$ErrorActionPreference = "Stop"

if ($DurationMs -le 0) {
    throw "DurationMs must be positive."
}

if ($CheckpointMs -lt 0 -or $IdleMs -lt 0) {
    throw "CheckpointMs and IdleMs must be non-negative."
}

$root = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$outputRoot = [System.IO.Path]::GetFullPath((Join-Path $root $OutputDir))
$kparserCli = [System.IO.Path]::GetFullPath((Join-Path $root $KparserCliPath))
$kparser2ProjectPath = [System.IO.Path]::GetFullPath((Join-Path $root $Kparser2Project))
$reconcileScript = Join-Path $root "kparser2\scripts\reconcile-capture.ps1"
$compareScript = Join-Path $root "kparser2\scripts\compare-synchronized.ps1"

if (-not (Test-Path -LiteralPath $kparserCli -PathType Leaf)) {
    throw "kparser CLI not found: $kparserCli"
}

if (-not (Test-Path -LiteralPath $kparser2ProjectPath -PathType Leaf)) {
    throw "kparser2 CLI project not found: $kparser2ProjectPath"
}

New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null

$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$resetLog = Join-Path $outputRoot "oracle_reset_$stamp.log"
$resetMetadataPath = Join-Path $outputRoot "oracle_$stamp.reset.json"
$chatPath = Join-Path $outputRoot "oracle_$stamp.kparser.chatlines.txt"
$packetPath = Join-Path $outputRoot "oracle_$stamp.kparser2.ndjson"
$completePacketPath = Join-Path $outputRoot "oracle_$stamp.kparser2.complete.ndjson"
$kparserLog = Join-Path $outputRoot "oracle_$stamp.kparser.log"
$kparserError = Join-Path $outputRoot "oracle_$stamp.kparser.error.log"
$kparser2Log = Join-Path $outputRoot "oracle_$stamp.kparser2.log"
$kparser2Error = Join-Path $outputRoot "oracle_$stamp.kparser2.error.log"

Write-Host "Reading relay boundary and resetting both visible parser sessions..."
$resetLines = @(
    & dotnet run `
        --project $kparser2ProjectPath `
        --no-build `
        -- `
        ui reset `
        --descriptor-dir $outputRoot `
        --require-exact 2>&1
)
$resetExitCode = [int]$LASTEXITCODE
$resetText = [string]::Join([Environment]::NewLine, [string[]]$resetLines)
Set-Content -LiteralPath $resetLog -Value $resetText -Encoding utf8

if ($resetExitCode -ne 0) {
    throw "UI reset failed with exit code $resetExitCode. See $resetLog"
}

$boundaryMatch = [regex]::Match(
    $resetText,
    "reset boundary: mode=(\S+) quality=(\S+) session_uuid=(\S*) after_message_id=(\d+) reset_id=(\S+) boundary_utc=(\S+) reason=(.*)")

if (-not $boundaryMatch.Success) {
    throw "UI reset did not return a parseable boundary. See $resetLog"
}

$resetRecord = [ordered]@{
    schema_version = 1
    reset_id = $boundaryMatch.Groups[5].Value
    reset_boundary_utc = $boundaryMatch.Groups[6].Value
    session_uuid = $boundaryMatch.Groups[3].Value
    boundary_message_id = [UInt64]$boundaryMatch.Groups[4].Value
    boundary_mode = $boundaryMatch.Groups[1].Value
    boundary_quality = $boundaryMatch.Groups[2].Value
    boundary_reason = $boundaryMatch.Groups[7].Value.Trim()
    reset_log = $resetLog
}
$resetRecord | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $resetMetadataPath -Encoding utf8

function Quote-Argument([string]$value) {
    return '"' + ($value -replace '"', '\"') + '"'
}

$kparserArguments = @(
    "capture",
    (Quote-Argument $chatPath),
    "--duration-ms",
    "$DurationMs",
    "--checkpoint-ms",
    "$CheckpointMs"
)

$kparser2Arguments = @(
    "run",
    "--project",
    (Quote-Argument $kparser2ProjectPath),
    "--no-build",
    "--",
    "record",
    (Quote-Argument $packetPath),
    "--duration-ms",
    "$DurationMs",
    "--idle-ms",
    "$IdleMs",
    "--checkpoint-ms",
    "$CheckpointMs",
    "--boundary-mode",
    $resetRecord.boundary_mode,
    "--boundary-quality",
    $resetRecord.boundary_quality,
    "--boundary-reason",
    $resetRecord.boundary_reason,
    "--boundary-session-uuid",
    $resetRecord.session_uuid,
    "--after-message-id",
    $resetRecord.boundary_message_id
)

Write-Host "Starting post-reset synchronized capture:"
Write-Host "  kparser chatlines: $chatPath"
Write-Host "  kparser2 packets:  $packetPath"
Write-Host "  reset metadata:    $resetMetadataPath"

$kparserProcess = Start-Process `
    -FilePath $kparserCli `
    -ArgumentList $kparserArguments `
    -WorkingDirectory $root `
    -RedirectStandardOutput $kparserLog `
    -RedirectStandardError $kparserError `
    -PassThru

$kparser2Process = Start-Process `
    -FilePath "dotnet" `
    -ArgumentList $kparser2Arguments `
    -WorkingDirectory $root `
    -RedirectStandardOutput $kparser2Log `
    -RedirectStandardError $kparser2Error `
    -PassThru

try {
    $kparserProcess.WaitForExit()
    $kparser2Process.WaitForExit()
}
finally {
    if (-not $kparserProcess.HasExited) {
        Stop-Process -Id $kparserProcess.Id
    }

    if (-not $kparser2Process.HasExited) {
        Stop-Process -Id $kparser2Process.Id
    }
}

$kparserProcess.Refresh()
$kparser2Process.Refresh()
$kparserExitCode = [int]$kparserProcess.ExitCode
$kparser2ExitCode = [int]$kparser2Process.ExitCode

if ($kparserExitCode -ne 0 -or $kparser2ExitCode -ne 0) {
    throw "Synchronized capture failed: kparser=$kparserExitCode kparser2=$kparser2ExitCode"
}

& powershell -NoProfile -ExecutionPolicy Bypass -File $reconcileScript `
    -Path $packetPath `
    -Output $completePacketPath
if ($LASTEXITCODE -ne 0) {
    throw "Capture reconciliation failed with exit code $LASTEXITCODE"
}

$compareArguments = @(
    "-NoProfile",
    "-ExecutionPolicy",
    "Bypass",
    "-File",
    $compareScript,
    "-KparserChatLines",
    $chatPath,
    "-Kparser2Capture",
    $completePacketPath,
    "-OutputDir",
    $outputRoot,
    "-KparserCliPath",
    $kparserCli,
    "-Kparser2Project",
    $kparser2ProjectPath,
    "-ResetId",
    $resetRecord.reset_id,
    "-ResetBoundaryUtc",
    $resetRecord.reset_boundary_utc,
    "-SessionUuid",
    $resetRecord.session_uuid,
    "-BoundaryMessageId",
    $resetRecord.boundary_message_id,
    "-BoundaryMode",
    $resetRecord.boundary_mode,
    "-BoundaryQuality",
    $resetRecord.boundary_quality,
    "-BoundaryReason",
    $resetRecord.boundary_reason,
    "-RequireExactBoundary"
)

if (-not [string]::IsNullOrWhiteSpace($UiObservations)) {
    $compareArguments += @("-UiObservations", $UiObservations)
}

& powershell @compareArguments
if ($LASTEXITCODE -ne 0) {
    throw "Synchronized comparison failed with exit code $LASTEXITCODE"
}

Write-Host "reset metadata: $resetMetadataPath"
Write-Host "complete capture: $completePacketPath"
