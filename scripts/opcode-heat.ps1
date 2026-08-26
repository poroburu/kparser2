# Ranked-parity fingerprint for a live NDJSON capture.
# Packet volume (0x0015 / 0x000E spam) is ignored. Exit 0 = mix unchanged (skip snapshot).
param(
    [Parameter(Mandatory = $true)]
    [string]$Path,
    [string]$Previous = "",
    [string]$Output = ""
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $Path)) {
    throw "File not found: $Path"
}

if ([string]::IsNullOrWhiteSpace($Output)) {
    $dir = Split-Path -Parent $Path
    $name = [System.IO.Path]::GetFileNameWithoutExtension($Path)
    $Output = Join-Path $dir "$name.heat.json"
}

if ([string]::IsNullOrWhiteSpace($Previous) -and (Test-Path -LiteralPath $Output)) {
    $Previous = $Output
}

$combat28 = 0
$combat29 = 0
$lootD2 = 0
$login0a = 0
$dig2f = 0
$digC2s63 = 0
$xp2d = 0
$chatKinds = New-Object 'System.Collections.Generic.HashSet[int]'
$logoutStates = New-Object 'System.Collections.Generic.HashSet[int]'
$combatCmds = New-Object 'System.Collections.Generic.HashSet[int]'
$msg29 = New-Object 'System.Collections.Generic.HashSet[int]'
$msg2d = New-Object 'System.Collections.Generic.HashSet[int]'
$packets = 0

# LSB-first bits after world header + info-size byte (Battle0x28 BitstreamReader).
function Get-Battle0x28CommandNo([byte[]]$data) {
    if ($null -eq $data -or $data.Length -lt 12) { return $null }
    $byteIndex = 5
    $bitIndex = 0
    $skip = 32 + 6 + 4
    $need = $skip + 4
    $acc = 0
    $got = 0
    for ($i = 0; $i -lt $need; $i++) {
        if ($byteIndex -ge $data.Length) { return $null }
        $bit = ($data[$byteIndex] -shr $bitIndex) -band 1
        $bitIndex++
        if ($bitIndex -ge 8) { $byteIndex++; $bitIndex = 0 }
        if ($i -lt $skip) { continue }
        $acc = $acc -bor ($bit -shl $got)
        $got++
    }
    return $acc
}

$fs = [System.IO.File]::Open($Path, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
try {
    $sr = New-Object System.IO.StreamReader($fs)
    try {
        while ($null -ne ($line = $sr.ReadLine())) {
            if ([string]::IsNullOrWhiteSpace($line)) { continue }
            if ($line -match '"type":"kparser2.session"') { continue }

            try {
                $row = $line | ConvertFrom-Json
            } catch {
                continue
            }

            $packets++
            if ($row.topic -notmatch 'world\.(s2c|c2s)\.(0x[0-9A-Fa-f]+)$') { continue }

            $dirn = $Matches[1]
            $op = $Matches[2].ToLower()
            $bytes = $null
            if ("$dirn $op" -in @("s2c 0x0017", "s2c 0x000b", "s2c 0x0028", "s2c 0x0029", "s2c 0x002d") -and -not [string]::IsNullOrWhiteSpace($row.data_b64)) {
                try { $bytes = [Convert]::FromBase64String($row.data_b64) } catch { $bytes = $null }
            }

            switch ("$dirn $op") {
                "s2c 0x0028" {
                    $combat28++
                    $cmd = Get-Battle0x28CommandNo $bytes
                    if ($null -ne $cmd) { [void]$combatCmds.Add([int]$cmd) }
                }
                "s2c 0x0029" {
                    $combat29++
                    if ($null -ne $bytes -and $bytes.Length -ge 26) {
                        [void]$msg29.Add([int][BitConverter]::ToUInt16($bytes, 24))
                    }
                }
                "s2c 0x002d" {
                    $xp2d++
                    if ($null -ne $bytes -and $bytes.Length -ge 26) {
                        [void]$msg2d.Add([int][BitConverter]::ToUInt16($bytes, 24))
                    }
                }
                "s2c 0x00d2" { $lootD2++ }
                "s2c 0x000a" { $login0a++ }
                "s2c 0x002f" { $dig2f++ }
                "c2s 0x0063" { $digC2s63++ }
                "s2c 0x0017" {
                    if ($null -ne $bytes -and $bytes.Length -ge 5) { [void]$chatKinds.Add([int]$bytes[4]) }
                }
                "s2c 0x000b" {
                    if ($null -ne $bytes -and $bytes.Length -ge 5) { [void]$logoutStates.Add([int]$bytes[4]) }
                }
            }
        }
    } finally {
        $sr.Dispose()
    }
} finally {
    $fs.Dispose()
}

$kindList = @($chatKinds | Sort-Object)
$stateList = @($logoutStates | Sort-Object)
$cmdList = @($combatCmds | Sort-Object)
$msg29List = @($msg29 | Sort-Object)
$msg2dList = @($msg2d | Sort-Object)
$lootBit = [int]($lootD2 -gt 0)
$digBit = [int](($dig2f -gt 0) -or ($digC2s63 -gt 0))
# Shape, not volume: extra melee of the same commandNo / extra yells of the same Kind stay cool.
$canon = "cmds=$($cmdList -join ',');m29=$($msg29List -join ',');kinds=$($kindList -join ',');d2=$lootBit;a=$login0a;b=$($stateList -join ',');dig=$digBit;m2d=$($msg2dList -join ',')"

$fp = [ordered]@{
    fingerprint     = $canon
    combat_0x28     = $combat28
    combat_cmds     = @($cmdList)
    combat_0x29     = $combat29
    combat_0x29_msg = @($msg29List)
    xp_0x2d         = $xp2d
    xp_0x2d_msg     = @($msg2dList)
    chat_kinds_0x17 = @($kindList)
    loot_0xd2       = $lootD2
    login_0x0a      = $login0a
    logout_states   = @($stateList)
    dig_s2c_0x2f    = $dig2f
    dig_c2s_0x63    = $digC2s63
}

$json = $fp | ConvertTo-Json -Compress
$utf8 = New-Object System.Text.UTF8Encoding $false

$unchanged = $false
if (-not [string]::IsNullOrWhiteSpace($Previous) -and (Test-Path -LiteralPath $Previous)) {
    try {
        $prev = Get-Content -LiteralPath $Previous -Raw | ConvertFrom-Json
        $unchanged = ($prev.fingerprint -eq $canon)
    } catch {
        $unchanged = $false
    }
}

[System.IO.File]::WriteAllText($Output, $json, $utf8)

Write-Host "packets=$packets fingerprint=$Output"
Write-Host ("  0x28={0} cmds=[{1}] 0x29={2} msg=[{3}] 0x2D={4} msg=[{5}] 0x17_kinds=[{6}] 0xD2={7} 0x0A={8} 0x0B_states=[{9}] dig={10}" -f `
    $combat28, ($cmdList -join ","), $combat29, ($msg29List -join ","), $xp2d, ($msg2dList -join ","), ($kindList -join ","), $lootD2, $login0a, ($stateList -join ","), $digBit)

if ($unchanged) {
    Write-Host "HEAT unchanged"
    exit 0
}

Write-Host "HEAT changed"
exit 1
