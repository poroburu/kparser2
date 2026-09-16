param(
    [Parameter(Mandatory = $true)]
    [string]$KparserJson,
    [Parameter(Mandatory = $true)]
    [string]$Kparser2Json,
    [string]$OutputPath = "",
    [switch]$SkipChat
)

$ErrorActionPreference = "Stop"

function Read-JsonFile {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "JSON file not found: $Path"
    }

    return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

function Get-PropertyValue {
    param(
        [object]$Object,
        [string]$Name
    )

    if ($null -eq $Object) {
        return $null
    }

    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) {
        return $null
    }

    return $property.Value
}

function As-Array {
    param([object]$Value)

    if ($null -eq $Value) {
        return @()
    }

    return @($Value)
}

function Text {
    param([object]$Value)

    if ($null -eq $Value) {
        return ""
    }

    return ([string]$Value).Trim()
}

function Get-KparserRows {
    param(
        [object]$Document,
        [ValidateSet("interaction", "chat")]
        [string]$Kind
    )

    $parity = Get-PropertyValue $Document "parity"
    $sourceRows = As-Array (Get-PropertyValue $parity $(if ($Kind -eq "interaction") { "interactions" } else { "chat" }))

    if ($Kind -eq "interaction") {
        return @(
            foreach ($row in $sourceRows) {
                [pscustomobject]@{
                    actorName = Text (Get-PropertyValue $row "actorName")
                    targetName = Text (Get-PropertyValue $row "targetName")
                    interactionType = Text (Get-PropertyValue $row "interactionType")
                    actionType = Text (Get-PropertyValue $row "actionType")
                    harmType = Text (Get-PropertyValue $row "harmType")
                    aidType = Text (Get-PropertyValue $row "aidType")
                    amount = [int](Get-PropertyValue $row "amount")
                    success = Text (Get-PropertyValue $row "success")
                }
            }
        )
    }

    return @(
        foreach ($row in $sourceRows) {
            [pscustomobject]@{
                speaker = Text (Get-PropertyValue $row "speaker")
                mode = Text (Get-PropertyValue $row "mode")
                message = Text (Get-PropertyValue $row "message")
            }
        }
    )
}

function Get-Kparser2Rows {
    param(
        [object]$Document,
        [ValidateSet("interaction", "chat")]
        [string]$Kind
    )

    $sourceRows = @()
    $parity = Get-PropertyValue $Document "parity"

    if ($null -ne $parity) {
        $sourceRows = As-Array (Get-PropertyValue $parity $(if ($Kind -eq "interaction") { "interactions" } else { "chat" }))
    }
    elseif ($Kind -eq "interaction") {
        $sourceRows = As-Array (Get-PropertyValue $Document "Interactions")
        if ($sourceRows.Count -eq 0) {
            $sourceRows = As-Array (Get-PropertyValue $Document "interactions")
        }
    }
    else {
        $sourceRows = As-Array (Get-PropertyValue $Document "ChatMessages")
        if ($sourceRows.Count -eq 0) {
            $sourceRows = As-Array (Get-PropertyValue $Document "chatMessages")
        }
        if ($sourceRows.Count -eq 0) {
            $sourceRows = As-Array (Get-PropertyValue $Document "chat")
        }
    }

    if ($Kind -eq "interaction") {
        return @(
            foreach ($row in $sourceRows) {
                $harmType = Text (Get-PropertyValue $row "harmType")
                $aidType = Text (Get-PropertyValue $row "aidType")
                $actionType = if ($harmType.Length -gt 0) {
                    $harmType
                }
                elseif ($aidType.Length -gt 0) {
                    $aidType
                }
                else {
                    Text (Get-PropertyValue $row "actionType")
                }

                $amountValue = Get-PropertyValue $row "amount"
                if ($null -eq $amountValue) {
                    $amountValue = Get-PropertyValue $row "value"
                }

                [pscustomobject]@{
                    actorName = Text (Get-PropertyValue $row "actorName")
                    targetName = Text (Get-PropertyValue $row "targetName")
                    interactionType = Text (Get-PropertyValue $row "interactionType")
                    actionType = $actionType
                    harmType = $harmType
                    aidType = $aidType
                    amount = [int]$amountValue
                    success = Text (Get-PropertyValue $row "success")
                }
            }
        )
    }

    return @(
        foreach ($row in $sourceRows) {
            $direction = Text (Get-PropertyValue $row "direction")
            if ($direction.Length -eq 0 -or $direction -ieq "incoming") {
                [pscustomobject]@{
                    speaker = Text (Get-PropertyValue $row "speaker")
                    mode = Text (Get-PropertyValue $row "mode")
                    message = Text (Get-PropertyValue $row "message")
                }
            }
        }
    )
}

