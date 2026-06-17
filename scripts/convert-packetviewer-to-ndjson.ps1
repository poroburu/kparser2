param(
    [string[]]$InputLogs = @(),
    [string]$FullLog = "",
    [string]$IncomingLog = "",
    [string]$OutgoingLog = "",
    [Parameter(Mandatory = $true)]
    [string]$OutputNdjson,
    [string]$SessionId = ""
)

$ErrorActionPreference = "Stop"

function Parse-PacketViewerLog {
    param([string]$Path, [string]$Direction)

    $lines = Get-Content -LiteralPath $Path
    $packets = New-Object System.Collections.Generic.List[object]
    $byteList = New-Object System.Collections.Generic.List[byte]
    $timestamp = $null
    $packetDirection = $Direction
    $pastMarker = $false
    $inPacket = $false

    foreach ($raw in $lines) {
        $line = $raw.TrimEnd("`r")
        $lower = $line.ToLower().Trim()

        if ($line -match '^\[(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2})\](.*)$') {
            if ($byteList.Count -ge 4) {
                $packets.Add([pscustomobject]@{
                    Timestamp = $timestamp
                    Direction = $packetDirection
                    Data      = [byte[]]$byteList.ToArray()
                })
            }
            $timestamp = [datetime]::ParseExact($Matches[1], 'yyyy-MM-dd HH:mm:ss', $null)
            $headerTail = $Matches[2].ToLower()
            if ($headerTail -match 'incoming') { $packetDirection = 'incoming' }
            elseif ($headerTail -match 'outgoing') { $packetDirection = 'outgoing' }
            else { $packetDirection = $Direction }
            $byteList.Clear()
            $pastMarker = $false
            $inPacket = $true
            continue
        }

        if (-not $inPacket) { continue }

        if (-not $pastMarker) {
            if ($lower -match '-{10,}') { $pastMarker = $true }
            continue
        }

        if ([string]::IsNullOrWhiteSpace($line)) {
            if ($byteList.Count -ge 4) {
                $packets.Add([pscustomobject]@{
                    Timestamp = $timestamp
                    Direction = $packetDirection
                    Data      = [byte[]]$byteList.ToArray()
                })
            }
            $byteList.Clear()
            $pastMarker = $false
            $inPacket = $false
            continue
        }

        $simple = $line -replace '[ \t]', ''
        $pipe = $simple.IndexOf('|')
        if ($pipe -lt 0) { continue }
        $hex = $simple.Substring($pipe + 1, [Math]::Min(32, $simple.Length - $pipe - 1))
        for ($i = 0; $i -lt 16; $i++) {
            $pair = $hex.Substring($i * 2, 2)
            if ($pair -ne '--') {
                $byteList.Add([Convert]::ToByte($pair, 16))
            }
        }
    }

    if ($byteList.Count -ge 4) {
        $packets.Add([pscustomobject]@{
            Timestamp = $timestamp
            Direction = $packetDirection
            Data      = [byte[]]$byteList.ToArray()
        })
    }

    return $packets
}

$logs = [System.Collections.Generic.List[string]]::new()
foreach ($log in $InputLogs) {
    if (-not [string]::IsNullOrWhiteSpace($log)) { $logs.Add($log) }
}
if (-not [string]::IsNullOrWhiteSpace($FullLog)) { $logs.Add($FullLog) }
if (-not [string]::IsNullOrWhiteSpace($IncomingLog)) { $logs.Add($IncomingLog) }
if (-not [string]::IsNullOrWhiteSpace($OutgoingLog)) { $logs.Add($OutgoingLog) }

if ($logs.Count -eq 0) {
    throw "No input logs specified. Use -FullLog, -IncomingLog/-OutgoingLog, or -InputLogs."
}

if ([string]::IsNullOrWhiteSpace($SessionId)) {
    $SessionId = [System.IO.Path]::GetFileNameWithoutExtension($OutputNdjson)
}

$all = New-Object System.Collections.Generic.List[object]
$msgId = 1

foreach ($log in $logs) {
    if (-not (Test-Path -LiteralPath $log)) {
        throw "Missing log: $log"
    }
    $dir =
        if ($log -match '\\incoming\\' -or $log -match 'incoming\.log$') { 'incoming' }
        elseif ($log -match '\\outgoing\\' -or $log -match 'outgoing\.log$') { 'outgoing' }
        else { 'incoming' }
    $parsed = Parse-PacketViewerLog -Path $log -Direction $dir
    foreach ($p in $parsed) { $all.Add($p) | Out-Null }
}

$sorted = $all | Sort-Object Timestamp, { $_.Data[0] }, { $_.Data[1] }
$epoch = [datetime]'1970-01-01T00:00:00Z'
$outputDir = Split-Path $OutputNdjson -Parent
if ($outputDir -and -not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
}
$sw = New-Object System.IO.StreamWriter($OutputNdjson, $false, [System.Text.UTF8Encoding]::new($false))

foreach ($p in $sorted) {
    $data = $p.Data
    $packetId = [int]$data[0] + ([int]($data[1] -band 0x01) * 0x100)
    $size = [uint32]$data.Length
    $tsMs = [uint64]([int64](($p.Timestamp.ToUniversalTime() - $epoch).TotalMilliseconds))
    $dir = $p.Direction
    $packetType = if ($dir -eq 'incoming') { 'world_s2c' } else { 'world_c2s' }
    $topicSuffix = if ($dir -eq 'incoming') { 's2c' } else { 'c2s' }
    $topic = "kpacket.v1.world.$topicSuffix.0x$($packetId.ToString('X4'))"

    $meta = @{
        timestamp    = $tsMs
        direction    = $dir
        packet_type  = $packetType
        packet_id    = $packetId
        packet_name  = "PV_0x$($packetId.ToString('X4'))"
        size         = $size
        metadata     = @{
            injected    = $false
            blocked     = $false
            chunk_size  = 0
            session_id  = $SessionId
            sync_count  = 0
        }
        version      = 'v1'
        session_uuid = $SessionId
        message_id   = $msgId
    } | ConvertTo-Json -Compress

    $b64 = [Convert]::ToBase64String($data)
    $line = (@{ topic = $topic; meta = $meta; data_b64 = $b64 } | ConvertTo-Json -Compress)
    $sw.WriteLine($line)
    $msgId++
}

$sw.Close()
Write-Host "Wrote $($sorted.Count) packets to $OutputNdjson"
