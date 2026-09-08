$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'parity-evidence.ps1')

function Assert-Throws([scriptblock]$Action) {
    $failed = $false
    try { & $Action } catch { $failed = $true }
    if (-not $failed) { throw 'Expected invalid evidence to be rejected.' }
}

$boundary = [pscustomobject]@{ mode = 'exact'; quality = 'exact'; session_uuid = 'session-a'; message_id = [UInt64]0 }
$argsExact = @{ CaptureBoundary = $boundary; SessionUuid = 'session-a'; BoundaryMessageId = [UInt64]0; BoundaryMode = 'exact'; BoundaryQuality = 'exact'; HasMessageId = $true }
Assert-ExactBoundary @argsExact
$boundary.message_id = 1
Assert-Throws { Assert-ExactBoundary @argsExact }
$boundary.message_id = $null
Assert-Throws { Assert-ExactBoundary @argsExact }
$boundary.message_id = 0
$argsExact.SessionUuid = ''
Assert-Throws { Assert-ExactBoundary @argsExact }
$argsExact.SessionUuid = 'session-a'
$argsExact.HasMessageId = $false
Assert-Throws { Assert-ExactBoundary @argsExact }
$argsExact.HasMessageId = $true
$argsExact.BoundaryQuality = 'unavailable'
Assert-Throws { Assert-ExactBoundary @argsExact }

$row = [pscustomobject]@{ reset_id = 'reset-a'; session_uuid = 'session-a'; boundary_message_id = 0; boundary_mode = 'exact'; boundary_quality = 'exact'; evidence_source = 'human'; surface = 'offense' }
$window = @{ Rows = @($row); ResetId = 'reset-a'; SessionUuid = 'session-a'; BoundaryMessageId = 0; BoundaryMode = 'exact' }
Assert-ObservationWindow @window
$row.reset_id = 'reset-b'
Assert-Throws { Assert-ObservationWindow @window }
$row.reset_id = 'reset-a'
$row.session_uuid = ''
Assert-Throws { Assert-ObservationWindow @window }
$row.session_uuid = 'session-a'
$row.boundary_message_id = 1
Assert-Throws { Assert-ObservationWindow @window }
$row.boundary_message_id = 0
$row | Add-Member -NotePropertyName capture_path -NotePropertyValue (Join-Path $PSScriptRoot 'window.ndjson')
$window.CapturePath = Join-Path $PSScriptRoot 'window.complete.ndjson'
Assert-ObservationWindow @window
$window.CapturePath = Join-Path $PSScriptRoot 'other.ndjson'
Assert-Throws { Assert-ObservationWindow @window }

$qa = New-QaSummary -ParityExit 0 -ReportExit 0 -Rows @() -Boundary $boundary
if ($qa.state.status -ne 'equal' -or $qa.ui.status -ne 'unobserved' -or $qa.human.status -ne 'unobserved') { throw 'Missing evidence must not pass.' }
$qa = New-QaSummary -ParityExit 1 -ReportExit 0 -Rows @($row) -Boundary $boundary
if ($qa.state.status -ne 'mismatch' -or $qa.ui.status -ne 'unobserved' -or $qa.human.status -ne 'recorded') { throw 'Human evidence must remain separate from UI and state.' }
$row.evidence_source = 'ui'
$qa = New-QaSummary -ParityExit 0 -ReportExit 0 -Rows @($row) -Boundary $boundary
if ($qa.ui.status -ne 'recorded' -or $qa.human.status -ne 'unobserved') { throw 'UI inspection must not imply human acceptance.' }

$temp = Join-Path ([IO.Path]::GetTempPath()) ('parity-evidence-' + [guid]::NewGuid().ToString('N') + '.jsonl')
try {
    '{"type":"kparser2.session","boundary_mode":"exact","boundary_quality":"exact","boundary_session_uuid":"session-a"}' | Set-Content -LiteralPath $temp
    $missingCursor = Read-CaptureBoundary $temp
    if ($null -ne $missingCursor.message_id) { throw 'Missing header cursor was fabricated.' }
    $valid = @{ observed_at_utc = '2026-09-08T21:00:00Z'; application = 'kparser2'; surface = 'offense'; classification = 'unclassified'; summary = 'Original human wording' }
    $valid | ConvertTo-Json -Compress | Set-Content -LiteralPath $temp
    $inputRows = Read-UiObservations $temp
    if ($inputRows.rows.Count -ne 1 -or $inputRows.rows[0].evidence_source -ne 'human' -or $inputRows.rows[0].summary -ne $valid.summary) { throw 'Legacy human observation was not preserved.' }
    $valid.classification = 'typo'
    $valid | ConvertTo-Json -Compress | Set-Content -LiteralPath $temp
    Assert-Throws { Read-UiObservations $temp }
    $valid.classification = 'unclassified'
    $valid.observed_at_utc = 'not a date'
    $valid | ConvertTo-Json -Compress | Set-Content -LiteralPath $temp
    Assert-Throws { Read-UiObservations $temp }
} finally {
    Remove-Item -LiteralPath $temp -Force
}
Write-Output 'parity evidence self-test passed'
