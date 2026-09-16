[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$KparserJson,
    [Parameter(Mandatory = $true)]
    [string]$Kparser2Json,
    [string]$OutputPath = ""
)

$ErrorActionPreference = "Stop"

function Read-JsonFile([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "JSON file not found: $Path"
    }

    return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

function Get-PropertyValue([object]$Object, [string]$Name) {
    if ($null -eq $Object) {
        return $null
    }

    foreach ($property in $Object.PSObject.Properties) {
        if ($property.Name -ieq $Name) {
            return $property.Value
        }
    }

    return $null
}

function As-Array([object]$Value) {
    if ($null -eq $Value) {
        return @()
    }

    return @($Value)
}

function Text([object]$Value) {
    if ($null -eq $Value) {
        return ""
    }

    return ([string]$Value).Trim()
}

function Number([object]$Value) {
    if ($null -eq $Value -or [string]::IsNullOrWhiteSpace([string]$Value)) {
        return 0
    }

    return [int]$Value
}

function Get-Interactions([object]$Document) {
    $parity = Get-PropertyValue $Document "parity"
    $rows = @(As-Array (Get-PropertyValue $parity "interactions"))
    if ($rows.Count -gt 0) {
        return $rows
    }

    $rows = As-Array (Get-PropertyValue $Document "Interactions")
    if ($rows.Count -gt 0) {
        return $rows
    }

    return As-Array (Get-PropertyValue $Document "interactions")
}

function Get-PlayerNames([object]$Document) {
    $names = @{}
    $combatants = @(As-Array (Get-PropertyValue $Document "Combatants"))
    if ($combatants.Count -eq 0) {
        $combatants = @(As-Array (Get-PropertyValue $Document "combatants"))
    }
    if ($combatants.Count -eq 0) {
        $combatants = @(As-Array (Get-PropertyValue $Document "entities"))
    }

    foreach ($combatant in $combatants) {
        $kind = Text (Get-PropertyValue $combatant "Kind")
        if ($kind.Length -eq 0) {
            $kind = Text (Get-PropertyValue $combatant "type")
        }

        if ($kind -ieq "Player" -or $kind -ieq "Pet" -or $kind -ieq "Fellow") {
            $name = Text (Get-PropertyValue $combatant "Name")
            if ($name.Length -eq 0) {
                $name = Text (Get-PropertyValue $combatant "name")
            }
            if ($name.Length -gt 0) {
                $names[$name.ToLowerInvariant()] = $true
            }
        }
    }

    return $names
}

function Get-NormalizedInteractions([object]$Document) {
    $players = Get-PlayerNames $Document
    $rows = @()
    # Legacy parity omits preparing flags. Corroborate each excluded row with
    # the native snapshot's explicit preparation records, preserving counts.
    $preparations = @{}
    if ($null -ne (Get-PropertyValue $Document "parity")) {
        foreach ($native in @(As-Array (Get-PropertyValue $Document "interactions"))) {
            if ((Get-PropertyValue $native "preparing") -eq $true) {
                $key = "$(Text (Get-PropertyValue $native 'actorName'))|$(Text (Get-PropertyValue $native 'targetName'))|$(Text (Get-PropertyValue $native 'actionType'))|$(Number (Get-PropertyValue $native 'amount'))"
                $preparations[$key] = 1 + [int]$preparations[$key]
            }
        }
    }

    foreach ($row in (Get-Interactions $Document)) {
        $actor = Text (Get-PropertyValue $row "actorName")
        $target = Text (Get-PropertyValue $row "targetName")
        $interaction = Text (Get-PropertyValue $row "interactionType")
        $action = Text (Get-PropertyValue $row "actionType")
        $prepareKey = "$actor|$target|$action|$(Number (Get-PropertyValue $row 'amount'))"
        if ((Text (Get-PropertyValue $row 'success')) -eq 'None' -and $preparations[$prepareKey] -gt 0) {
            $preparations[$prepareKey]--
            continue
        }
        $harm = Text (Get-PropertyValue $row "harmType")
        $category = Text (Get-PropertyValue $row "Category")
        if ($category.Length -eq 0) {
            $category = Text (Get-PropertyValue $row "category")
        }
        if ($category.Length -eq 0) {
            $category = $action
        }
        # v1's aggregate category includes critical hits in melee/ranged.
        if ($category -eq 'Melee Crit') { $category = 'Melee' }
        if ($category -eq 'Ranged Crit') { $category = 'Ranged' }

        $isPlayerActor = $players.Count -eq 0 -or $players.ContainsKey($actor.ToLowerInvariant())
        $isEnfeeble = $harm -ieq "Enfeeble" -or $action -ieq "Enfeeble"
        $isOffense = $interaction -ieq "Harm" -and $isPlayerActor -and -not $isEnfeeble

        $value = Get-PropertyValue $row "amount"
        if ($null -eq $value) {
            $value = Get-PropertyValue $row "Value"
        }
        if ($null -eq $value) {
            $value = Get-PropertyValue $row "value"
        }

        $rows += [pscustomobject]@{
                Actor = $actor
                Target = $target
                Interaction = $interaction
                Action = $action
                Category = $category
                Harm = $harm
                Value = [Math]::Max(0, (Number $value))
                Success = Text (Get-PropertyValue $row "success")
                IsOffense = $isOffense
            }
    }

    return $rows
}

function Is-Hit([string]$Success) {
    return $Success -ieq "hit" -or
        $Success -ieq "message" -or
        $Success -ieq "critical" -or
        $Success -ieq "magic-burst"
}

function Get-OffenseRows([object]$Document) {
    $groups = @{}
    foreach ($event in @(Get-NormalizedInteractions $Document)) {
        if (-not $event.IsOffense) {
            continue
        }

        $key = "$($event.Actor)|$($event.Category)"
        if (-not $groups.ContainsKey($key)) {
            $groups[$key] = @()
        }
        $groups[$key] = @($groups[$key]) + $event
    }

    $rows = @()
    $totalDamage = 0
    $totalAttempts = 0
    $totalHits = 0

    foreach ($key in $groups.Keys) {
        $events = @($groups[$key])
        $hits = @($events | Where-Object { Is-Hit $_.Success })
        $damage = ($events | Measure-Object -Property Value -Sum).Sum
        if ($null -eq $damage) {
            $damage = 0
        }

        $parts = $key.Split("|", 2)
        $row = [pscustomobject]@{
            key = ("{0}|{1}|{2}|{3}|{4}|{5}" -f
                $parts[0],
                $parts[1],
                [int]$damage,
                $events.Count,
                $hits.Count,
                ($events.Count - $hits.Count)).ToLowerInvariant()
            actor = $parts[0]
            category = $parts[1]
            damage = [int]$damage
            attempts = $events.Count
            hits = $hits.Count
            misses = $events.Count - $hits.Count
        }
        $rows += $row
        $totalDamage += [int]$damage
        $totalAttempts += $events.Count
        $totalHits += $hits.Count
    }

    $rows += [pscustomobject]@{
            key = "all|all"
            actor = "(all)"
            category = "All"
            damage = $totalDamage
            attempts = $totalAttempts
            hits = $totalHits
            misses = $totalAttempts - $totalHits
        }

    return $rows
}

function Get-Battles([object]$Document) {
    $battles = @(As-Array (Get-PropertyValue $Document "battles"))
    if ($battles.Count -eq 0) {
        $battles = @(As-Array (Get-PropertyValue $Document "Battles"))
    }

    $rows = @()
    foreach ($battle in $battles) {
        $enemy = Text (Get-PropertyValue $battle "enemyName")
        if ($enemy.Length -eq 0) {
            $enemy = Text (Get-PropertyValue $battle "EnemyName")
        }
        # Wire NPC resource names use underscores; the game renders spaces.
        $enemy = $enemy.Replace('_', ' ')
        $killer = Text (Get-PropertyValue $battle "killerName")
        if ($killer.Length -eq 0) {
            $killer = Text (Get-PropertyValue $battle "KillerName")
        }
        if ($killer.Length -eq 0) {
            $killerId = Get-PropertyValue $battle "KillerId"
            if ($null -ne $killerId) {
                $combatants = @(As-Array (Get-PropertyValue $Document "Combatants"))
                $match = @($combatants | Where-Object {
                    (Get-PropertyValue $_ "Id") -eq $killerId
                })
                if ($match.Count -eq 1) { $killer = Text (Get-PropertyValue $match[0] "Name") }
            }
        }
        $killed = [bool](Get-PropertyValue $battle "killed")
        if ($null -eq (Get-PropertyValue $battle "killed")) {
            $killed = [bool](Get-PropertyValue $battle "Killed")
        }
        $xp = Number (Get-PropertyValue $battle "experiencePoints")
        if ($xp -eq 0) {
            $xp = Number (Get-PropertyValue $battle "ExperiencePoints")
        }
        $chain = Number (Get-PropertyValue $battle "experienceChain")
        if ($chain -eq 0) {
            $chain = Number (Get-PropertyValue $battle "ExperienceChain")
        }

        $rows += [pscustomobject]@{
                key = ("{0}|{1}|{2}|{3}|{4}" -f $enemy, $killed, $killer, $xp, $chain).ToLowerInvariant()
                enemy = $enemy
                killed = $killed
                killer = $killer
                experience = $xp
                chain = $chain
            }
    }

    return $rows
}

function Compare-RecordSet([object[]]$Left, [object[]]$Right) {
    $leftCounts = @{}
    $rightCounts = @{}
    $leftRows = @{}
    $rightRows = @{}

    foreach ($row in @($Left)) {
        if (-not $leftCounts.ContainsKey($row.key)) {
            $leftCounts[$row.key] = 0
            $leftRows[$row.key] = $row
        }
        $leftCounts[$row.key]++
    }

    foreach ($row in @($Right)) {
        if (-not $rightCounts.ContainsKey($row.key)) {
            $rightCounts[$row.key] = 0
            $rightRows[$row.key] = $row
        }
        $rightCounts[$row.key]++
    }

    $keys = @($leftCounts.Keys) + @($rightCounts.Keys) | Sort-Object -Unique
    $missing = @()
    $extra = @()

    foreach ($key in $keys) {
        $leftCount = if ($leftCounts.ContainsKey($key)) { $leftCounts[$key] } else { 0 }
        $rightCount = if ($rightCounts.ContainsKey($key)) { $rightCounts[$key] } else { 0 }
        if ($leftCount -gt $rightCount) {
            for ($i = 0; $i -lt ($leftCount - $rightCount); $i++) {
                $missing += $leftRows[$key]
            }
        }
        elseif ($rightCount -gt $leftCount) {
            for ($i = 0; $i -lt ($rightCount - $leftCount); $i++) {
                $extra += $rightRows[$key]
            }
        }
    }

    return [pscustomobject]@{
        equal = $missing.Count -eq 0 -and $extra.Count -eq 0
        left_count = @($Left).Count
        right_count = @($Right).Count
        missing = @($missing)
        extra = @($extra)
    }
}

$kparser = Read-JsonFile $KparserJson
$kparser2 = Read-JsonFile $Kparser2Json
$kparserBattles = @(Get-Battles $kparser)
$kparser2Battles = @(Get-Battles $kparser2)
$kparserOffense = @(Get-OffenseRows $kparser)
$kparser2Offense = @(Get-OffenseRows $kparser2)
$fights = Compare-RecordSet -Left $kparserBattles -Right $kparser2Battles
$offense = Compare-RecordSet -Left $kparserOffense -Right $kparser2Offense

$xp = [pscustomobject]@{
    equal = (
        ($kparserBattles | Measure-Object -Property experience -Sum).Sum -eq
        ($kparser2Battles | Measure-Object -Property experience -Sum).Sum -and
        ($kparserBattles | Measure-Object -Property chain -Sum).Sum -eq
        ($kparser2Battles | Measure-Object -Property chain -Sum).Sum
    )
    kparser_experience = ($kparserBattles | Measure-Object -Property experience -Sum).Sum
    kparser2_experience = ($kparser2Battles | Measure-Object -Property experience -Sum).Sum
    kparser_chain = ($kparserBattles | Measure-Object -Property chain -Sum).Sum
    kparser2_chain = ($kparser2Battles | Measure-Object -Property chain -Sum).Sum
}

$equal = $fights.equal -and $offense.equal -and $xp.equal
$report = [ordered]@{
    schema_version = 1
    status = if ($equal) { "equal" } else { "mismatch" }
    generated_at = [DateTimeOffset]::UtcNow.ToString("O")
    sources = [ordered]@{
        kparser = [System.IO.Path]::GetFullPath($KparserJson)
        kparser2 = [System.IO.Path]::GetFullPath($Kparser2Json)
    }
    fights = $fights
    offense = $offense
    experience = $xp
}

$json = $report | ConvertTo-Json -Depth 10
if (-not [string]::IsNullOrWhiteSpace($OutputPath)) {
    $directory = Split-Path -Parent $OutputPath
    if (-not [string]::IsNullOrWhiteSpace($directory)) {
        New-Item -ItemType Directory -Force -Path $directory | Out-Null
    }
    Set-Content -LiteralPath $OutputPath -Value $json -Encoding utf8
}

Write-Output $json
if (-not $equal) {
    exit 1
}

exit 0
