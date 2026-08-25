# Promote BST capture slices into golden fixtures.
param(
    [string]$Source = "C:\Users\porob\git\kdev\ffxi-captures\ndjson\bst_leveling_20260616_1713.ndjson",
    [string]$OutDir = "C:\Users\porob\git\kdev\kparser2\fixtures\sessions"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $Source)) {
    Write-Error "Source capture not found: $Source"
}

$lines = [System.IO.File]::ReadAllLines($Source)
Write-Host "Source lines: $($lines.Length)"

function Find-LineIndex([scriptblock]$predicate, [int]$start = 0) {
    for ($i = $start; $i -lt $lines.Length; $i++) {
        if (& $predicate $lines[$i]) { return $i }
    }
    return -1
}

$dfIdx = Find-LineIndex { param($l) $l -match '"packet_id"\s*:\s*223\b' -or $l -match '0x00DF' -or $l -match '0x00df' -or $l -match 'GROUP_ATTR' }
$d3Idx = Find-LineIndex { param($l) $l -match 'Poroburu' -or ($l -match '"packet_id"\s*:\s*211\b' -and $l -match 'TROPHY') }
if ($d3Idx -lt 0) {
    $d3Idx = Find-LineIndex { param($l) $l -match '"packet_id"\s*:\s*211\b' }
}
# fallback: decode base64 for id 5485 (0x0000156D) in 0xDF packets
if ($dfIdx -lt 0) {
    for ($i = 0; $i -lt $lines.Length; $i++) {
        if ($lines[$i] -notmatch 'data_b64') { continue }
        try {
            $obj = $lines[$i] | ConvertFrom-Json
            $bytes = [Convert]::FromBase64String($obj.data_b64)
            if ($bytes.Length -ge 8 -and $bytes[2] -eq 0xDF) {
                $id = [BitConverter]::ToUInt32($bytes, 4)
                if ($id -eq 5485) { $dfIdx = $i; break }
            }
        } catch {}
    }
}
if ($d3Idx -lt 0) {
    for ($i = 0; $i -lt $lines.Length; $i++) {
        if ($lines[$i] -notmatch 'data_b64') { continue }
        try {
            $obj = $lines[$i] | ConvertFrom-Json
            $bytes = [Convert]::FromBase64String($obj.data_b64)
            if ($bytes.Length -ge 40 -and $bytes[2] -eq 0xD3) {
                $id = [BitConverter]::ToUInt32($bytes, 4)
                if ($id -eq 5485) { $d3Idx = $i; break }
            }
        } catch {}
    }
}
Write-Host "0x00DF line index: $dfIdx"
Write-Host "Poroburu line index: $d3Idx"

if ($dfIdx -lt 0 -or $d3Idx -lt 0) {
    Write-Error "Could not locate required packets in capture"
}

# bst_loot_name: from first 0x00DF through ~200 lines after first D3 (includes combat context)
$lootEnd = [Math]::Min($lines.Length - 1, $d3Idx + 250)
$lootStart = [Math]::Max(0, $dfIdx - 5)
$lootSlice = $lines[$lootStart..$lootEnd]
$lootPath = Join-Path $OutDir "bst_loot_name.ndjson"
[System.IO.File]::WriteAllLines($lootPath, $lootSlice)
Write-Host "Wrote $($lootSlice.Length) lines to $lootPath"

# bst_camp_multi: find Master_Coeurl mob spawns in 0x00E packets
$masterIdx = @()
for ($i = 0; $i -lt $lines.Length; $i++) {
    if ($lines[$i] -notmatch 'data_b64') { continue }
    try {
        $obj = $lines[$i] | ConvertFrom-Json
        $bytes = [Convert]::FromBase64String($obj.data_b64)
        if ($bytes.Length -ge 60 -and $bytes[2] -eq 0x0E) {
            $nameBytes = $bytes[52..([Math]::Min(67, $bytes.Length - 1))]
            $name = ([Text.Encoding]::UTF8.GetString($nameBytes)).Trim([char]0)
            if ($name -match 'Master_Coeurl|Coeurl') { $masterIdx += $i }
        }
        else {
            $text = [Text.Encoding]::UTF8.GetString($bytes)
            if ($text -match 'Master_Coeurl') { $masterIdx += $i }
        }
    } catch {}
}
Write-Host "Master_Coeurl mentions: $($masterIdx.Count)"

if ($masterIdx.Count -ge 3) {
    $multiStart = [Math]::Max(0, $masterIdx[0] - 400)
    $multiEnd = [Math]::Min($lines.Length - 1, $masterIdx[[Math]::Min(5, $masterIdx.Count - 1)] + 400)
    $multiSlice = $lines[$multiStart..$multiEnd]
    $multiPath = Join-Path $OutDir "bst_camp_multi.ndjson"
    [System.IO.File]::WriteAllLines($multiPath, $multiSlice)
    Write-Host "Wrote $($multiSlice.Length) lines to $multiPath"
} else {
    # Fallback: slice around validated multi-fight window (lines 3500-9000 from BST camp session)
    $multiStart = 3500
    $multiEnd = [Math]::Min($lines.Length - 1, 9000)
    $multiSlice = $lines[$multiStart..$multiEnd]
    $multiPath = Join-Path $OutDir "bst_camp_multi.ndjson"
    [System.IO.File]::WriteAllLines($multiPath, $multiSlice)
    Write-Host "Wrote fallback $($multiSlice.Length) lines to $multiPath"
}
