param(
    [string]$SqlPath = (Join-Path $PSScriptRoot "..\..\server\sql\zone_settings.sql"),
    [string]$OutputPath = (Join-Path $PSScriptRoot "..\data\zones.json")
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $SqlPath)) {
    throw "SQL file not found: $SqlPath"
}

$zones = [ordered]@{}
$pattern = "INSERT INTO ``zone_settings`` VALUES \((\d+),[^,]+,[^,]+,[^,]+,'([^']+)'"

Get-Content $SqlPath -Encoding UTF8 | ForEach-Object {
    if ($_ -match $pattern) {
        $id = [int]$Matches[1]
        $name = $Matches[2] -replace '_', ' '
        $zones["$id"] = $name
    }
}

$outputDir = Split-Path $OutputPath -Parent
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir | Out-Null
}

$zones | ConvertTo-Json -Depth 2 | Set-Content -Path $OutputPath -Encoding UTF8
Write-Host "Exported $($zones.Count) zones to $OutputPath"
