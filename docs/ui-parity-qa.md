# UI parity QA

QA runs at three resolutions: state, UI, and human. The packet stream and raw
ChatLines remain authoritative. A visible
difference is a lead to reconcile, not proof that either parser is correct.

## Three-resolution plan

1. **State:** compare normalized interactions/chat and report calculations on
   paired captures. Preserve duplicates and record boundary quality. Equality
   means the compared projections agree; it does not establish UI correctness.
2. **UI:** inspect visible rows, selected filters, refresh, and ordering against
   the state from that window. Record direct agent/automation inspections with
   `-EvidenceSource ui`; include the surface and filter context in `Notes`.
   A control endpoint responding to status/reset does not prove rendering.
3. **Human:** preserve the user's wording and observation time with
   `-EvidenceSource human` (the default). Reconcile the report with state and
   visible UI evidence before deciding its classification. Human feedback can
   expose missing workflows even when state totals agree.

The synchronized comparison emits `<capture>.qa.json` with independent state,
UI, and human results. Observation-only results are `unobserved` or `recorded`.
A verified WPF replay bundle gives the UI layer a scoped `passed`/`failed`
result. Older observation records count as human evidence. Empty files do not
establish coverage. The default exit code gates state comparisons only;
`-RequireUiEvidence` additionally requires a passing UI replay bundle. Neither
mode infers human acceptance.

Run offline gates before a live window:

```powershell
dotnet test .\kparser2\kparser2.sln --filter "Category!=Integration"
powershell -NoProfile -ExecutionPolicy Bypass -File .\kparser2\scripts\test-parity-comparator.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\kparser2\scripts\test-report-comparator.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\kparser2\scripts\test-parity-evidence.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\kparser2\scripts\test-ui-evidence.ps1
```

Then validate reset acknowledgements, collect paired evidence, run the smoke
matrix below, and reconcile observations. Track unresolved work on the owning
child's GitHub issue, with links to local evidence where appropriate; do not
promote a capture merely because the offline gates pass.

An exact cursor proves a packet cutoff, not lossless paired capture: the
current helper starts recorders after resetting the UIs, and the legacy RAM
reader has no shared packet cursor. Events during startup or different stop
times can still cause differences. Validate capture coverage before using a
mismatch to change a decoder. A future coordinated readiness/start/stop
handshake needs live verification before claiming a common full window.

## Repeatable UI replay

Run from the parent `kdev` directory on Windows with a **closed, frozen** capture:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\kparser2\scripts\test-ui-replay.ps1 `
  -CapturePath .\kparser2\fixtures\sessions\bcmn30_petrifying_pair.ndjson `
  -OutputDir .\kparser2\artifacts\ui-qa\camp-review
```

Choose a new output directory for every run, or omit it to get a unique one.
The wrapper builds into `artifacts/ui-qa-build`, launches a hidden console with
an offscreen WPF host, and returns nonzero on a failed check or timeout. It does
not use the live UI control descriptors or reset a live session. Settings go to
the run directory through `KPARSER2_VIEW_SETTINGS_PATH`; normal app settings
and live executables are not used for the test run. `-NoBuild` reuses the QA
build only, after an explicit build of the current sources.

The runner exercises the actual text-report controls from `AnalyticsViewCatalog`:

- Default reports and every offered report mode.
- Player/enemy filters, saved filter restoration, and reset.
- Applicable detail, crystal, zero-XP, and base-attack controls.
- Modal fight selection: no fights, one fight, then reset.
- Empty-state refresh and repeated snapshot updates (no duplicated text).
- Chat mode and speaker selection.

Each case saves a viewport PNG, full actual/expected document text, filters,
and a verdict. `ui-run.json` records the capture SHA-256, case identities,
failures, skipped surfaces, and limitations; `state.json` and `capture.ndjson`
retain the state and exact input. The text oracle is `AnalyticsReportService`,
so these checks establish binding/control agreement with state, not independent
correctness of report calculations. Screenshot review checks appearance; a
text match alone cannot establish legibility or absence of clipping.

Chart/packet diagnostic surfaces and live connection health remain explicitly
outside this text-report runner. Use the manual smoke matrix for those. A
report's empty-data check does not establish event coverage for that feature.

Supply the bundle when comparing the **same bytes**:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\kparser2\scripts\compare-synchronized.ps1 `
  -KparserChatLines .\ffxi-captures\ndjson\window.kparser.chatlines.txt `
  -Kparser2Capture .\ffxi-captures\ndjson\window.kparser2.complete.ndjson `
  -UiRun .\kparser2\artifacts\ui-qa\camp-review\ui-run.json `
  -RequireUiEvidence
```

