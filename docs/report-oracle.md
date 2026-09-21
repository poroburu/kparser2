# Report-oracle loop

Canonical runbook for a local parity scan that uses **two oracles** from one play session. This is not an RC gate and not a cast checklist. Iterate this file when the loop changes; append a dated bullet under [Current pass](#current-pass) when a scan starts or lands.

Parent agent contract (kdev umbrella): `.cursor/rules/parity-oracle.mdc`. Bucket meanings: [parity-inequalities.md](parity-inequalities.md). RC checklist stays [RELEASING.md](RELEASING.md).

## Two questions

**Wire:** did this packet mean what we classified?  
Horizon NDJSON (`kparser2.cli record`), XiPackets, VieweD, `msg.lua`. `--assert-settled` lives here. Never edit kparser ParseCodes so a dump matches.

**Product:** should this tab show the same analytics a kparser user has used for years?  
kparser tabs (Offense, Extra Attacks, Recovery, …) = [kparser2#4](https://github.com/poroburu/kparser2/issues/4). If both sides saw the same melee/kill/XP and the numbers disagree, kparser tab meaning wins. Implement in kparser2. Packets still win when kparser invented an event the wire never carried.

Live WPF and `kparser2.cli report` both call `AnalyticsReportService.formatRequest` → `DetailedReports.format`. Score that path, not unused `AnalyticsReports` skeletons.

## Where it runs

Local Cursor **Agent** chat (not Plan) in the `kdev` workspace on the game PC. kpacket publishes on `localhost:5555`. Not GitHub Actions, not Cloud Agent, not Cursor Automation, not WPF-driven.

Long-lived processes:

1. Last-green **`develop`** `kparser2.Cli.exe record` (not session WIP).
2. **`kparser.cli capture`** (or `KParser.exe --parity-ui` plus a ChatLine file).

Attach `notify_on_output` to `record checkpoint:` / `recording stopped:` and to the kparser capture checkpoint/stop lines. Do **not** arm `Start-Sleep` + `AGENT_LOOP_WAKE`, and do not use Cursor `/loop` as a sleeper. Auto-review treats that as a second unattended job.

## Dual capture is a gate

The report-oracle scan does **not** mark kparser v1 “unobserved” and continue.

| State | Action |
|-------|--------|
| kpacket `:5555`/`:5556` down | Fix `/load kpacket` / `probe`. Do not invent a sleeper. |
| `kparser.cli capture` not attached | Build x86 net3.5 CLI if missing. `capture` must run as Administrator (`Start-Process -Verb RunAs`); an unelevated process exits 2 and writes no ChatLines. A sandbox exception does not elevate. **Block** product compare and report PRs until the elevated process is writing lines or a proven idle attach. |
| Capture attached, town idle, no new ChatLines | Allowed. Wait for play. |
| NDJSON grew combat/chat and chatlines did not | Capture is broken. Fix attach. Do not classify those rows as kparser-only. |

`kparser.cli snapshot` of a **file** is not attach. `capture` subscribes to the live RAM ChatLine stream at `0x0062D8F0`. It does not parse or write a `.sdf`.

RC / `--assert-settled` still does not require v1. That distinction is deliberate: shipping classifiers vs cloning tab meaning.

## Start order

Horizon loaded, `/load kpacket`.

1. `kparser2.cli probe`. Fail → live untested for **wire** until the plugin is up; still do not skip kparser attach once the game is up.
2. Record last-green develop exe to `ffxi-captures/ndjson/<stamp>.ndjson` with `--checkpoint-ms 120000 --idle-ms 180000`. Attach if a recorder is already writing. Exactly one kparser2 recorder.
3. Start `kparser.cli capture` elevated to a sibling `ffxi-captures/<stamp>.chatlines.txt` with `--checkpoint-ms 120000`. It exits 2 unless it is Administrator. Prove attach (elevated process, file exists) before the first heat classify.
4. I play. No in-game script. `kparser2.cli echo` only if one sample is blocking a family already in progress.

## Checkpoint

On `record checkpoint:` or `recording stopped:`:

1. First checkpoint and recording stop/completion **always** reconcile and compare, regardless of heat. At other checkpoints, run `scripts/opcode-heat.ps1` on the live NDJSON (FileShare read); exit 0 (`HEAT unchanged`) skips reconcile/snapshot. Shape, not volume. Heat sees `0x28` command numbers, not finish-message IDs: a new ID on an existing command can wait until the final comparison. Keep this policy; a cool checkpoint is not a parity verdict.
2. Heat changed, first checkpoint, or stop → `scripts/reconcile-capture.ps1` (drop truncated last lines), then freeze **both** inputs under a new private checkpoint directory. Normalize, align, and snapshot as described below. Never overwrite a snapshot referenced by a comparison. Do not add scripts or a `PARITY.md` tracker.
3. Classify **before coding** (see [parity-inequalities.md](parity-inequalities.md)):

| Bucket | Meaning | Action |
|--------|---------|--------|
| kparser2-missing **ingest** | Packet exists, no interaction | Implement decoder/ingest in kparser2 |
| kparser2-missing **report** | Interactions exist, tab math/layout ≠ kparser | Implement `DetailedReports` family |
| kparser-only | Chatline artifact, no packet | Do not clone |
| kparser2-extra | Packet event kparser never showed | Keep; not a miss |
| intentional fork | e.g. fight close 30s vs 10 min | Write it down; default match kparser report meaning |

4. One family per commit on `cursor/session-yyyymmdd-hhmm` from `develop`. Prove with an existing fixture plus `--assert-settled-code` when it is an ingest id. `dotnet test --filter Category!=Integration` after freeze, not every packet. Draft PR into **`develop`**, never `main`. No kdev pin bump from the scan agent. Search open PRs for the same `code` first.

Zone change (`0x000B` LogoutState `2`) keeps **both** files. Real logout / DC / plugin gone ends that pair: `wait-kpacket-session.ps1`, then a **new** NDJSON and a **new** chatlines capture together. Stop if the tester says stop.

### Product evidence preflight

Use the existing [saved-session workflow](saved-session-parity.md). Keep raw inputs,
normalization provenance, declared UTC interval/offset, input/output hashes, and
the tested commit and executable hashes together in private evidence. Preserve
earlier failed attempts. A path to a mutable snapshot is not reproducible evidence.

Normalize only a derived ChatLine file with `oracle-dataset.cjs normalize-chat`.
The adapter preserves headers, body content, order, and duplicates; its sidecar
records the observed checker continuation rows whose timestamp comes from the
immediately preceding wrapped message. Unknown untimestamped forms still block
alignment. Do not remove rows to make the comparison pass or edit legacy kparser.

Align a declared shared half-open interval; disclose initialization packets and
excluded tails. Before interpreting report differences, check legacy parse counts,
source tracing, and distinct beginning/middle/end anchors. RAM attach alone does
not prove usable parsing. Correlation is approximate (-1500 through +10000 ms),
not an exact packet boundary. Joined legacy messages can span interleaved actors;
each matching source fragment is consumed once without consuming intervening rows.

Use a full `analytics snapshot --json` for report comparison; `--parity` alone is
an interaction projection. A settled pass only says ranked classification checks
passed. Verify the meaning of values in reports (for example, status IDs must not
be summed as damage). Keep unexplained differences visible and classify only
those supported by source evidence. Keep state, automated UI, inspected screenshots,
and human acceptance separate; a UI execution failure must retain completed state
results. A passing rerun does not resolve an intermittent failure.

## GitHub

- [#4](https://github.com/poroburu/kparser2/issues/4) — report inventory. Each row is one state:

  - **Verified** — paired Horizon window or committed live slice; figures checked against packet bytes and against kparser where both saw the event; WPF filters for that report exercised; honest empty shown when the event is absent.
  - **Shipped with a named limit** — the report renders, and the unchecked mode is named.
  - **Blocked on bytes** — the report stays empty or partial until a capture contains the event.
  - **Deferred** — written reason; no implementation until that reason changes.

  Comment when a family is classified or proven. Close only when every row is Verified or Deferred. A child issue closing, a CLI `report` with rows, `--assert-settled`, a cool heat fingerprint, or a UI text match against `AnalyticsReportService` does not move a row to Verified. `scripts/test-ui-replay.ps1` checks that WPF bound the same text as state.
- [#9](https://github.com/poroburu/kparser2/issues/9) — harness freeze lifted **only to run** the existing compare path for #4. Do not close. Do not grow oracle scripts until a missing compare blocks a family.
- [#6](https://github.com/poroburu/kparser2/issues/6) — do not touch.
- [#10](https://github.com/poroburu/kparser2/issues/10)–[#13](https://github.com/poroburu/kparser2/issues/13) — stay closed.
- Do not edit kparser ParseCodes. Do not open kparser issues for dump-matching.

## Paste this (solo, same box)

Horizon loaded, `/load kpacket`. New **Agent** chat (not Plan):

```
Report-oracle parity loop while I play. Follow kparser2/docs/report-oracle.md. Local Agent on this game PC in the kdev workspace — not Cloud, not Automation, not WPF.

Two oracles from the same play session. Both must be recording before you classify or code.
- Wire: Horizon NDJSON via last-green develop kparser2.Cli.exe record. Packets / XiPackets / VieweD / msg.lua win on classification. Never edit kparser ParseCodes. --assert-settled is this layer.
- Product: kparser tabs = kparser2#4. If both sides saw the same melee/kill/XP and numbers disagree, kparser tab meaning wins. Implement in kparser2.

kparser RAM capture is required. Do not mark v1 “unobserved” and proceed.

Start: probe; record last-green develop exe to ffxi-captures/ndjson with --checkpoint-ms 120000 --idle-ms 180000 (attach if already writing); kparser.cli capture elevated (`Start-Process -Verb RunAs`) to a sibling chatlines file. It exits 2 if it is not Administrator. Notify only on record checkpoint: / recording stopped: and the capture’s checkpoint/stop. No Start-Sleep, AGENT_LOOP_WAKE, or Cursor /loop timer. Prove the elevated attach before the first heat classify. Town idle with a live attach is OK. NDJSON combat/chat with a dead chatlines file is capture broken.

First checkpoint and stop: always freeze, normalize, align, and compare both sides. Other checkpoints: opcode-heat.ps1 (exit 0 → skip and say so); heat change → the same paired preflight. Prove usable parsing and source anchors before classifying ingest | report | kparser-only | extra | fork. Search open PRs. Draft PR into develop, never main. Do not close #4/#6/#9. Do not reopen #10–#13. Do not touch #6. Do not ask me to cast. echo only if one sample is blocking a family already in progress. Zone change keep both files; real logout/DC ends the pair. Stop if I say stop.
```

## Current pass

Append a dated bullet when a scan starts or a family lands. Do not turn this into a cast list.

- **2026-09-20** — Offline review of the September 19 pair at `2a6fa6d`, with frozen inputs and repaired timestamp/source tracing. The earlier timestamp blocker is recoverable through existing tools. Stat-absorb values 136–139 are status IDs, not damage: `0e9d809` corrects the classification and damage aggregators, superseding the Harm/Spell interpretation below. Private reproducible evidence: `artifacts/parity-review-20260920/` (final manifest, failed attempts, anchors, hashes, and replays). Product mismatches remain; follow-up ownership is [#4](https://github.com/poroburu/kparser2/issues/4), [#16](https://github.com/poroburu/kparser2/issues/16), and [#17](https://github.com/poroburu/kparser2/issues/17). No new live run or human acceptance.

- **2026-09-19** — Live dual-capture (`ffxi-captures/ndjson/20260919_105549.ndjson` + sibling chatlines; last-green `v0.1.0-rc.3` / `16f7651`; RAM attach proven). Heat stayed cool until **recording stopped**, which flushed cmd-4 leftover **329–332** (`msg.lua` MAGIC_ABSORB_STR/DEX/VIT/AGI). [PR #15](https://github.com/poroburu/kparser2/pull/15) now classifies the contiguous **329–335** region as Harm/Spell (no Recovery dual-emit). Live prove: session CLI `--assert-settled-code unclassified_message` then `--assert-settled` actionable 0 on the complete NDJSON. Cmd-5 tuna sushi ItemUses already in this PR. Freeze after this leftover: recast/range/`/check` stay extra; product Offense still blocked on kparser CLI timestamped `ChatText`. [#4](https://github.com/poroburu/kparser2/issues/4) / [#9](https://github.com/poroburu/kparser2/issues/9) unchanged.
