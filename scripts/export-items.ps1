param(
    [string]$SqlPath = (Join-Path $PSScriptRoot "..\..\server\sql\item_basic.sql"),
    [string]$OutputPath = (Join-Path $PSScriptRoot "..\data\items.json")
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $SqlPath)) {
    throw "SQL file not found: $SqlPath"
}

$items = [ordered]@{}
$pattern = "INSERT INTO ``item_basic`` VALUES \((\d+),0,'([^']+)'"

Get-Content $SqlPath -Encoding UTF8 | ForEach-Object {
    if ($_ -match $pattern) {
        $id = [int]$Matches[1]
        $name = $Matches[2] -replace '_', ' '
        $items["$id"] = $name
    }
}

$outputDir = Split-Path $OutputPath -Parent
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir | Out-Null
}

$items | ConvertTo-Json -Depth 2 | Set-Content -Path $OutputPath -Encoding UTF8
Write-Host "Exported $($items.Count) items to $OutputPath"
