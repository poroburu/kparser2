param(
    [string]$Source = "C:\Users\porob\git\ffxi-captures\ndjson\bst_leveling_20260616_1713.ndjson",
    [string]$PetName = "LullabyMelodia",
    [uint32]$LocalId = 5485
)

$found = 0
foreach ($line in [IO.File]::ReadLines($Source)) {
    if ($line -notmatch 'data_b64') { continue }
    try {
        $obj = $line | ConvertFrom-Json
        $b = [Convert]::FromBase64String($obj.data_b64)
        if ($b.Length -lt 28 -or $b[2] -ne 0x68) { continue }

        $owner8 = [BitConverter]::ToUInt32($b, 8)
        $target20 = [BitConverter]::ToUInt32($b, 20)
        $name24 = ([Text.Encoding]::UTF8.GetString($b, 24, 16)).Trim([char]0)

        $text = [Text.Encoding]::UTF8.GetString($b)
        if ($text -notmatch $PetName) { continue }

        Write-Host "len=$($b.Length) owner@8=$owner8 target@20=$target20 name@24='$name24' localMatch=$(($owner8 -eq $LocalId))"
        $found++
        if ($found -ge 8) { break }
    } catch {}
}

Write-Host "samples=$found"

# Also scan 0x0E with pet name
$eFound = 0
foreach ($line in [IO.File]::ReadLines($Source)) {
    if ($line -notmatch 'data_b64') { continue }
    try {
        $obj = $line | ConvertFrom-Json
        $b = [Convert]::FromBase64String($obj.data_b64)
        if ($b.Length -lt 68 -or $b[2] -ne 0x0E) { continue }
        $name = ([Text.Encoding]::UTF8.GetString($b, 52, 16)).Trim([char]0)
        if ($name -ne $PetName) { continue }
        $id = [BitConverter]::ToUInt32($b, 4)
        $claimer = [BitConverter]::ToUInt32($b, 44)
        Write-Host "0x0E id=$id claimer=$claimer claimerLocal=$(($claimer -eq $LocalId))"
        $eFound++
        if ($eFound -ge 5) { break }
    } catch {}
}

Write-Host "0x0E samples=$eFound"
