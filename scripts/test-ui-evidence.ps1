$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'parity-evidence.ps1')
function Assert-Throws([scriptblock]$Action) {
    $failed = $false
    try { & $Action } catch { $failed = $true }
    if (-not $failed) { throw 'Expected invalid UI evidence to be rejected.' }
}
$tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
$temp = [IO.Path]::GetFullPath((Join-Path $tempRoot ('kparser2-ui-evidence-' + [guid]::NewGuid().ToString('N'))))
if (-not $temp.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe test directory.' }
New-Item -ItemType Directory -Path $temp | Out-Null
try {
    $capture = Join-Path $temp 'capture.ndjson'
    Set-Content -LiteralPath $capture -Value '{"fixture":"UI evidence contract only"}'
    Set-Content -LiteralPath (Join-Path $temp 'state.json') -Value '{}'
    Set-Content -LiteralPath (Join-Path $temp 'actual.txt') -Value 'Displayed row'
    Set-Content -LiteralPath (Join-Path $temp 'expected.txt') -Value 'Displayed row'
    # Only artifact presence is checked here; the real runner produces/visually checks PNGs.
    Set-Content -LiteralPath (Join-Path $temp 'viewport.png') -Value 'test placeholder'
    $manifest = @{
        schema_version = 1; evidence_source = 'wpf-replay'; status = 'passed';
        capture_sha256 = (Get-FileHash -LiteralPath $capture -Algorithm SHA256).Hash;
        frozen_capture = 'capture.ndjson'; state_path = 'state.json'; case_count = 1; failure_count = 0;
        cases = @(@{ surface = 'offense'; scenario = 'default'; status = 'passed'; screenshot = 'viewport.png'; actual_text = 'actual.txt'; expected_text = 'expected.txt' });
        skipped = @(); limitations = @('contract fixture')
    }
    $path = Join-Path $temp 'ui-run.json'
    function Save-Manifest { $manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $path }
    Save-Manifest
    $run = Read-UiRun $path $capture
    $qa = New-QaSummary -ParityExit 0 -ReportExit 0 -Rows @() -Boundary $null -UiRun $run
    if ($qa.ui.status -ne 'passed' -or $qa.human.status -ne 'unobserved') { throw 'UI pass must not imply human review.' }
    $observationPath = Join-Path $temp 'observations.jsonl'
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'record-ui-observation.ps1') `
        -CapturePath $capture -ObservationPath $observationPath -Application kparser2 -Surface offense `
        -Classification unclassified -Summary 'Contract test only, not real human feedback' -UiRun $path -UiCase offense/default | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Could not record linked test observation.' }
    $observations = Read-UiObservations $observationPath
    Assert-ObservationWindow -Rows $observations.rows -CapturePath $capture -BoundaryMode none
    $manifest.limitations = @('changed after review')
    Save-Manifest
    Assert-Throws { Assert-ObservationWindow -Rows $observations.rows -CapturePath $capture -BoundaryMode none }
    $manifest.capture_sha256 = 'wrong'
    Save-Manifest
    Assert-Throws { Read-UiRun $path $capture }
    $manifest.capture_sha256 = (Get-FileHash -LiteralPath $capture -Algorithm SHA256).Hash
    $manifest.cases[0].screenshot = '../outside.png'
    Save-Manifest
    Assert-Throws { Read-UiRun $path $capture }
    $manifest.cases[0].screenshot = 'missing.png'
    Save-Manifest
    Assert-Throws { Read-UiRun $path $capture }
    $manifest.cases[0].screenshot = 'viewport.png'
    Save-Manifest
    Set-Content -LiteralPath (Join-Path $temp 'actual.txt') -Value 'Wrong displayed row'
    Assert-Throws { Read-UiRun $path $capture }
    $manifest.cases[0].status = 'failed'
    Save-Manifest
    Assert-Throws { Read-UiRun $path $capture }
    $manifest.failure_count = 1; $manifest.status = 'failed'
    Save-Manifest
    $run = Read-UiRun $path $capture
    if ($run.manifest.status -ne 'failed') { throw 'Failed run lost its verdict.' }
    $manifest.cases = @(); $manifest.case_count = 0; $manifest.failure_count = 0; $manifest.status = 'passed'
    Save-Manifest
    Assert-Throws { Read-UiRun $path $capture }
    Write-Output 'UI evidence self-test passed'
} finally {
    Remove-Item -LiteralPath $temp -Recurse -Force
}