Run UI QA on the reconciled capture when that is the comparison input. A hash
mismatch, missing artifact, inconsistent verdict, empty case list, or changed
text in a passing case rejects the bundle before the comparison runs.

To attach a person's report to a saved screenshot case, add these options to
`record-ui-observation.ps1` (with the same capture and surface):

```powershell
  -EvidenceSource human `
  -UiRun .\kparser2\artifacts\ui-qa\camp-review\ui-run.json `
  -UiCase offense/default
```

This preserves the person's summary/time and records the manifest hash and
screenshot reference. The comparator rejects links to a changed bundle or a
different capture/surface. Agent screenshot inspections use `-EvidenceSource
ui`; do not record automated output as human feedback.

## Preflight

Keep the following running for the same play window:

1. `kparser.cli capture` for raw ChatLines.
2. `kparser2.cli record` for packet NDJSON.
3. The kparser WinForms UI, started as Administrator with its parity startup
   mode:

   ```powershell
   .\kparser\FFXILogParser\bin\x86\Debug\KParser.exe --parity-ui
   ```

   `--parity-ui` selects RAM parsing at offset `0062D8F0`, starts parsing, and
   opens all non-debug report tabs. Normal kparser startup remains unchanged.
4. The kparser2 WPF UI.
   - Select `Live`.
   - Confirm the status bar reports a connected live feed.

The synchronized capture script intentionally does not launch or terminate the
desktop applications. UAC, D3D focus, and the game desktop should remain under
the user's control. Launch the kparser parity mode only after the game is
running and approve its elevation prompt.

Both UIs publish a token-protected, loopback-only control descriptor:

```text
ffxi-captures\ndjson\ui-control-kparser.json
ffxi-captures\ndjson\ui-control-kparser2.json
```

The descriptors contain ephemeral ports and tokens and are local runtime
artifacts. They must not be committed or shared.

## In-place reset

Reset both visible sessions without closing the game or either UI:

```powershell
dotnet run --project .\kparser2\kparser2.Cli\kparser2.Cli.fsproj --no-build -- ui status
dotnet run --project .\kparser2\kparser2.Cli\kparser2.Cli.fsproj --no-build -- ui reset
```

`ui reset` always resets both parser applications in place and preserves their
windows and report tabs. With a cursor-capable kpacket it sends an `exact`
boundary and kparser2 accepts only matching-session events whose `message_id`
is strictly greater than `after_message_id`. With an older stable kpacket it
uses an explicit `degraded` boundary, resets the parsers without claiming a
strict packet partition, and records the reason in the status/acknowledgement.
Neither mode restarts FFXI.

Use `--require-exact` when the reset is for synchronized parity evidence:

```powershell
dotnet run --project .\kparser2\kparser2.Cli\kparser2.Cli.fsproj --no-build -- `
  ui reset --require-exact
```

This fails before resetting either parser if the loaded plugin cannot provide
the exact cursor. A normal parser reset can still proceed without that
capability.

The reset command requires a kpacket2 build that advertises the
`message_id_cursor` capability only when `--require-exact` is used. It never
falls back to a fabricated zero cursor.

For a complete one-window loop, use the helper below. It reads the relay
boundary, resets both visible sessions, records both streams, reconciles a
possibly truncated tail, and runs the synchronized comparison:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File `
  .\kparser2\scripts\reset-parity-window.ps1 `
  -DurationMs 120000
```

The helper writes `oracle_<timestamp>.reset.json`; kparser2 NDJSON session
headers also contain `boundary_session_uuid` and `boundary_message_id`.

## User observations

When a visible inconsistency is found, record one JSONL observation. The
observation should include the wall-clock time, application, UI surface, what
was displayed, and an initial classification.

