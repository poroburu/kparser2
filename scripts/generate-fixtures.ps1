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

function New-GroupAttrPacket([uint32]$EntityId = 100, [uint16]$ZoneId = 140) {
    $data = New-Object byte[] 40
    $data[2] = 0xDF
    [BitConverter]::GetBytes($EntityId).CopyTo($data, 4)
    [BitConverter]::GetBytes($ZoneId).CopyTo($data, 26)
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

$playerId = 100
$mobId = 200
$groupAttr = New-GroupAttrPacket $playerId 140
$harm = New-CombatActionPacket -ActorId $playerId -TargetId $mobId -CommandNo 1 -Damage 55 -MessageId 1
$killMsg = New-BattleMessagePacket $playerId $mobId 6
$fallMsg = New-BattleMessagePacket $mobId $mobId 20
$killXp = New-BattleMessagePacket $playerId $playerId 8 0 150
$chainChat = New-ChatPacket "System" "EXP chain #2!" 6
$combatKillXpLines = @(
    (New-NdjsonLine "kpacket.v1.world.s2c.0x00DF" (New-Meta 0xDF "GP_SERV_COMMAND_GROUP_ATTR" $groupAttr.Length 1) $groupAttr)
    (New-NdjsonLine "kpacket.v1.world.s2c.0x0028" (New-Meta 0x28 "GP_SERV_COMMAND_BATTLE2" $harm.Length 2) $harm)
    (New-NdjsonLine "kpacket.v1.world.s2c.0x0029" (New-Meta 0x29 "GP_SERV_COMMAND_BATTLE_MESSAGE" $killMsg.Length 3) $killMsg)
    (New-NdjsonLine "kpacket.v1.world.s2c.0x0029" (New-Meta 0x29 "GP_SERV_COMMAND_BATTLE_MESSAGE" $fallMsg.Length 4) $fallMsg)
    (New-NdjsonLine "kpacket.v1.world.s2c.0x0029" (New-Meta 0x29 "GP_SERV_COMMAND_BATTLE_MESSAGE" $killXp.Length 5) $killXp)
    (New-NdjsonLine "kpacket.v1.world.s2c.0x0017" (New-Meta 0x17 "GP_SERV_COMMAND_CHAT_STD" $chainChat.Length 6) $chainChat)
)
Set-Content -Path (Join-Path $OutputDir "combat_kill_xp.ndjson") -Value $combatKillXpLines -Encoding UTF8

$playerEntityId = 100
$mobEntityId = 200
$partyEntityId = 300
$bootstrap = New-GroupAttrPacket $playerEntityId 140

$meleeHitPlayer = New-CombatActionPacket -ActorId $playerEntityId -TargetId $mobEntityId -CommandNo 1 -Damage 128 -MessageId 0x14
$meleeHitMob = New-CombatActionPacket -ActorId $mobEntityId -TargetId $playerEntityId -CommandNo 1 -Damage 170 -MessageId 0x1C
$meleeHitParty = New-CombatActionPacket -ActorId $partyEntityId -TargetId $mobEntityId -CommandNo 1 -Damage 168 -MessageId 0x19
$combatMeleeHitsLines = @(
    (New-NdjsonLine "kpacket.v1.world.s2c.0x00DF" (New-Meta 0xDF "GP_SERV_COMMAND_GROUP_ATTR" $bootstrap.Length 1) $bootstrap)
    (New-NdjsonLine "kpacket.v1.world.s2c.0x0028" (New-Meta 0x28 "GP_SERV_COMMAND_BATTLE2" $meleeHitPlayer.Length 2) $meleeHitPlayer)
    (New-NdjsonLine "kpacket.v1.world.s2c.0x0028" (New-Meta 0x28 "GP_SERV_COMMAND_BATTLE2" $meleeHitMob.Length 3) $meleeHitMob)
    (New-NdjsonLine "kpacket.v1.world.s2c.0x0028" (New-Meta 0x28 "GP_SERV_COMMAND_BATTLE2" $meleeHitParty.Length 4) $meleeHitParty)
)
Set-Content -Path (Join-Path $OutputDir "combat_melee_hits.ndjson") -Value $combatMeleeHitsLines -Encoding UTF8

$missPlayer = New-CombatActionPacket -ActorId $playerEntityId -TargetId $mobEntityId -CommandNo 1 -Damage 0 -MessageId 0x15 -Miss 1
$missMob = New-CombatActionPacket -ActorId $mobEntityId -TargetId $playerEntityId -CommandNo 1 -Damage 0 -MessageId 0x1D -Miss 1
$combatMissesLines = @(
    (New-NdjsonLine "kpacket.v1.world.s2c.0x00DF" (New-Meta 0xDF "GP_SERV_COMMAND_GROUP_ATTR" $bootstrap.Length 1) $bootstrap)
    (New-NdjsonLine "kpacket.v1.world.s2c.0x0028" (New-Meta 0x28 "GP_SERV_COMMAND_BATTLE2" $missPlayer.Length 2) $missPlayer)
    (New-NdjsonLine "kpacket.v1.world.s2c.0x0028" (New-Meta 0x28 "GP_SERV_COMMAND_BATTLE2" $missMob.Length 3) $missMob)
)
Set-Content -Path (Join-Path $OutputDir "combat_misses.ndjson") -Value $combatMissesLines -Encoding UTF8

$rangedHit = New-CombatActionPacket -ActorId $partyEntityId -TargetId $mobEntityId -CommandNo 2 -Damage 247 -MessageId 0x19
$rangedMiss = New-CombatActionPacket -ActorId $playerEntityId -TargetId $mobEntityId -CommandNo 2 -Damage 0 -MessageId 0x15 -Miss 1
$combatRangedLines = @(
    (New-NdjsonLine "kpacket.v1.world.s2c.0x0028" (New-Meta 0x28 "GP_SERV_COMMAND_BATTLE2" $rangedHit.Length 1) $rangedHit)
    (New-NdjsonLine "kpacket.v1.world.s2c.0x0028" (New-Meta 0x28 "GP_SERV_COMMAND_BATTLE2" $rangedMiss.Length 2) $rangedMiss)
)
Set-Content -Path (Join-Path $OutputDir "combat_ranged.ndjson") -Value $combatRangedLines -Encoding UTF8

$parry = New-CombatActionPacket -ActorId $mobEntityId -TargetId $playerEntityId -CommandNo 1 -Damage 0 -MessageId 0x1D -Miss 3
$shadow = New-CombatActionPacket -ActorId $mobEntityId -TargetId $playerEntityId -CommandNo 1 -Damage 0 -MessageId 0x1D -Miss 0
$combatDefenseLines = @(
    (New-NdjsonLine "kpacket.v1.world.s2c.0x00DF" (New-Meta 0xDF "GP_SERV_COMMAND_GROUP_ATTR" $bootstrap.Length 1) $bootstrap)
    (New-NdjsonLine "kpacket.v1.world.s2c.0x0028" (New-Meta 0x28 "GP_SERV_COMMAND_BATTLE2" $parry.Length 2) $parry)
    (New-NdjsonLine "kpacket.v1.world.s2c.0x0028" (New-Meta 0x28 "GP_SERV_COMMAND_BATTLE2" $shadow.Length 3) $shadow)
)
Set-Content -Path (Join-Path $OutputDir "combat_defense.ndjson") -Value $combatDefenseLines -Encoding UTF8

$failBuff = New-CombatActionPacket -ActorId $playerEntityId -TargetId $playerEntityId -CommandNo 4 -Damage 0 -MessageId 0x44 -Miss 0
$failDebuff = New-CombatActionPacket -ActorId $mobEntityId -TargetId $playerEntityId -CommandNo 4 -Damage 0 -MessageId 0x3B -Miss 0
$combatFailuresLines = @(
    (New-NdjsonLine "kpacket.v1.world.s2c.0x0028" (New-Meta 0x28 "GP_SERV_COMMAND_BATTLE2" $failBuff.Length 1) $failBuff)
    (New-NdjsonLine "kpacket.v1.world.s2c.0x0028" (New-Meta 0x28 "GP_SERV_COMMAND_BATTLE2" $failDebuff.Length 2) $failDebuff)
)
Set-Content -Path (Join-Path $OutputDir "combat_failures.ndjson") -Value $combatFailuresLines -Encoding UTF8

$counter = New-CombatActionPacket -ActorId $playerEntityId -TargetId $mobEntityId -CommandNo 1 -Damage 56 -MessageId 0x14 -Miss 0
$retaliate = New-CombatActionPacket -ActorId $partyEntityId -TargetId $mobEntityId -CommandNo 1 -Damage 52 -MessageId 0x19 -Miss 0
$combatCountersLines = @(
    (New-NdjsonLine "kpacket.v1.world.s2c.0x0028" (New-Meta 0x28 "GP_SERV_COMMAND_BATTLE2" $counter.Length 1) $counter)
    (New-NdjsonLine "kpacket.v1.world.s2c.0x0028" (New-Meta 0x28 "GP_SERV_COMMAND_BATTLE2" $retaliate.Length 2) $retaliate)
)
Set-Content -Path (Join-Path $OutputDir "combat_counters.ndjson") -Value $combatCountersLines -Encoding UTF8

$tpHit = New-CombatActionPacket -ActorId $playerEntityId -TargetId $mobEntityId -CommandNo 1 -Damage 90 -MessageId 0xA3 -Miss 0
$tpDrain = New-CombatActionPacket -ActorId $playerEntityId -TargetId $mobEntityId -CommandNo 1 -Damage 3 -MessageId 0xBB -Miss 0
$combatTpDrainLines = @(
    (New-NdjsonLine "kpacket.v1.world.s2c.0x0028" (New-Meta 0x28 "GP_SERV_COMMAND_BATTLE2" $tpHit.Length 1) $tpHit)
    (New-NdjsonLine "kpacket.v1.world.s2c.0x0028" (New-Meta 0x28 "GP_SERV_COMMAND_BATTLE2" $tpDrain.Length 2) $tpDrain)
)
Set-Content -Path (Join-Path $OutputDir "combat_tp_drain.ndjson") -Value $combatTpDrainLines -Encoding UTF8

$enfeeble = New-CombatActionPacket -ActorId $playerEntityId -TargetId $mobEntityId -CommandNo 4 -Damage 0 -MessageId 0x39 -Miss 0
$buff = New-CombatActionPacket -ActorId $playerEntityId -TargetId $playerEntityId -CommandNo 4 -Damage 0 -MessageId 0x38 -Miss 0
$drain = New-CombatActionPacket -ActorId $mobEntityId -TargetId $playerEntityId -CommandNo 4 -Damage 50 -MessageId 0x16 -Miss 0
$ja = New-CombatActionPacket -ActorId $playerEntityId -TargetId $mobEntityId -CommandNo 13 -Damage 80 -MessageId 0x68 -Miss 0
$combatEnfeebleLines = @(
    (New-NdjsonLine "kpacket.v1.world.s2c.0x0028" (New-Meta 0x28 "GP_SERV_COMMAND_BATTLE2" $enfeeble.Length 1) $enfeeble)
)
Set-Content -Path (Join-Path $OutputDir "combat_enfeeble.ndjson") -Value $combatEnfeebleLines -Encoding UTF8
$combatBuffLines = @(
    (New-NdjsonLine "kpacket.v1.world.s2c.0x0028" (New-Meta 0x28 "GP_SERV_COMMAND_BATTLE2" $buff.Length 1) $buff)
)
Set-Content -Path (Join-Path $OutputDir "combat_buff.ndjson") -Value $combatBuffLines -Encoding UTF8
$combatDrainLines = @(
    (New-NdjsonLine "kpacket.v1.world.s2c.0x0028" (New-Meta 0x28 "GP_SERV_COMMAND_BATTLE2" $drain.Length 1) $drain)
)
Set-Content -Path (Join-Path $OutputDir "combat_drain.ndjson") -Value $combatDrainLines -Encoding UTF8
$combatJaLines = @(
    (New-NdjsonLine "kpacket.v1.world.s2c.0x0028" (New-Meta 0x28 "GP_SERV_COMMAND_BATTLE2" $ja.Length 1) $ja)
)
Set-Content -Path (Join-Path $OutputDir "combat_ja.ndjson") -Value $combatJaLines -Encoding UTF8

$prepare = New-CombatActionPacket -ActorId $playerEntityId -TargetId $mobEntityId -CommandNo 4 -Damage 0 -MessageId 0x32 -Miss 0
$combatPrepareLines = @(
    (New-NdjsonLine "kpacket.v1.world.s2c.0x0028" (New-Meta 0x28 "GP_SERV_COMMAND_BATTLE2" $prepare.Length 1) $prepare)
)
Set-Content -Path (Join-Path $OutputDir "combat_prepare.ndjson") -Value $combatPrepareLines -Encoding UTF8

$cover = New-CombatActionPacket -ActorId $mobEntityId -TargetId $playerEntityId -CommandNo 1 -Damage 0 -MessageId 0x6D -Miss 1
$combatCoverLines = @(
    (New-NdjsonLine "kpacket.v1.world.s2c.0x0028" (New-Meta 0x28 "GP_SERV_COMMAND_BATTLE2" $cover.Length 1) $cover)
)
Set-Content -Path (Join-Path $OutputDir "combat_cover.ndjson") -Value $combatCoverLines -Encoding UTF8

$scMsg = New-BattleMessagePacket $playerEntityId $mobEntityId 287
$scFollow = New-CombatActionPacket -ActorId $playerEntityId -TargetId $mobEntityId -CommandNo 1 -Damage 100 -MessageId 0x28 -Miss 0
$combatSkillchainLines = @(
    (New-NdjsonLine "kpacket.v1.world.s2c.0x0029" (New-Meta 0x29 "GP_SERV_COMMAND_BATTLE_MESSAGE" $scMsg.Length 1) $scMsg)
    (New-NdjsonLine "kpacket.v1.world.s2c.0x0028" (New-Meta 0x28 "GP_SERV_COMMAND_BATTLE2" $scFollow.Length 2) $scFollow)
)
Set-Content -Path (Join-Path $OutputDir "combat_skillchain.ndjson") -Value $combatSkillchainLines -Encoding UTF8

Write-Host "Generated fixtures in $OutputDir"
