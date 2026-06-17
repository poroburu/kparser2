param(
    [string]$SqlPath = (Join-Path $PSScriptRoot "..\..\server\sql\mob_pool.sql"),
    [string]$OutputPath = (Join-Path $PSScriptRoot "..\data\mob_xp.json")
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $SqlPath)) {
    Write-Host "No SQL source at $SqlPath; keeping existing mob_xp.json if present."
    exit 0
}

$mobs = [ordered]@{}
$pattern = "INSERT INTO ``mob_pool`` VALUES \('([^']+)'"

Get-Content $SqlPath -Encoding UTF8 | ForEach-Object {
    if ($_ -match $pattern) {
        $name = $Matches[1] -replace '_', ' '
        if (-not $mobs.Contains($name)) {
            $mobs[$name] = 100
        }
    }
}

$outputDir = Split-Path $OutputPath -Parent
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir | Out-Null
}

$mobs | ConvertTo-Json -Depth 2 | Set-Content -Path $OutputPath -Encoding UTF8
Write-Host "Exported $($mobs.Count) mob names to $OutputPath"
