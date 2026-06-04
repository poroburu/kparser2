param(
    [string]$SqlPath = (Join-Path $PSScriptRoot "..\..\server\sql\abilities.sql"),
    [string]$OutputPath = (Join-Path $PSScriptRoot "..\data\actions.json")
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $SqlPath)) {
    Write-Host "No SQL source at $SqlPath; writing minimal actions.json seed."
    @'
{
  "1": "Attack",
  "2": "Ranged Attack",
  "7": "Weaponskill"
}
'@ | Set-Content -Path $OutputPath -Encoding UTF8
    exit 0
}

$actions = [ordered]@{}
$commandTypes = [ordered]@{
    "1"  = "Attack"
    "2"  = "Ranged Attack"
    "3"  = "Magic Attack"
    "4"  = "Magic Attack"
    "5"  = "Magic Attack"
    "6"  = "Magic Attack"
    "7"  = "Weaponskill"
    "8"  = "Weaponskill"
    "9"  = "Weaponskill"
    "10" = "Weaponskill"
    "11" = "Weaponskill"
    "12" = "Weaponskill"
    "13" = "Ability"
    "14" = "Ability"
    "15" = "Ability"
}

foreach ($key in $commandTypes.Keys) {
    $actions[$key] = $commandTypes[$key]
}

$pattern = "INSERT INTO ``abilities`` VALUES \((\d+),'([^']+)'"

Get-Content $SqlPath -Encoding UTF8 | ForEach-Object {
    if ($_ -match $pattern) {
        $id = [int]$Matches[1]
        $name = $Matches[2] -replace '_', ' '
        $actions["$id"] = $name
    }
}

$outputDir = Split-Path $OutputPath -Parent
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir | Out-Null
}

$actions | ConvertTo-Json -Depth 2 | Set-Content -Path $OutputPath -Encoding UTF8
Write-Host "Exported $($actions.Count) actions to $OutputPath"
