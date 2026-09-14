# Development oracle datasets

QA parity includes the read-only kparser v1 oracle, not just kparser2 settled
checks. Capture packets and raw RAM ChatLines together. Request native shell
permissions and Windows Administrator elevation when attachment requires it.
Verify both files grow before announcing readiness. Keep raw evidence private.

## Validated timestamp adapter

`node scripts/oracle-dataset.cjs normalize-chat RAW NEW_OUTPUT` handles the
observed Ashita timestamp/color prefix after the 21-field RAM header. It keeps
the timestamp, header, body, duplicates and line endings. It neither changes
kparser nor removes arbitrary color codes or addon text. It refuses overwrite,
rejects unsupported headers, and writes input/output hashes and a change count.
Copy an active ChatLine file before snapshotting it: v1 opens snapshots with
sharing flags incompatible with an active writer.

Validation: `node --test scripts/test-oracle-dataset.cjs`. On the 2026-09-14
BST checkpoint, v1 parsed 4/35 messages before adaptation and 32/35 after it.
The remaining three were checker/command chat, not timestamp-prefixed combat.
On the larger frozen window it parsed 228/239 messages. These counts establish
adapter usefulness, not v1/v2 semantic equality.

## Dataset layers

1. Private immutable source: packet NDJSON, raw ChatLines, recorder logs,
   plugin/session identity, exact source/build commits, start/end times and
   SHA-256 hashes. Never put runtime control tokens in a distributable bundle.
2. Private derived replay: reconciled NDJSON and adapted ChatLines with hashes,
   adapter version, parent hashes and boundary classification. Preserve entity,
   zone, party and pet initialization before the scored window.
3. Sanitized interaction projections: `node scripts/oracle-dataset.cjs
   export-pair V1_JSON V2_PARITY_JSON NEW_DIR bst-pet-leveling`. Both files share
   dataset-local aliases; only reviewed categories and numeric amounts survive.
   No raw payload, chat body, timestamp, source ID or name map is exported.
   This supports comparator regressions, **not packet-decoder replay**. Empty
   chat arrays mean excluded coverage, not chat parity. The manifest starts
   unreviewed, with unverified boundaries and unobserved UI/human evidence.
4. Approved regression fixtures: promote small cases only after reconciling
   expected outcomes against raw bytes, v1, packet layouts and observations.
   Keep disagreements as explicit known failures; never bless v2's own output
   as independent truth. Separate development examples from held-out sessions.

Run the existing `scripts/compare-parity.ps1` on exported files with `-SkipChat`.
Keep comparison output local: its `sources` fields contain absolute paths.
Run `scripts/compare-synchronized.ps1` on private derived captures for report
and state evidence; use `docs/ui-parity-qa.md` for separate UI/human evidence.

## Unequal start and end boundaries

Preserve setup packets but score only the shared window. Wall-clock bounds
alone are approximate: RAM buffering, second-resolution displayed timestamps,
timezone/day rollover and duplicate actions can misalign them. Corroborate
timestamps with multiple distinctive actor/target/action/amount sequences.
Do not silently choose whichever alignment gives the smallest diff. Exclude
partial fights from whole-fight totals and label unmatched tails separately.
If exact cursor/reset evidence is missing, do not claim exact reconciliation.
The September 14 full capture has a late v1 attachment. Use reviewed complete
fight windows rather than comparing its whole-file totals.

`node scripts/align-oracle-window.cjs PACKETS NORMALIZED_CHAT NEW_DIR START_UTC
END_UTC LOCAL_OFFSET_MINUTES` produces a private candidate with a half-open
scored window, earlier entity/party/pet setup packets, and source hashes. UTC
arguments must end in `Z`; the local UTC offset is explicit. Split windows at
local midnight. The tool does not discover anchors or certify complete fights.
Tests: `node --test scripts/test-align-oracle-window.cjs`.

### September 14 aligned validation

The Antican window 14:50:20Z–14:52:35Z has matching 405/285 weapon-skill damage,
236/186 pet-move damage, and the 14:52:29Z kill/180 XP as anchors. A second
beetle window 14:52:40Z–14:54:12Z ends after its 14:54:09Z kill/131 XP and before
the next fight's opening hit at about 14:54:14Z. Offset: local time = UTC−4.
The earlier 14:54:15Z candidate included that partial next fight; it is not the
accepted whole-fight window. These are recorded boundary choices, not automatic
best-fit alignment. Retained setup packets remain outside scored combat.

After fixing wire 67 critical damage, wire 185 WS/pet damage categories, and
the pet heuristic's overwrite of observed NPC names, both windows give equal
fight/offense/XP reports. The comparator resolves killer IDs through combatants,
normalizes NPC underscores, folds critical hits into legacy aggregate categories,
and excludes preparation rows only when corroborated by native v1 flags.
Real damage changes, unresolved killer IDs and uncorroborated zero-value rows
remain mismatches in regression tests. This is report parity for these windows;
it does not establish all-interaction, chat, UI or human parity.

## Additional sessions to collect

- BST pet lifecycle: charm success/failure, release/death/replacement, pet moves,
  player/pet damage, kill/XP/loot attribution. Current capture is a candidate.
- Melee/ranged: hit/miss/critical, multi-hit weapon skills, counters, parries,
  shadows, skillchains. Collect varied outcomes, not longer identical fights.
- Magic/support: cast start/finish/interruption, resist/no-effect, cure, buffs,
  debuffs, drain, dispel and status removal; distinguish attempts from results.
- Party/alliance: multiple players/pets, ownership changes, joins/leaves and
  filters. This targets known party and nonlocal pet attribution limitations.
- Loot/XP: pool creation, lot/pass, award/loss, repeated items, chains, level-up,
  death/recovery and capped progression. Preserve the whole lifecycle.
- Session lifecycle: login, mid-zone attachment, zoning, disconnect/reconnect,
  capture recovery and repeated replay. Test session reset and duplication.
- Chat/encoding: controlled synthetic messages, auto-translate and system text.
  Real conversations stay private; projection exports deliberately omit chat.

Tag each case by server family/build, source session, scenario, opcode/message
shape, observed outcomes, setup dependencies, known gaps and evidence status.
Use small fast fixtures per change, broader saved-session replay nightly, and
fresh live sessions only for missing behavior or ingest/wire changes.

## Research and next implementation boundary

[XiPackets layouts](https://github.com/atom0s/XiPackets) are field references;
live server bytes remain authoritative. Names occur in binary payloads, e.g.
entity updates and trophy awards. A public wire dataset needs a reviewed
opcode/field allowlist, consistent entity-ID remapping (including bit-packed
actions), name rewriting, relative time/session IDs, unknown-opcode rejection,
and before/after semantic invariance tests. Base64 text replacement cannot
provide this. That wire sanitizer is not implemented by the projection tool.

[Wireshark editcap](https://www.wireshark.org/docs/man-pages/editcap.html)
provides capture editing; it does not supply our FFXI NDJSON semantic sanitizer.
[DVC](https://dvc.org/) provides data versioning and reproducible pipelines.
Start with Git for small sanitized fixtures/manifests and private storage for
raw sessions; evaluate DVC if the retained corpus makes manual hash manifests
cumbersome. Do not add storage infrastructure before useful cases exist.
