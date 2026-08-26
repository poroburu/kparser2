# Compare kparser vs kparser2 chat parity arrays (order-insensitive).
# Accepts a root JSON array, kparser snapshot (.parity.chat), or kparser2 snapshot (.ChatMessages incoming).
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$KparserJson,
    [Parameter(Mandatory = $true, Position = 1)]
    [string]$Kparser2Json
)

$ErrorActionPreference = "Stop"

function Get-ChatRows([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        throw "File not found: $Path"
    }

    $raw = Get-Content -LiteralPath $Path -Raw
    $j = $raw | ConvertFrom-Json
    if ($null -eq $j) {
        throw "Empty JSON: $Path"
    }

    if ($raw.TrimStart().StartsWith("[")) {
        return @($j)
    }

    $names = @($j.PSObject.Properties.Name)
    if ($names -contains "parity" -and $null -ne $j.parity.chat) {
        return @($j.parity.chat)
    }

    if ($names -contains "ChatMessages") {
        return @(
            $j.ChatMessages |
            Where-Object { -not $_.Direction -or $_.Direction -eq "incoming" } |
            ForEach-Object {
                $speaker = if ([string]::IsNullOrWhiteSpace($_.Speaker)) { "System" } else { $_.Speaker }
                [pscustomobject]@{
                    speaker = $speaker
                    mode    = $_.Mode
                    message = $_.Message
                }
            }
        )
    }

    throw "No chat rows in $Path (expected array, parity.chat, or ChatMessages)"
}

function Get-RowKey($row) {
    "{0}`t{1}`t{2}" -f [string]$row.speaker, [string]$row.mode, [string]$row.message
}

$left = @(Get-ChatRows $KparserJson | ForEach-Object { Get-RowKey $_ } | Sort-Object)
$right = @(Get-ChatRows $Kparser2Json | ForEach-Object { Get-RowKey $_ } | Sort-Object)

$diff = Compare-Object -ReferenceObject $left -DifferenceObject $right
if ($diff) {
    Write-Host "chat parity MISMATCH"
    foreach ($row in $diff) {
        $side = if ($row.SideIndicator -eq "<=") { "kparser" } else { "kparser2" }
        Write-Host ("  [{0}] {1}" -f $side, $row.InputObject)
    }
    exit 1
}

Write-Host ("chat parity OK ({0} rows)" -f $left.Count)
exit 0
