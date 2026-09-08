[CmdletBinding()]
param(
    [string]$OutputDir = "ffxi-captures\ndjson",
    [int]$DurationMs = 120000,
    [int]$CheckpointMs = 120000,
    [int]$IdleMs = 180000,
    [string]$KparserCliPath = "kparser\kparser.Cli\bin\x86\Debug\kparser.cli.exe",
    [string]$Kparser2Project = "kparser2\kparser2.Cli\kparser2.Cli.fsproj"
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

if (-not (Test-Path -LiteralPath $kparserCli -PathType Leaf)) {
    throw "kparser CLI not found: $kparserCli"
}

if (-not (Test-Path -LiteralPath $kparser2ProjectPath -PathType Leaf)) {
    throw "kparser2 CLI project not found: $kparser2ProjectPath"
}

New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null

$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$chatPath = Join-Path $outputRoot "oracle_$stamp.kparser.chatlines.txt"
$packetPath = Join-Path $outputRoot "oracle_$stamp.kparser2.ndjson"
$kparserLog = Join-Path $outputRoot "oracle_$stamp.kparser.log"
$kparserError = Join-Path $outputRoot "oracle_$stamp.kparser.error.log"
$kparser2Log = Join-Path $outputRoot "oracle_$stamp.kparser2.log"
$kparser2Error = Join-Path $outputRoot "oracle_$stamp.kparser2.error.log"

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
    "$CheckpointMs"
)

Write-Host "Starting synchronized oracle capture:"
Write-Host "  kparser chatlines: $chatPath"
Write-Host "  kparser2 packets:  $packetPath"

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
    # Wait on each Process object explicitly. Wait-Process with an array of
    # ids can return after the first child on Windows PowerShell, which would
    # terminate the still-running half of the paired capture.
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
Write-Host "kparser exit=$kparserExitCode"
Write-Host "kparser2 exit=$kparser2ExitCode"
Write-Host "kparser log:  $kparserLog"
Write-Host "kparser2 log: $kparser2Log"

if ($kparserExitCode -ne 0 -or $kparser2ExitCode -ne 0) {
    exit 1
}

exit 0
