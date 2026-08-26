param(
    [string]$Cli = (Join-Path $PSScriptRoot "..\kparser2.Cli\bin\Debug\net8.0\kparser2.Cli.exe"),
    [string]$PreviousUuid = "",
    [int]$TimeoutMs = 180000,
    [int]$IntervalMs = 5000
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $Cli)) {
    throw "kparser2.Cli.exe not found: $Cli"
}

function Get-KpacketHello {
    $text = & $Cli hello 2>&1 | Out-String
    if ($text -match "Unable to reach") {
        return $null
    }
    return $text.Trim()
}

function Get-SessionUuid([string]$helloText) {
    try {
        $j = $helloText | ConvertFrom-Json
        if ($null -ne $j.session_uuid) { return [string]$j.session_uuid }
    } catch { }
    if ($helloText -match '"session_uuid"\s*:\s*"([^"]+)"') {
        return $Matches[1]
    }
    return ""
}

$deadline = [datetime]::UtcNow.AddMilliseconds($TimeoutMs)

while ([datetime]::UtcNow -le $deadline) {
    $hello = Get-KpacketHello
    if ($null -ne $hello) {
        $uuid = Get-SessionUuid $hello
        $isNew = [string]::IsNullOrWhiteSpace($PreviousUuid) -or ($uuid -ne $PreviousUuid)
        if ($isNew) {
            Write-Host "kpacket session ready uuid=$uuid"
            Write-Output $uuid
            exit 0
        }
        Write-Host "kpacket still uuid=$uuid (waiting for a new session)"
    } else {
        Write-Host "kpacket :5556 offline"
    }
    Start-Sleep -Milliseconds $IntervalMs
}

Write-Host "timed out waiting for kpacket"
exit 2