function Get-InteractionKey {
    param([object]$Row)

    # kparser calls the physical family actionType (Melee/Ranged/Spell),
    # while kparser2 exposes the same normalized family as harmType and
    # projects it into actionType. The legacy broad harmType values
    # (Damage/Enfeeble/Drain) are not comparable to that family.
    $actionType = Text $Row.actionType
    if ($actionType.Length -eq 0) {
        $actionType = Text $Row.harmType
    }

    return (
        (Text $Row.actorName).ToLowerInvariant() + "|" +
        (Text $Row.targetName).ToLowerInvariant() + "|" +
        (Text $Row.interactionType).ToLowerInvariant() + "|" +
        $actionType.ToLowerInvariant() + "|" +
        [string][int]$Row.amount + "|" +
        (Text $Row.success).ToLowerInvariant()
    )
}

function Get-ChatKey {
    param([object]$Row)

    return (
        (Text $Row.speaker).ToLowerInvariant() + "|" +
        (Text $Row.mode).ToLowerInvariant() + "|" +
        (Text $Row.message)
    )
}

function Compare-Multiset {
    param(
        [object[]]$Left,
        [object[]]$Right,
        [scriptblock]$KeySelector
    )

    $leftBuckets = @{}
    $rightBuckets = @{}

    foreach ($row in @($Left)) {
        $key = & $KeySelector $row
        if (-not $leftBuckets.ContainsKey($key)) {
            $leftBuckets[$key] = New-Object System.Collections.Generic.List[object]
        }
        $leftBuckets[$key].Add($row)
    }

    foreach ($row in @($Right)) {
        $key = & $KeySelector $row
        if (-not $rightBuckets.ContainsKey($key)) {
            $rightBuckets[$key] = New-Object System.Collections.Generic.List[object]
        }
        $rightBuckets[$key].Add($row)
    }

    $keys = @(
        foreach ($key in $leftBuckets.Keys) { $key }
        foreach ($key in $rightBuckets.Keys) { $key }
    ) | Sort-Object -Unique
    $missing = New-Object System.Collections.Generic.List[object]
    $extra = New-Object System.Collections.Generic.List[object]

    foreach ($key in $keys) {
        $leftRows = New-Object System.Collections.Generic.List[object]
        $rightRows = New-Object System.Collections.Generic.List[object]

        if ($leftBuckets.ContainsKey($key)) {
            foreach ($row in $leftBuckets[$key]) {
                $leftRows.Add($row)
            }
        }
        if ($rightBuckets.ContainsKey($key)) {
            foreach ($row in $rightBuckets[$key]) {
                $rightRows.Add($row)
            }
        }

        if ($leftRows.Count -gt $rightRows.Count) {
            for ($i = $rightRows.Count; $i -lt $leftRows.Count; $i++) {
                $missing.Add([pscustomobject]@{ key = $key; row = $leftRows[$i] })
            }
        }
        elseif ($rightRows.Count -gt $leftRows.Count) {
            for ($i = $leftRows.Count; $i -lt $rightRows.Count; $i++) {
                $extra.Add([pscustomobject]@{ key = $key; row = $rightRows[$i] })
            }
        }
    }

    [pscustomobject]@{
        equal = $missing.Count -eq 0 -and $extra.Count -eq 0
        left_count = @($Left).Count
        right_count = @($Right).Count
        missing = @($missing | ForEach-Object { $_ })
        extra = @($extra | ForEach-Object { $_ })
    }
}

$kparser = Read-JsonFile $KparserJson
$kparser2 = Read-JsonFile $Kparser2Json
$kparserInteractions = Get-KparserRows $kparser "interaction"
$kparser2Interactions = Get-Kparser2Rows $kparser2 "interaction"
$interactionComparison = Compare-Multiset $kparserInteractions $kparser2Interactions ${function:Get-InteractionKey}

$chatComparison = $null
if (-not $SkipChat) {
    $kparserChat = Get-KparserRows $kparser "chat"
    $kparser2Chat = Get-Kparser2Rows $kparser2 "chat"
    $chatComparison = Compare-Multiset $kparserChat $kparser2Chat ${function:Get-ChatKey}
}

$equal = $interactionComparison.equal -and ($SkipChat -or $chatComparison.equal)
$report = [ordered]@{
    schema_version = 1
    status = if ($equal) { "equal" } else { "mismatch" }
    generated_at = [DateTimeOffset]::UtcNow.ToString("O")
    sources = [ordered]@{
        kparser = [System.IO.Path]::GetFullPath($KparserJson)
        kparser2 = [System.IO.Path]::GetFullPath($Kparser2Json)
    }
    interactions = $interactionComparison
    chat = if ($SkipChat) { $null } else { $chatComparison }
}

$json = $report | ConvertTo-Json -Depth 8
if (-not [string]::IsNullOrWhiteSpace($OutputPath)) {
    $outputDirectory = Split-Path -Parent $OutputPath
    if (-not [string]::IsNullOrWhiteSpace($outputDirectory)) {
        New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
    }
    Set-Content -LiteralPath $OutputPath -Value $json -Encoding utf8
}

Write-Output $json
if (-not $equal) {
    exit 1
}

exit 0
