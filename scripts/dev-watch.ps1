# Run kparser2 WPF with dotnet watch (auto-rebuild on file changes).
# Close any running kparser2.exe first to avoid DLL lock errors.
param(
    [switch]$HotReload
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "kparser2\kparser2.csproj"

# F# projects are not supported by dotnet hot reload; enabling it spams fsproj warnings.
# Default: --no-hot-reload (full restart on change, works for C# + F#).
$watchArgs = @("watch", "run", "--project", $project)

if (-not $HotReload) {
    $watchArgs += "--no-hot-reload"
}

Write-Host "Starting kparser2 dev watch..."
if ($HotReload) {
    Write-Host "  Hot reload ON (C# only; expect F# fsproj warnings)"
}
else {
    Write-Host "  Hot reload OFF - app restarts on any C# or F# change (recommended)"
}
Write-Host "  Live ZMQ session is lost on restart - use Session, Replay for UI work"
Write-Host "  Press Ctrl+R in the watch terminal to force restart"
Write-Host ""

Set-Location $root
dotnet @watchArgs
