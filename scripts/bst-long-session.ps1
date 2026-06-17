param(
    [int]$DurationMinutes = 20,
    [string]$OutputDir = "C:\Users\porob\git\ffxi-captures\ndjson"
)

$ErrorActionPreference = "Stop"
$cli = "C:\Users\porob\git\kparser2\kparser2.Cli\kparser2.Cli.fsproj"
$durationMs = $DurationMinutes * 60 * 1000
$stamp = Get-Date -Format "yyyyMMdd_HHmm"
$output = Join-Path $OutputDir "bst_leveling_$stamp.ndjson"

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

Write-Host "Recording BST session for $DurationMinutes minutes -> $output"
dotnet run --project $cli -- probe
dotnet run --project $cli -- record $output --duration-ms $durationMs

Write-Host "Post-session validation"
dotnet run --project $cli -- analytics snapshot $output --assert-combat
dotnet run --project $cli -- report fights $output
dotnet run --project $cli -- report offense $output
dotnet run --project $cli -- report performance $output
dotnet run --project $cli -- report experience $output

Write-Host "Capture: $output"
