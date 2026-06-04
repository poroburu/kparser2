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

function New-BattleMessagePacket(
    [uint32]$Caster,
    [uint32]$Target,
    [uint16]$MessageNum,
    [uint32]$Param1 = 0,
    [uint32]$Param2 = 0,
    [byte]$MessageType = 0
) {
    $data = New-Object byte[] 28
    $data[2] = 0x29
    [BitConverter]::GetBytes($Caster).CopyTo($data, 4)
    [BitConverter]::GetBytes($Target).CopyTo($data, 8)
    [BitConverter]::GetBytes($Param1).CopyTo($data, 12)
    [BitConverter]::GetBytes($Param2).CopyTo($data, 16)
    [BitConverter]::GetBytes($MessageNum).CopyTo($data, 24)
    $data[26] = $MessageType
    return $data
}

function New-CombatActionPacket(
    [uint32]$ActorId = 1,
    [uint32]$TargetId = 2,
    [int]$CommandNo = 1,
    [int]$Damage = 42,
    [int]$MessageId = 1,
    [int]$Miss = 0
) {
    $bits = New-Object System.Collections.Generic.List[int]

    function Add-Bits([uint32]$Value, [int]$Count) {
        for ($i = 0; $i -lt $Count; $i++) {
            $bits.Add([int](($Value -shr $i) -band 1))
        }
    }

    Add-Bits $ActorId 32
    Add-Bits 1 6
    Add-Bits 0 4
    Add-Bits ([uint32]$CommandNo) 4
    Add-Bits 0 32
    Add-Bits 0 32
    Add-Bits ([uint32]$TargetId) 32
    Add-Bits 1 4
    Add-Bits ([uint32]$Miss) 3
    Add-Bits 0 2
    Add-Bits 0 12
    Add-Bits 0 5
    Add-Bits 0 5
    Add-Bits ([uint32]$Damage) 17
    Add-Bits ([uint32]$MessageId) 10
    Add-Bits 0 31
    Add-Bits 0 1
    Add-Bits 0 1

    $byteCount = [Math]::Ceiling($bits.Count / 8.0)
    $payloadBytes = New-Object byte[] $byteCount
    for ($bi = 0; $bi -lt $byteCount; $bi++) {
        $value = 0
        for ($bit = 0; $bit -lt 8; $bit++) {
            $idx = $bi * 8 + $bit
            if ($idx -lt $bits.Count -and $bits[$idx] -eq 1) {
                $value = $value -bor (1 -shl $bit)
            }
        }
        $payloadBytes[$bi] = [byte]$value
    }

    $header = [byte[]](0x20, 0, 0x28, 0, [byte]$payloadBytes.Length)
    return $header + $payloadBytes
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

$melee = New-CombatActionPacket -ActorId 100 -TargetId 200 -CommandNo 1 -Damage 55 -MessageId 1
$spell = New-CombatActionPacket -ActorId 100 -TargetId 200 -CommandNo 4 -Damage 120 -MessageId 2
$combatActionLines = @(
    (New-NdjsonLine "kpacket.v1.world.s2c.0x0028" (New-Meta 0x28 "GP_SERV_COMMAND_BATTLE2" $melee.Length 1) $melee)
    (New-NdjsonLine "kpacket.v1.world.s2c.0x0028" (New-Meta 0x28 "GP_SERV_COMMAND_BATTLE2" $spell.Length 2) $spell)
)
Set-Content -Path (Join-Path $OutputDir "combat_action.ndjson") -Value $combatActionLines -Encoding UTF8

$kill = New-BattleMessagePacket 100 200 6
$fall = New-BattleMessagePacket 200 200 20
$combatDeathLines = @(
    (New-NdjsonLine "kpacket.v1.world.s2c.0x0029" (New-Meta 0x29 "GP_SERV_COMMAND_BATTLE_MESSAGE" $kill.Length 1) $kill)
    (New-NdjsonLine "kpacket.v1.world.s2c.0x0029" (New-Meta 0x29 "GP_SERV_COMMAND_BATTLE_MESSAGE" $fall.Length 2) $fall)
)
Set-Content -Path (Join-Path $OutputDir "combat_death.ndjson") -Value $combatDeathLines -Encoding UTF8

$cureMsg = New-BattleMessagePacket 100 100 7 0 350
$cureAction = New-CombatActionPacket -ActorId 100 -TargetId 100 -CommandNo 4 -Damage 350 -MessageId 7
$combatRecoveryLines = @(
    (New-NdjsonLine "kpacket.v1.world.s2c.0x0029" (New-Meta 0x29 "GP_SERV_COMMAND_BATTLE_MESSAGE" $cureMsg.Length 1) $cureMsg)
    (New-NdjsonLine "kpacket.v1.world.s2c.0x0028" (New-Meta 0x28 "GP_SERV_COMMAND_BATTLE2" $cureAction.Length 2) $cureAction)
)
Set-Content -Path (Join-Path $OutputDir "combat_recovery.ndjson") -Value $combatRecoveryLines -Encoding UTF8

$xpGain = New-BattleMessagePacket 100 100 8 0 150
$xpChain = New-BattleMessagePacket 100 100 253 3 180
$xpChat = New-ChatPacket "System" "Player gains 150 experience points." 6
$chatXpLines = @(
    (New-NdjsonLine "kpacket.v1.world.s2c.0x0029" (New-Meta 0x29 "GP_SERV_COMMAND_BATTLE_MESSAGE" $xpGain.Length 1) $xpGain)
    (New-NdjsonLine "kpacket.v1.world.s2c.0x0029" (New-Meta 0x29 "GP_SERV_COMMAND_BATTLE_MESSAGE" $xpChain.Length 2) $xpChain)
    (New-NdjsonLine "kpacket.v1.world.s2c.0x0017" (New-Meta 0x17 "GP_SERV_COMMAND_CHAT_STD" $xpChat.Length 3) $xpChat)
)
Set-Content -Path (Join-Path $OutputDir "chat_xp.ndjson") -Value $chatXpLines -Encoding UTF8

Write-Host "Generated fixtures in $OutputDir"
