param(
    [string]$CliProject = "",
    [string]$OutputDir = "",
    [int]$DurationMs = 1200000,
    [int]$IdleMs = 180000,
    [int]$CheckpointMs = 120000,
    [string]$KparserJson = "",
    [switch]$AssertCombat,
    [switch]$AssertChat,
    [switch]$AssertNames,
    [int]$MinBattles = 0,
    [int]$MinChat = 0,
    [string[]]$SkipCode = @()
)

$ErrorActionPreference = "Stop"

$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$kparser2Root = Split-Path -Parent $scriptDirectory
$repoRoot = Split-Path -Parent $kparser2Root

if ([string]::IsNullOrWhiteSpace($CliProject)) {
    $CliProject = Join-Path $kparser2Root "kparser2.Cli\kparser2.Cli.fsproj"
}

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $repoRoot "ffxi-captures\ndjson"
}

if (-not (Test-Path -LiteralPath $CliProject -PathType Leaf)) {
    throw "kparser2 CLI project not found: $CliProject"
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$capturePath = Join-Path $OutputDir "parity_$stamp.ndjson"
$heatStatePath = Join-Path $OutputDir "parity_$stamp.heat.json"
$latestStatusPath = Join-Path $OutputDir "parity_latest.json"
$heatScript = Join-Path $scriptDirectory "opcode-heat.ps1"
$reconcileScript = Join-Path $scriptDirectory "reconcile-capture.ps1"
$compareScript = Join-Path $scriptDirectory "compare-parity.ps1"
$overallFailure = $false
$sawStop = $false

function Quote-ProcessArgument {
    param([string]$Value)

    return '"' + ($Value -replace '"', '\"') + '"'
}

function Invoke-AnalyticsSnapshot {
    param(
        [string]$InputPath,
        [string]$ParityPath,
        [string]$LogPath
    )

    $arguments = @(
        "run",
        "--project", $CliProject,
        "--no-build",
        "--",
        "analytics", "snapshot", $InputPath,
        "--parity",
        "--output", $ParityPath,
        "--assert-settled"
    )

    if ($AssertCombat) {
        $arguments += "--assert-combat"
    }
    if ($AssertChat) {
        $arguments += "--assert-chat"
    }
    if ($AssertNames) {
        $arguments += "--assert-names"
    }
    if ($MinBattles -gt 0) {
        $arguments += @("--min-battles", [string]$MinBattles)
    }
    if ($MinChat -gt 0) {
        $arguments += @("--min-chat", [string]$MinChat)
    }
    foreach ($code in $SkipCode) {
        if (-not [string]::IsNullOrWhiteSpace($code)) {
            $arguments += @("--skip-code", $code)
        }
    }

    & dotnet @arguments *> $LogPath
    return $LASTEXITCODE
}

function Invoke-Checkpoint {
    param(
        [string]$Reason,
        [int]$Number
    )

    $heatLines = @(
        & $heatScript `
            -Path $capturePath `
            -Previous $heatStatePath `
            -Output $heatStatePath 2>&1
    )
    $heatExit = $LASTEXITCODE
    $heatText = $heatLines -join [Environment]::NewLine
    $heatLabel =
        if ($heatExit -eq 0) {
            "HEAT unchanged"
        }
        elseif ($heatExit -eq 1) {
            "HEAT changed"
        }
        else {
            "HEAT unavailable"
        }
    $heat = [pscustomobject]@{
        status = $heatLabel
        output = [System.IO.Path]::GetFullPath($heatStatePath)
        log = $heatText
    }

    $needsSnapshot = (
        ($Number -eq 1) -or
        ($heatExit -eq 1) -or
        ($Reason -eq "recording stopped") -or
        ($Reason -eq "recording completed")
    )
    $status = [ordered]@{
        schema_version = 1
        checkpoint = $Number
        reason = $Reason
        capture = [System.IO.Path]::GetFullPath($capturePath)
        heat = $heat
        reconciled = $false
        snapshot_exit_code = $null
        parity_exit_code = $null
        generated_at = [DateTimeOffset]::UtcNow.ToString("O")
    }

    if ($needsSnapshot) {
        $reconciledPath = Join-Path $OutputDir "parity_$stamp.checkpoint_$('{0:D3}' -f $Number).ndjson"
        $reconcileOutput = @(& $reconcileScript -Path $capturePath -Output $reconciledPath 2>&1)

        $status.reconciled = $true
        $status.reconcile_log = $reconcileOutput -join [Environment]::NewLine
        if ($status.reconcile_log -match "dropped=(\d+)") {
            $status.reconcile_dropped_lines = [int]$Matches[1]
        }
        $status.reconciled_capture = [System.IO.Path]::GetFullPath($reconciledPath)
        $parityPath = Join-Path $OutputDir "parity_$stamp.checkpoint_$('{0:D3}' -f $Number).json"
        $logPath = Join-Path $OutputDir "parity_$stamp.checkpoint_$('{0:D3}' -f $Number).log"
        $status.snapshot_exit_code = Invoke-AnalyticsSnapshot $reconciledPath $parityPath $logPath
        if ($status.snapshot_exit_code -ne 0) {
            $script:overallFailure = $true
        }

        $hasParityInput =
            (-not [string]::IsNullOrWhiteSpace($KparserJson)) -and
            (Test-Path -LiteralPath $parityPath -PathType Leaf)
        if ($hasParityInput) {
            $comparePath = Join-Path $OutputDir "parity_$stamp.checkpoint_$('{0:D3}' -f $Number).compare.json"
            $compareOutput = @(
                & $compareScript `
                    -KparserJson $KparserJson `
                    -Kparser2Json $parityPath `
                    -OutputPath $comparePath 2>&1
            )
            $status.parity_exit_code = $LASTEXITCODE
            if ($status.parity_exit_code -ne 0) {
                $script:overallFailure = $true
            }
            $status.parity_report = [System.IO.Path]::GetFullPath($comparePath)
        }
        else {
            $status.parity = "not-run: no kparser JSON supplied"
        }

        $statusJson = $status | ConvertTo-Json -Depth 12
        Set-Content -LiteralPath $latestStatusPath -Value $statusJson -Encoding utf8
        $reportPath = Join-Path $OutputDir "parity_$stamp.checkpoint_$('{0:D3}' -f $Number).status.json"
        Set-Content -LiteralPath $reportPath -Value $statusJson -Encoding utf8
        Write-Host "parity checkpoint $Number reason=$Reason heat=$($heat.status) snapshot_exit=$($status.snapshot_exit_code)"
    }
    else {
        $statusJson = $status | ConvertTo-Json -Depth 12
        Set-Content -LiteralPath $latestStatusPath -Value $statusJson -Encoding utf8
        Write-Host "parity checkpoint $Number reason=$Reason heat=$($heat.status) skipped=unchanged"
    }
}

