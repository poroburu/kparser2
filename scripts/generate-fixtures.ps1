param(
    [string]$OutputDir = (Join-Path $PSScriptRoot "..\fixtures\sessions")
)

$ErrorActionPreference = "Stop"

function New-NdjsonLine {
    param(
        [string]$Topic,
        [hashtable]$Meta,
        [byte[]]$Data
    )

    $metaJson = ($Meta | ConvertTo-Json -Compress)
    $record = @{
        topic = $Topic
        meta = $metaJson
        data_b64 = [Convert]::ToBase64String($Data)
    }

    ($record | ConvertTo-Json -Compress)
}

function Pad-Name([string]$Name) {
    $bytes = New-Object byte[] 15
    [Text.Encoding]::UTF8.GetBytes($Name).CopyTo($bytes, 0)
    return $bytes
}

function New-ChatPacket([string]$Speaker, [string]$Message, [byte]$Kind = 0) {
    $header = [byte[]](0x20, 0, 0x17, 0)
    $payload = [byte[]]($Kind, 0, 0, 0) + (Pad-Name $Speaker) + [Text.Encoding]::UTF8.GetBytes($Message) + [byte[]](0)
    return $header + $payload
}

function New-TrophyListPacket([int]$ItemId, [int]$Quantity = 1, [uint32]$DropperId = 12345) {
    $data = New-Object byte[] 60
    $data[2] = 0xD2
    [BitConverter]::GetBytes([uint32]$Quantity).CopyTo($data, 4)
    [BitConverter]::GetBytes($DropperId).CopyTo($data, 8)
    [BitConverter]::GetBytes([uint16]$ItemId).CopyTo($data, 16)
    return $data
}

function New-TrophySolutionPacket([int]$PoolSlot, [int]$JudgeFlag, [string]$ActorName) {
    $data = New-Object byte[] 60
    $data[2] = 0xD3
    $data[20] = [byte]$PoolSlot
    $data[21] = [byte]$JudgeFlag
    (Pad-Name $ActorName).CopyTo($data, 22)
    return $data
}

function New-BattleMessagePacket([uint32]$Caster, [uint32]$Target, [uint16]$MessageNum) {
    $data = New-Object byte[] 28
    $data[2] = 0x29
    [BitConverter]::GetBytes($Caster).CopyTo($data, 4)
    [BitConverter]::GetBytes($Target).CopyTo($data, 8)
    [BitConverter]::GetBytes($MessageNum).CopyTo($data, 24)
    return $data
}

function New-Meta([int]$PacketId, [string]$PacketName, [int]$Size, [int]$MessageId, [string]$Direction = "incoming") {
    return @{
        timestamp = 1700000000000 + $MessageId
        direction = $Direction
        packet_type = if ($Direction -eq "incoming") { "world_s2c" } else { "world_c2s" }
        packet_id = $PacketId
        packet_name = $PacketName
        size = $Size
        metadata = @{
            injected = $false
            blocked = $false
            chunk_size = 0
            session_id = "fixture"
            sync_count = 100 + $MessageId
        }
        version = "v1"
        session_uuid = "fixture"
        message_id = $MessageId
    }
}

if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir | Out-Null
}

$chatData = New-ChatPacket "Alice" "Hello from fixture" 0
$d2Data = New-TrophyListPacket 704 1
$actionData = [byte[]](0x10, 0, 0x1A, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12)

$sampleLines = @(
    (New-NdjsonLine "kpacket.v1.world.s2c.0x0017" (New-Meta 0x17 "GP_SERV_COMMAND_CHAT_STD" $chatData.Length 1) $chatData)
    (New-NdjsonLine "kpacket.v1.world.s2c.0x00D2" (New-Meta 0xD2 "GP_SERV_COMMAND_TROPHY_LIST" $d2Data.Length 2) $d2Data)
    (New-NdjsonLine "kpacket.v1.world.c2s.0x001A" (New-Meta 0x1A "GP_CLI_ACTION" $actionData.Length 3 "outgoing") $actionData)
)
Set-Content -Path (Join-Path $OutputDir "sample.ndjson") -Value $sampleLines -Encoding UTF8

$loginChat = New-ChatPacket "System" "Welcome to Vana'diel" 6
$enterZone = [byte[]](0x10, 0, 0x08, 0, 1, 2, 3, 4)
$loginLines = @(
    (New-NdjsonLine "kpacket.v1.world.s2c.0x0008" (New-Meta 0x08 "GP_SERV_COMMAND_ENTERZONE" $enterZone.Length 1) $enterZone)
    (New-NdjsonLine "kpacket.v1.world.s2c.0x0017" (New-Meta 0x17 "GP_SERV_COMMAND_CHAT_STD" $loginChat.Length 2) $loginChat)
)
Set-Content -Path (Join-Path $OutputDir "login.ndjson") -Value $loginLines -Encoding UTF8

$d2 = New-TrophyListPacket 4172 1
$d3 = New-TrophySolutionPacket 0 1 "Winner"
$dropLines = @(
    (New-NdjsonLine "kpacket.v1.world.s2c.0x00D2" (New-Meta 0xD2 "GP_SERV_COMMAND_TROPHY_LIST" $d2.Length 1) $d2)
    (New-NdjsonLine "kpacket.v1.world.s2c.0x00D3" (New-Meta 0xD3 "GP_SERV_COMMAND_TROPHY_SOLUTION" $d3.Length 2) $d3)
)
Set-Content -Path (Join-Path $OutputDir "item_drop.ndjson") -Value $dropLines -Encoding UTF8

$bm = New-BattleMessagePacket 100 200 0x0033
$combatLines = @(
    (New-NdjsonLine "kpacket.v1.world.s2c.0x0029" (New-Meta 0x29 "GP_SERV_COMMAND_BATTLE_MESSAGE" $bm.Length 1) $bm)
)
Set-Content -Path (Join-Path $OutputDir "combat_basic.ndjson") -Value $combatLines -Encoding UTF8

Write-Host "Generated fixtures in $OutputDir"
