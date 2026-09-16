function Read-UiObservations {
    param([string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return [pscustomobject]@{ path = ""; rows = @() }
    }

    if ([System.IO.Path]::IsPathRooted($Path)) {
        $resolvedPath = [System.IO.Path]::GetFullPath($Path)
    }
    else {
        $resolvedPath = [System.IO.Path]::GetFullPath((Join-Path $root $Path))
    }

    if (-not (Test-Path -LiteralPath $resolvedPath -PathType Leaf)) {
        throw "UI observation file not found: $resolvedPath"
    }

    $rows = @()
    $lineNumber = 0
    foreach ($line in Get-Content -LiteralPath $resolvedPath) {
        $lineNumber++
        if ([string]::IsNullOrWhiteSpace($line)) {
            continue
        }

        try {
            $row = $line | ConvertFrom-Json
        }
        catch {
            throw "Invalid UI observation JSON at $resolvedPath line $lineNumber`: $($_.Exception.Message)"
        }

        foreach ($propertyName in @("observed_at_utc", "application", "surface", "classification", "summary")) {
            if ($null -eq $row.PSObject.Properties[$propertyName] -or
                [string]::IsNullOrWhiteSpace([string]$row.$propertyName)) {
                throw "UI observation at $resolvedPath line $lineNumber is missing '$propertyName'."
            }
        }

        if ([string]$row.classification -notin @("kparser-only", "kparser2-missing", "kparser2-extra", "rendering-only", "deferred", "unclassified")) {
            throw "Unknown observation classification at $resolvedPath line $lineNumber."
        }
        if ([string]$row.application -notin @("kparser", "kparser2")) {
            throw "Unknown observation application at $resolvedPath line $lineNumber."
        }
        $timestamp = [DateTimeOffset]::MinValue
        if (-not [DateTimeOffset]::TryParse([string]$row.observed_at_utc, [ref]$timestamp)) {
            throw "Invalid observation timestamp at $resolvedPath line $lineNumber."
        }
        # Older observations were manually recorded; never infer automated UI coverage.
        if ($null -eq $row.PSObject.Properties['evidence_source']) {
            $row | Add-Member -NotePropertyName evidence_source -NotePropertyValue human
        }
        if ([string]$row.evidence_source -notin @("human", "ui")) {
            throw "Unknown evidence source at $resolvedPath line $lineNumber."
        }
        $rows += $row
    }

    return [pscustomobject]@{
        path = $resolvedPath
        rows = @($rows)
    }
}

function Read-CaptureBoundary {
    param([string]$Path)

    $line = Get-Content -LiteralPath $Path -TotalCount 1
    if ([string]::IsNullOrWhiteSpace($line)) {
        return [pscustomobject]@{
            mode = "none"
            quality = "unavailable"
            reason = "capture has no session header"
            session_uuid = ""
            message_id = 0
        }
    }

    try {
        $header = $line | ConvertFrom-Json
    }
    catch {
        return [pscustomobject]@{
            mode = "none"
            quality = "unavailable"
            reason = "capture session header is invalid"
            session_uuid = ""
            message_id = 0
        }
    }

    if ([string]$header.type -ne "kparser2.session") {
        return [pscustomobject]@{
            mode = "none"
            quality = "unavailable"
            reason = "capture session header is missing"
            session_uuid = ""
            message_id = 0
        }
    }

    return [pscustomobject]@{
        mode = if ($header.boundary_mode) { [string]$header.boundary_mode } else { "none" }
        quality = if ($header.boundary_quality) { [string]$header.boundary_quality } else { "unavailable" }
        reason = if ($header.boundary_reason) { [string]$header.boundary_reason } else { "" }
        session_uuid = if ($header.boundary_session_uuid) { [string]$header.boundary_session_uuid } else { "" }
        message_id = if ($null -ne $header.boundary_message_id) { [UInt64]$header.boundary_message_id } else { $null }
    }
}

function New-UiComparison {
    param(
        [string]$Path,
        [object[]]$Rows,
        [string]$ResetId,
        [string]$ResetBoundaryUtc,
        [string]$SessionUuid,
        [UInt64]$BoundaryMessageId,
        [string]$BoundaryMode,
        [string]$BoundaryQuality,
        [string]$BoundaryReason
    )

    $semanticClassifications = @(
        "kparser-only",
        "kparser2-missing",
        "kparser2-extra"
    )
    $renderingRows = @($Rows | Where-Object {
        [string]$_.classification -ieq "rendering-only"
    })
    $semanticRows = @($Rows | Where-Object {
        $semanticClassifications -contains ([string]$_.classification).ToLowerInvariant()
    })
    $deferredRows = @($Rows | Where-Object {
        [string]$_.classification -ieq "deferred"
    })
    $unclassifiedRows = @($Rows | Where-Object {
        [string]$_.classification -ieq "unclassified"
    })

    $byClassification = [ordered]@{}
    foreach ($row in @($Rows)) {
        $classification = [string]$row.classification
        if (-not $byClassification.Contains($classification)) {
            $byClassification[$classification] = 0
        }
        $byClassification[$classification]++
    }

    return [ordered]@{
        schema_version = 1
        observations_path = $Path
        reset_id = $ResetId
        reset_boundary_utc = $ResetBoundaryUtc
        session_uuid = $SessionUuid
        boundary_message_id = $BoundaryMessageId
        boundary_mode = $BoundaryMode
        boundary_quality = $BoundaryQuality
        boundary_reason = $BoundaryReason
        observation_count = @($Rows).Count
        rendering_only_count = $renderingRows.Count
        semantic_count = $semanticRows.Count
        deferred_count = $deferredRows.Count
        unclassified_count = $unclassifiedRows.Count
        by_classification = $byClassification
        rendering_only = @($renderingRows)
        semantic = @($semanticRows)
        deferred = @($deferredRows)
        unclassified = @($unclassifiedRows)
    }
}

function Assert-ExactBoundary {
    param($CaptureBoundary, [string]$SessionUuid, [UInt64]$BoundaryMessageId,
        [string]$BoundaryMode, [string]$BoundaryQuality, [bool]$HasMessageId)
    if ($BoundaryMode -ne 'exact' -or $BoundaryQuality -ne 'exact' -or
        $CaptureBoundary.mode -ne 'exact' -or $CaptureBoundary.quality -ne 'exact') {
        throw 'Exact comparison requires exact mode and quality on both reset and capture.'
    }
    if ([string]::IsNullOrWhiteSpace($SessionUuid) -or -not $HasMessageId -or
        $null -eq $CaptureBoundary.message_id) {
        throw 'Exact comparison requires an explicit session UUID and message cursor on both inputs.'
    }
    if ($CaptureBoundary.session_uuid -cne $SessionUuid -or
        [UInt64]$CaptureBoundary.message_id -ne $BoundaryMessageId) {
        throw 'Capture boundary does not match the reset session and cursor.'
    }
}

function Assert-ObservationWindow {
    param([object[]]$Rows, [string]$ResetId, [string]$SessionUuid,
        [UInt64]$BoundaryMessageId, [string]$BoundaryMode, [string]$CapturePath = '')
    foreach ($row in $Rows) {
        if ($null -eq $row) { continue }
        if ($CapturePath) {
            # Reconciliation preserves the source basename with a .complete suffix.
            $expected = [IO.Path]::GetFullPath($CapturePath) -replace '\.complete\.ndjson$', '.ndjson'
            if ([string]::IsNullOrWhiteSpace([string]$row.capture_path)) {
                throw 'Observation is missing its capture path.'
            }
            $actualPath = [IO.Path]::GetFullPath([string]$row.capture_path) -replace '\.complete\.ndjson$', '.ndjson'
            if ($actualPath -ine $expected) { throw 'Observation belongs to a different capture.' }
            if ($null -ne $row.ui_evidence) {
                $linked = Read-UiRun -Path $row.ui_evidence.manifest_path -CapturePath $CapturePath
                if ((Get-FileHash -LiteralPath $linked.path -Algorithm SHA256).Hash -ine $row.ui_evidence.manifest_sha256) {
                    throw 'The UI bundle changed after this observation was recorded.'
                }
                $matches = @($linked.manifest.cases | Where-Object { "$($_.surface)/$($_.scenario)" -ceq $row.ui_evidence.case })
                if ($matches.Count -ne 1 -or $matches[0].surface -ine $row.surface -or
                    $matches[0].screenshot -cne $row.ui_evidence.screenshot -or $row.application -ne 'kparser2') {
                    throw 'Observation UI evidence does not match its surface and screenshot.'
                }
            }
        }
        foreach ($pair in @(@('reset_id', $ResetId), @('session_uuid', $SessionUuid))) {
            $actual = [string]$row.($pair[0])
            if (-not [string]::IsNullOrWhiteSpace($pair[1]) -and
                (($actual -and $actual -cne $pair[1]) -or ($BoundaryMode -eq 'exact' -and -not $actual))) {
                throw "Observation $($pair[0]) does not match the comparison window."
            }
        }
        if ($BoundaryMode -eq 'exact' -and
            ($row.boundary_mode -ne 'exact' -or $row.boundary_quality -ne 'exact' -or
             $null -eq $row.boundary_message_id -or
             [UInt64]$row.boundary_message_id -ne $BoundaryMessageId)) {
            throw 'Observation does not have the same exact boundary as the comparison.'
        }
    }
}

function New-QaSummary {
    param([int]$ParityExit, [int]$ReportExit, [object[]]$Rows, $Boundary, $UiRun = $null)
    $layers = [ordered]@{}
    foreach ($source in @('ui', 'human')) {
        $observations = @($Rows | Where-Object { $null -ne $_ -and $_.evidence_source -eq $source })
        $layers[$source] = [ordered]@{
            status = if ($observations.Count) { 'recorded' } else { 'unobserved' }
            observation_count = $observations.Count
            surfaces = @($observations | ForEach-Object { $_.surface } | Sort-Object -Unique)
        }
    }
    if ($null -ne $UiRun) {
        $layers.ui = [ordered]@{
            status = $UiRun.manifest.status
            manifest_path = $UiRun.path
            case_count = $UiRun.manifest.case_count
            failure_count = $UiRun.manifest.failure_count
            surfaces = @($UiRun.manifest.cases | ForEach-Object { $_.surface } | Sort-Object -Unique)
            skipped = @($UiRun.manifest.skipped)
            limitations = @($UiRun.manifest.limitations)
        }
    }
    return [ordered]@{
        schema_version = 1
        boundary = $Boundary
        state = [ordered]@{
            status = if ($ParityExit -eq 0 -and $ReportExit -eq 0) { 'equal' } else { 'mismatch' }
            parity_exit = $ParityExit
            report_exit = $ReportExit
        }
        ui = $layers.ui
        human = $layers.human
        note = 'State equality is not UI or human approval. Recorded observations require reconciliation; no overall parity verdict is inferred.'
    }
}

function Read-UiRun {
    param([string]$Path, [string]$CapturePath)
    if ([string]::IsNullOrWhiteSpace($Path)) { return $null }
    $resolved = (Resolve-Path -LiteralPath $Path -ErrorAction Stop).Path
    $manifest = Get-Content -LiteralPath $resolved -Raw | ConvertFrom-Json
    if ($manifest.schema_version -ne 1 -or $manifest.evidence_source -ne 'wpf-replay' -or
        $manifest.status -notin @('passed', 'failed') -or @($manifest.cases).Count -eq 0 -or
        $manifest.case_count -ne @($manifest.cases).Count) {
        throw 'Invalid WPF replay evidence manifest.'
    }
    $hash = (Get-FileHash -LiteralPath $CapturePath -Algorithm SHA256).Hash
    if ($hash -ine $manifest.capture_sha256) { throw 'UI replay evidence belongs to different capture bytes.' }
    $directory = Split-Path -Parent $resolved
    $prefix = $directory.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    function Resolve-Artifact([string]$RelativePath) {
        if ([string]::IsNullOrWhiteSpace($RelativePath) -or [IO.Path]::IsPathRooted($RelativePath)) { throw 'UI artifact path must be relative.' }
        $artifact = [IO.Path]::GetFullPath((Join-Path $directory $RelativePath))
        if (-not $artifact.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase) -or
            -not (Test-Path -LiteralPath $artifact -PathType Leaf)) { throw 'UI evidence artifact is missing or outside its run directory.' }
        return $artifact
    }
    $frozen = Resolve-Artifact $manifest.frozen_capture
    if ((Get-FileHash -LiteralPath $frozen -Algorithm SHA256).Hash -ine $hash) { throw 'Frozen UI capture hash does not match.' }
    $null = Resolve-Artifact $manifest.state_path
    $failures = 0
    $identities = @{}
    foreach ($case in $manifest.cases) {
        $identity = "$($case.surface)/$($case.scenario)"
        if (-not $case.surface -or -not $case.scenario -or $identities.ContainsKey($identity)) { throw 'UI cases require unique surface/scenario identities.' }
        $identities[$identity] = $true
        if ($case.status -eq 'failed') { $failures++; continue }
        if ($case.status -ne 'passed') { throw 'Invalid UI case status.' }
        $null = Resolve-Artifact $case.screenshot
        $actual = [IO.File]::ReadAllText((Resolve-Artifact $case.actual_text)).Replace("`r`n", "`n").TrimEnd("`r", "`n")
        $expected = [IO.File]::ReadAllText((Resolve-Artifact $case.expected_text)).Replace("`r`n", "`n").TrimEnd("`r", "`n")
        if ($actual -cne $expected) { throw 'A passed UI case contains mismatched text artifacts.' }
    }
    if ($manifest.failure_count -ne $failures -or ($manifest.status -eq 'passed') -ne ($failures -eq 0)) {
        throw 'UI manifest verdict disagrees with its cases.'
    }
    return [pscustomobject]@{ path = $resolved; manifest = $manifest }
}