$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = "dotnet"
$psi.Arguments = @(
    "run",
    "--project", (Quote-ProcessArgument $CliProject),
    "--no-build",
    "--",
    "record",
    (Quote-ProcessArgument $capturePath),
    "--duration-ms", $DurationMs,
    "--idle-ms", $IdleMs,
    "--checkpoint-ms", $CheckpointMs
) -join " "
$psi.UseShellExecute = $false
$psi.CreateNoWindow = $true
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $true

$process = New-Object System.Diagnostics.Process
$process.StartInfo = $psi
$process.Start() | Out-Null

$checkpointNumber = 0
try {
    while (($line = $process.StandardOutput.ReadLine()) -ne $null) {
        Write-Host "[record] $line"

        if ($line -match "record checkpoint:") {
            $checkpointNumber++
            Invoke-Checkpoint "record checkpoint" $checkpointNumber
        }
        elseif ($line -match "recording stopped:") {
            $sawStop = $true
            $checkpointNumber++
            Invoke-Checkpoint "recording stopped" $checkpointNumber
        }
    }

    $stderr = $process.StandardError.ReadToEnd()
    $process.WaitForExit()
    if (-not [string]::IsNullOrWhiteSpace($stderr)) {
        Write-Host "[record:stderr] $stderr"
    }
}
catch {
    if (-not $process.HasExited) {
        $process.Kill()
        $process.WaitForExit()
    }
    throw
}
finally {
    $recordExitCode = $process.ExitCode
    $process.Dispose()
}

if (-not $sawStop) {
    $checkpointNumber++
    Invoke-Checkpoint "recording completed" $checkpointNumber
}

if ($recordExitCode -ne 0) {
    throw "record exited with code $recordExitCode"
}

if ($overallFailure) {
    throw "parity validation failed; inspect $latestStatusPath"
}

Write-Host "parity capture: $capturePath"
Write-Host "latest status: $latestStatusPath"