Use `unclassified` when the cause is not yet known:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File `
  .\kparser2\scripts\record-ui-observation.ps1 `
  -CapturePath .\ffxi-captures\ndjson\oracle_sync_YYYYMMDD_HHMM.kparser2.ndjson `
  -Application kparser2 `
  -Surface offense `
  -Classification unclassified `
  -Summary "Offense tab omits Poroburu's 50 damage hit" `
  -ResetId <reset-id> `
  -ResetBoundaryUtc <reset-boundary-utc> `
  -SessionUuid <session-uuid> `
  -BoundaryMessageId <boundary-message-id> `
  -BoundaryMode exact `
  -BoundaryQuality exact
```

The default output is adjacent to the capture:

```text
<capture>.ui-observations.jsonl
```

Optional fields identify a displayed event or total:

```powershell
  -ActorName Poroburu `
  -TargetName "Desert Beetle" `
  -Category Melee `
  -Amount 50 `
  -Success hit `
  -Notes "Visible after selecting Offense; kparser still shows the row"
```

Allowed classifications:

- `kparser-only`: only the legacy parser/UI has the event.
- `kparser2-missing`: packet evidence or established semantics are absent from
  kparser2.
- `kparser2-extra`: kparser2 has a packet event that kparser never represented.
- `rendering-only`: state agrees, but the displayed view is stale, filtered,
  formatted, or otherwise different.
- `deferred`: real but outside this scan's scope.
- `unclassified`: awaiting reconciliation.

The same record can be created from a user's natural-language report. The
agent should preserve the stated time and wording when converting it into the
JSONL artifact.

## Post-capture comparison

First reconcile a capture that may still have a partial final line:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File `
  .\kparser2\scripts\reconcile-capture.ps1 `
  -Path .\ffxi-captures\ndjson\oracle_sync_YYYYMMDD_HHMM.kparser2.ndjson `
  -Output .\ffxi-captures\ndjson\oracle_sync_YYYYMMDD_HHMM.kparser2.complete.ndjson
```

Then include UI observations in the normal synchronized comparison:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File `
  .\kparser2\scripts\compare-synchronized.ps1 `
  -KparserChatLines .\ffxi-captures\ndjson\oracle_sync_YYYYMMDD_HHMM.kparser.chatlines.txt `
  -Kparser2Capture .\ffxi-captures\ndjson\oracle_sync_YYYYMMDD_HHMM.kparser2.complete.ndjson `
  -UiObservations .\ffxi-captures\ndjson\oracle_sync_YYYYMMDD_HHMM.kparser2.ndjson.ui-observations.jsonl
```

This produces the existing snapshots and:

```text
<capture>.ui-compare.json
```

The UI comparison separates `rendering-only` observations from semantic
observations, deferred observations, and unresolved observations. It does not
turn a human observation into an automatic decoder fix or change the existing
parity/report exit criteria.

Unknown classifications, applications, evidence sources, or invalid timestamps
are rejected. When reset/session metadata is supplied, conflicting observation
metadata is rejected. Observations must name the compared capture (the
reconciled `.complete.ndjson` filename is accepted for its original `.ndjson`).
Exact comparisons also require matching exact observation
boundaries, including cursor zero. `-RequireExactBoundary` requires explicit
`-SessionUuid`, `-BoundaryMessageId`, `-BoundaryMode exact`, and
`-BoundaryQuality exact` matching the capture header; missing metadata is not a
zero cursor. Supply `-ResetId` to check the reset identity as well.

## Manual smoke matrix

For each live window, inspect these kparser2 surfaces when their corresponding
state appears in the capture:

| Surface | Verify |
|---------|--------|
| Connection/health | Live status, packet activity, no reconnect loop |
| Chat | Speaker, mode, body, direction, and refresh |
| Interactions | Actor, target, category, amount, and success |
| Fights | Fight boundaries, enemy, kill state, and ordering |
| Offense | Category totals, attempts, hits, misses, and filters |
| Experience | Points, chain count, and attribution |
| Loot | Item name, quantity, actor, and ordering |
| Filters | Selected player/category changes only the intended rows |
| Refresh/ordering | New events appear once, in stable chronological order |

For every suspected mismatch, compare the visible row with the nearest
normalized CLI projection and the raw capture evidence. A UI-only mismatch is
reported as `rendering-only`; a state mismatch follows
`docs/parity-inequalities.md` before any code change.
