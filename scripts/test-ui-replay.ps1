[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$CapturePath,
    [string]$OutputDir = '',
    [switch]$NoBuild
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$capture = (Resolve-Path -LiteralPath $CapturePath).Path
if (-not $OutputDir) {
    $OutputDir = Join-Path $repo ('artifacts/ui-qa/' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0,8))
}
$output = [IO.Path]::GetFullPath($OutputDir)
if (Test-Path -LiteralPath $output) { throw 'OutputDir must be new to preserve earlier QA evidence.' }
$build = Join-Path $repo 'artifacts/ui-qa-build'
if (-not $NoBuild) {
    & dotnet build (Join-Path $repo 'kparser2.Ui.Qa/kparser2.Ui.Qa.csproj') --artifacts-path $build --verbosity quiet
    if ($LASTEXITCODE -ne 0) { throw "UI QA build failed: $LASTEXITCODE" }
}
$exe = Join-Path $build 'bin/kparser2.Ui.Qa/debug/kparser2.Ui.Qa.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw "QA executable not found: $exe" }
# Logs live beside the new run directory, which the runner owns.
$parent = Split-Path -Parent $output
New-Item -ItemType Directory -Force -Path $parent | Out-Null
$process = Start-Process -FilePath $exe -WindowStyle Hidden -PassThru `
    -ArgumentList @(('"' + $capture + '"'), ('"' + $output + '"')) `
    -RedirectStandardOutput ($output + '.log') -RedirectStandardError ($output + '.error.log')
# Retain the native handle before exit so Windows PowerShell can retrieve ExitCode.
$processHandle = $process.Handle
try {
    if (-not $process.WaitForExit(300000)) { throw 'UI QA exceeded its five-minute deadline.' }
    $process.Refresh()
    if ($process.ExitCode -ne 0) { throw "UI QA failed: $($process.ExitCode). Inspect $output and $output.error.log" }
    Write-Output "UI evidence: $(Join-Path $output 'ui-run.json')"
} finally {
    if (-not $process.HasExited) { Stop-Process -Id $process.Id }
    $process.Dispose()
}
