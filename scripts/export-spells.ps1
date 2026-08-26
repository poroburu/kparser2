param(
    [string]$SqlPath = (Join-Path $PSScriptRoot "..\..\server\sql\spell_list.sql"),
    [string]$OutputPath = (Join-Path $PSScriptRoot "..\data\spells.json")
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $SqlPath)) {
    throw "SQL file not found: $SqlPath"
}

$spells = [ordered]@{}
$pattern = "INSERT INTO ``spell_list`` VALUES \((\d+),'([^']+)'"

Get-Content $SqlPath -Encoding UTF8 | ForEach-Object {
    if ($_ -match $pattern) {
        $id = [int]$Matches[1]
        $name = $Matches[2] -replace '_', ' '
        $spells["$id"] = $name
    }
}

$outputDir = Split-Path $OutputPath -Parent
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir | Out-Null
}

$spells | ConvertTo-Json -Depth 2 | Set-Content -Path $OutputPath -Encoding UTF8
Write-Host "Exported $($spells.Count) spells to $OutputPath"
