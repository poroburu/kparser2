param(
    [string]$SqlPath = (Join-Path $PSScriptRoot "..\..\server\sql\spell_list.sql"),
    [string]$OutputPath = (Join-Path $PSScriptRoot "..\data\spells.json")
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $SqlPath)) {
    throw "SQL file not found: $SqlPath"
}

$spells = [ordered]@{}
# SQL columns: spellid, name, jobs (one hex literal), group, family, element,
# zonemisc, validTargets, skill, mpCost. Element/skill may be SQL variables.
$pattern = "^INSERT INTO ``spell_list`` VALUES \((\d+),'([^']+)',0x[0-9A-Fa-f]+(?:,(?:@\w+|\d+)){6},(\d+),"

Get-Content $SqlPath -Encoding UTF8 | ForEach-Object {
    if ($_ -match $pattern) {
        $id = [int]$Matches[1]
        $name = $Matches[2] -replace '_', ' '
        $spells["$id"] = [ordered]@{ name = $name; mpCost = [int]$Matches[3] }
    } elseif ($_ -match '^INSERT INTO `spell_list`') {
        throw "Unrecognized spell INSERT; update the exporter for this SQL schema."
    } elseif ($_ -match "^--\s+INSERT INTO ``spell_list`` VALUES \((\d+),'([^']+)'" ) {
        # Preserve the old name lookup for disabled SQL rows, without claiming
        # their proposed MP costs are implemented.
        $spells[$Matches[1]] = [ordered]@{ name = ($Matches[2] -replace '_', ' '); mpCost = $null }
    }
}

if ($spells.Count -eq 0) { throw "No spell rows found in $SqlPath" }
$outputDir = Split-Path $OutputPath -Parent
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir | Out-Null
}

$spells | ConvertTo-Json -Depth 2 | Set-Content -Path $OutputPath -Encoding UTF8
Write-Host "Exported $($spells.Count) spells to $OutputPath"
