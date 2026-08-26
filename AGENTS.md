# Agent guide — kparser2

This document helps Cursor agents iterate on kparser2 **without Ashita running**.

## Quick commands

```powershell
# Build everything
dotnet build C:\Users\porob\git\kdev\kparser2\kparser2.sln

# Run all unit tests (sync ingest path; ~2 min)
dotnet test C:\Users\porob\git\kdev\kparser2\kparser2.sln

# CI / agent fast path — skip retail NDJSON slices (~3100 lines each)
dotnet test C:\Users\porob\git\kdev\kparser2\kparser2.sln --filter "Category!=Integration"

# Replay golden fixture (preferred verification)
dotnet run --project C:\Users\porob\git\kdev\kparser2\kparser2.Cli\kparser2.Cli.fsproj -- replay C:\Users\porob\git\kdev\kparser2\fixtures\sessions\sample.ndjson

# Structured decoder output (no WPF required)
dotnet run --project C:\Users\porob\git\kdev\kparser2\kparser2.Cli\kparser2.Cli.fsproj -- decode C:\Users\porob\git\kdev\kparser2\fixtures\sessions\sample.ndjson

# Filter by opcode
dotnet run --project C:\Users\porob\git\kdev\kparser2\kparser2.Cli\kparser2.Cli.fsproj -- decode C:\Users\porob\git\kdev\kparser2\fixtures\sessions\sample.ndjson --filter 0x17 --json

# Regenerate item name lookup from LandSandBoat SQL
dotnet run --project C:\Users\porob\git\kdev\kparser2\kparser2.Cli\kparser2.Cli.fsproj -- export-items

# Regenerate ability name lookup from LandSandBoat SQL
dotnet run --project C:\Users\porob\git\kdev\kparser2\kparser2.Cli\kparser2.Cli.fsproj -- export-actions

# Regenerate zone name lookup from LandSandBoat SQL
dotnet run --project C:\Users\porob\git\kdev\kparser2\kparser2.Cli\kparser2.Cli.fsproj -- export-zones

# Import PacketViewer .log → NDJSON (fixed s2c/c2s topics)
dotnet run --project C:\Users\porob\git\kdev\kparser2\kparser2.Cli\kparser2.Cli.fsproj -- import packetviewer --full C:\path\to\full.log -o capture.ndjson

# Validate imported capture (entity/opcode/analytics summary)
dotnet run --project C:\Users\porob\git\kdev\kparser2\kparser2.Cli\kparser2.Cli.fsproj -- import packetviewer --validate capture.ndjson

# Analytics snapshot (waits for replay completion; no 500 ms race)
dotnet run --project C:\Users\porob\git\kdev\kparser2\kparser2.Cli\kparser2.Cli.fsproj -- analytics snapshot capture.ndjson
dotnet run --project C:\Users\porob\git\kdev\kparser2\kparser2.Cli\kparser2.Cli.fsproj -- analytics snapshot capture.ndjson --parity-chat -o chat.json --assert-chat
dotnet run --project C:\Users\porob\git\kdev\kparser2\kparser2.Cli\kparser2.Cli.fsproj -- analytics snapshot capture.ndjson --assert-settled
dotnet run --project C:\Users\porob\git\kdev\kparser2\kparser2.Cli\kparser2.Cli.fsproj -- analytics snapshot capture.ndjson --assert-settled-code unclassified_message-236

# Checkpoint cool-off — exit 0 (HEAT unchanged) means skip snapshot
powershell -File C:\Users\porob\git\kdev\kparser2\scripts\opcode-heat.ps1 -Path capture.ndjson

# Regenerate synthetic fixtures with valid packet bytes
powershell -File C:\Users\porob\git\kdev\kparser2\scripts\generate-fixtures.ps1

# Live plugin health (requires game + kpacket2 loaded)
dotnet run --project C:\Users\porob\git\kdev\kparser2\kparser2.Cli\kparser2.Cli.fsproj -- probe

# Long BST camp session (20 min record + post-session oracles)
dotnet run --project C:\Users\porob\git\kdev\kparser2\kparser2.Cli\kparser2.Cli.fsproj -- record C:\Users\porob\git\kdev\ffxi-captures\ndjson\bst_leveling.ndjson --duration-ms 1200000 --idle-ms 180000 --checkpoint-ms 120000
dotnet run --project C:\Users\porob\git\kdev\kparser2\kparser2.Cli\kparser2.Cli.fsproj -- watch --analytics --duration-ms 300000 --interval-ms 5000
dotnet run --project C:\Users\porob\git\kdev\kparser2\kparser2.Cli\kparser2.Cli.fsproj -- analytics snapshot capture.ndjson --assert-combat --min-battles 2
dotnet run --project C:\Users\porob\git\kdev\kparser2\kparser2.Cli\kparser2.Cli.fsproj -- report fights capture.ndjson
dotnet run --project C:\Users\porob\git\kdev\kparser2\kparser2.Cli\kparser2.Cli.fsproj -- report offense capture.ndjson
```

## Decoder oracle workflow

1. **Unit tests** — hex fixtures in `kparser2.Decoders.Tests` (XiPackets / LandSandBoat layouts).
2. **CLI decode** — replay NDJSON and print structured events for human/agent verification.
3. **kpacket2 packet_monitor.exe** — live byte oracle on `:5555`.
4. **VieweD** — open the same NDJSON capture for independent field-level dumps when decoder output is ambiguous.

## PacketViewer import oracle chain

1. **Convert** — `scripts/convert-packetviewer-to-ndjson.ps1` or `kparser2.cli import packetviewer` (topics must be `kpacket.v1.world.s2c` / `c2s`, not `i2c` / `o2s`).
2. **Validate** — `import packetviewer --validate capture.ndjson` (opcode histogram, entities, local player, zone, battles).
3. **Analytics** — `analytics snapshot capture.ndjson` (uses `WaitForReplayComplete`; no fixed delay).
4. **VieweD** — cross-check ambiguous fields (0x00E name@0x34, 0x00DF vitals) against `VieweD/data/ffxi/rules/ffxi.xml`.

Mid-capture PV logs often lack **0x00A** (login); local player falls back to **0x00DF UniqueNo@4**. Zone id from **0x00A@0x30** or **0x00DF ZoneNo@26** may be zero during instanced battles — zone name can stay empty until a packet carries a non-zero zone id.

Packet payloads include the **4-byte world header**; decoder field offsets start at **byte 4**.

## Fixture paths

| File | Contents |
|------|----------|
| `fixtures/sessions/sample.ndjson` | chat (0x17), trophy list (0xD2), action |
| `fixtures/sessions/login.ndjson` | enter zone + system chat |
| `fixtures/sessions/item_drop.ndjson` | trophy list + solution |
| `fixtures/sessions/combat_basic.ndjson` | battle message (0x29) |
| `fixtures/sessions/combat_action.ndjson` | combat action (0x28) melee + spell |
| `fixtures/sessions/combat_death.ndjson` | MsgBasic defeat + falls to ground |
| `fixtures/sessions/combat_recovery.ndjson` | cure via 0x29 + 0x28 |
| `fixtures/sessions/chat_xp.ndjson` | MsgBasic XP + EXP chain + system chat |
| `fixtures/sessions/combat_kill_xp.ndjson` | kill + XP + chain attribution |
| `fixtures/sessions/chat_self_say.ndjson` | outgoing say + tell bootstrap |
| `fixtures/sessions/chat_yell.ndjson` | yell (0x17 kind 0x1A) |
| `fixtures/sessions/chat_yell_live.ndjson` | live HorizonXI yells (zone in Data, 0xFD auto-translate) |
| `fixtures/sessions/combat_melee_hits.ndjson` | 0x28 melee hits (0x14/0x19/0x1C) |
| `fixtures/sessions/combat_misses.ndjson` | melee misses (0x15/0x1D) |
| `fixtures/sessions/combat_ranged.ndjson` | ranged hit + miss |
| `fixtures/sessions/combat_defense.ndjson` | parry + shadow absorb |
| `fixtures/sessions/combat_failures.ndjson` | no-effect buff/debuff (0x44/0x3B) |
| `fixtures/sessions/combat_counters.ndjson` | counter + retaliate |
| `fixtures/sessions/combat_tp_drain.ndjson` | 0xA3 hit + 0xBB TP drain |
| `fixtures/sessions/combat_enfeeble.ndjson` | enfeeble (0x39) |
| `fixtures/sessions/combat_buff.ndjson` | enhance (0x38) |
| `fixtures/sessions/combat_drain.ndjson` | drain (0x16) |
| `fixtures/sessions/combat_ja.ndjson` | job ability (commandNo 13) |
| `fixtures/sessions/combat_prepare.ndjson` | preparing spell (0x32) |
| `fixtures/sessions/combat_cover.ndjson` | cover miss (0x6D) |
| `fixtures/sessions/combat_skillchain.ndjson` | skillchain MsgBasic + follow-up |
| `fixtures/sessions/bcmn30_petrifying_pair.ndjson` | retail BCMN30 slice: mob spawns (0x00E), combat, defeat |

## kparser game-event test parity

Legacy **kparser** (`TestParser.cs`) parses comma-hex chat log lines; **kparser2** maps the same semantics from **0x28/0x29** via `ParseCodesTables.fs` (ported from kparser `ParseCodes.cs`).

| Tier | Project | What it covers |
|------|---------|----------------|
| 1 | `kparser2.Analytics.Tests/ParseCodesParityTests.fs` | Table-driven `(commandNo, messageId, miss, value)` → `BattleMessageCatalog.classifyActionEffect` for every active kparser `TestParser` scenario + empty regions |
| 2 | `kparser2.Analytics.Tests/InteractionParityTests.fs` | Full `0x28` → `InteractionBuilder` pipeline with entity names |
| 3 | `kparser2.Analytics.Tests/FixtureReplayParityTests` | NDJSON replay via `ReplayHelpers.ingestFixture` |
| 4 | `kparser2.Decoders.Tests/DecoderTests.fs` | `TestMobNames01`–`10` via `0x00E` npc updates (16-char packet name limit) |

```powershell
# Fast parity oracle (no PacketSession replay)
dotnet test C:\Users\porob\git\kdev\kparser2\kparser2.Analytics.Tests\kparser2.Analytics.Tests.fsproj --filter "FullyQualifiedName~ParseCodesParity|FullyQualifiedName~InteractionParity|FullyQualifiedName~FixtureReplay"

# Retail integration slices (bcmn30_petrifying_pair, bst_camp_multi, bst_loot_name)
dotnet test C:\Users\porob\git\kdev\kparser2\kparser2.Analytics.Tests\kparser2.Analytics.Tests.fsproj --filter "Category=Integration"

# Regenerate synthetic combat parity fixtures
powershell -File C:\Users\porob\git\kdev\kparser2\scripts\generate-fixtures.ps1
```

**Test reliability:** Analytics tests use synchronous `ReplayHelpers.ingestFixture` (not `PacketSessionFactory.fromReplayDefault`) so replay does not spawn background threads or block on `WaitForReplayComplete`. `PacketSession` replay is validated via `kparser2.cli analytics snapshot` (unit test skipped — xUnit sync-over-async deadlock). `xunit.runner.json` disables parallelization to avoid `EntityRegistry` races.

Parity matrix rows are named after kparser test methods (`TestPlayerHitMob`, `FailSelfBuff`, `region_enfeeble`, etc.). Mark a row done when Tier 1–3 tests pass for that scenario.

### Dual dump (kparser vs kparser2)

kparser can dump the same ChatLine fixtures the unit tests use, without the WinForms host or a `.sdf` file:

```powershell
powershell -File C:\Users\porob\git\kdev\kparser\scripts\snapshot.ps1 snapshot `
  C:\Users\porob\git\kdev\kparser\fixtures\chatlines\chat_yell.txt --parity-chat -o kparser-chat.json
```

Compare `parity.chat` (speaker / mode / body) with kparser2 incoming chat:

```powershell
dotnet run --project C:\Users\porob\git\kdev\kparser2\kparser2.Cli\kparser2.Cli.fsproj -- analytics snapshot `
  C:\Users\porob\git\kdev\kparser2\fixtures\sessions\chat_yell.ndjson --parity-chat -o k2-yell.json

powershell -File C:\Users\porob\git\kdev\kparser2\scripts\compare-chat-parity.ps1 kparser-chat.json k2-yell.json
```

These fixture dumps do not load kpacket. They only prove both CLIs agree on **constructed** bytes/text (`generate-fixtures.ps1`), not that HorizonXI sends that layout. Combat still diffs `parity.interactions` by **name** (not IDs). Schema: [kparser/docs/snapshot-schema.md](../kparser/docs/snapshot-schema.md). kparser `actionType` is Melee/Ranged/Spell; kparser2 `HarmType` uses the same labels. kparser `success` uses `hit` / `miss` / `parry` / `shadow-absorb` / `no-effect`. Chat `message` is body-only; kparser native `chat[]` keeps the full line.

**Oracle policy:** live NDJSON is ground truth. `kparser.cli` is a **read-only** oracle (chatlines / `parity.chat` / `parity.interactions`). It can be wrong. Do **not** change kparser to match kparser2. If they disagree, research XiPackets / VieweD / the capture, then fix kparser2 (or note that kparser omits the event). Dual-dump never authorizes editing kparser.

Long-running parity: observe (`watch` / `record` under `ffxi-captures/ndjson/`). When one pattern fails to reconcile, implement that event in kparser2 only. Prompt in-game with `kparser2.cli echo`, not a Cursor checklist.

**Disconnect:** `record` stops on `:5556` hello failure (3 missed 1s polls), `session_uuid` change, incoming `0x000B` with `LogoutState` LOGOUT/TIMEOUT/GMLOGOUT (`1`/`8`/`9`), or `--idle-ms` stall after packets (default 180s; `0` disables). Incoming `0x000B` **ZONECHANGE** (`2`) is a zone-server handoff (next IP/port in `Iwasaki`), not `/logout` — keep recording the same file. It does not append across real session ends. Copy complete lines with `scripts/reconcile-capture.ps1`. Do not treat a truncated last line or a magic start without finish as a decoder bug. After a real stop, wait with `scripts/wait-kpacket-session.ps1 -PreviousUuid <old>`, then `record` a **new** path. If the user ends the parity run, do not start another recorder.

### Promote a fixture

Oracle is **live bytes**, then VieweD, then a golden slice. Never the reverse (do not invent NDJSON to match a test).

1. Record under `C:\Users\porob\git\kdev\ffxi-captures\ndjson\` (`kparser2.cli record … --duration-ms …`). Keep full captures there; they are not committed.
2. Inspect: `decode --filter 0x17` / `--filter 0x28`, `analytics snapshot --parity-chat`, interaction rows. Open the same file in **VieweD** if opcode fields are ambiguous.
3. If live disagrees with a synthetic fixture, **live wins**. Fix classifiers or replace the synthetic; do not edit the capture to match the generator.
4. Copy a short slice into `fixtures/sessions/` and add a test only after that inspection.
5. Optional second check: kparser RAM chatlines from the same session via `kparser.cli snapshot`. `kparser.cli` cannot attach to the process.

Retail PacketViewer slices (`bcmn30_petrifying_pair`) already follow this gate.

Expected `decode sample.ndjson` output (non-JSON mode):

```
0x0017 GP_SERV_COMMAND_CHAT_STD
  chat [Say] Alice: Hello from fixture
0x00D2 GP_SERV_COMMAND_TROPHY_LIST
  loot Found item=... (704) actor=Entity 12345
Decoded 2 packets with structured events from ...
```

## Project map

```
kparser2.Abstractions/   C#  DTOs, IPacketSession
kparser2.Protocol/       F#  PacketEvent, JSON meta
kparser2.Decoders/       F#  opcode decoders, item lookup
kparser2.Ingest/         F#  NetMQ, NDJSON, CommandClient
kparser2.Core/           F#  PacketStore, transforms, PacketSession
kparser2.Cli/            F#  headless entry (replay, decode, record, probe)
kparser2/                C#  WPF host + views
data/items.json          item id → name (from server/sql/item_basic.sql)
data/zones.json          zone id → name (from server/sql/zone_settings.sql)
```

## Common tasks

### Add opcode decoder

1. Add module under `kparser2.Decoders/` (use `Binary.fs` or `Bitstream.fs`).
2. Register in `DecoderRegistry.fs`.
3. Map to DTO in `kparser2.Core/Transforms.fs` and `DtoMapping.fs`.
4. Add unit test hex fixture + optional NDJSON golden file.
5. Verify with `dotnet test` and `kparser2.cli decode`.

### Add view tab

1. Create `UserControl` + ViewModel under `kparser2/Views/` and `kparser2/ViewModels/`.
2. Implement `IPacketView` in `kparser2/Views/PacketViews.cs`.
3. Register in `kparser2/Services/ViewRegistry.cs`.

## Live session (only when task says `live`)

Fixture replay (`analytics snapshot`, `--parity-chat`, `dotnet test`) does **not** need kpacket. Dual-dump against `fixtures/sessions/*.ndjson` is offline. Live ingest (`probe` / `record` / `watch`) needs the plugin publishing on `:5555`.

1. Close HorizonXI, then rebuild/deploy: `C:\Users\porob\git\kdev\kpacket2\build.ps1` (Ashita 4.3 SDK; plugin must export `expDestroyPlugin`)
2. Load in Ashita: `/load kpacket`
3. Confirm: `kparser2.cli probe` (or `packet_monitor.exe`)
4. Record (prints in Ashita chat via kpacket `echo`):
   `kparser2.cli record capture.ndjson --duration-ms 120000 --prompt "Cast: Yell; Cure at full HP; Protect; Blaze Spikes"`
5. Decode: `kparser2.cli decode capture.ndjson --json`
6. Run kparser2 WPF with **Session → Use Live Feed**

## Agentic parity scan

Testers **only play**. A local Cursor Agent thread on the game PC records last-green CLI and ranks settled gaps. Not a cloud Automation (`:5555` is localhost). Not WPF. No in-game cast checklist.

Docs: [docs/parity-inequalities.md](docs/parity-inequalities.md), [docs/metadata-gaps.md](docs/metadata-gaps.md).

### Paste this (solo, same box)

Horizon loaded, `/load kpacket`. New **Agent** chat (not Plan):

```
Parity scan while I play. Record last-green kparser2 CLI to ffxi-captures/ndjson with --checkpoint-ms 120000 (or attach if a recorder is already writing). Notify on `record checkpoint:` and `recording stopped:` from that process — do not start a Sleep / AGENT_LOOP_WAKE shell. On each checkpoint, reconcile and analytics snapshot --assert-settled. Rank gaps; skip deferred/spiral/open-PR codes. Classify each ticket with parity-inequalities.md (kparser-only / kparser2-missing / kparser2-extra / deferred) before coding. Lookup XiPackets, VieweD, server/scripts/enum/msg.lua — never edit those pins or kparser. Bootstrap may port a whole message family; later leftover ids. Prove with --assert-settled-code (targeted code gone; leftovers OK) plus dotnet test --filter Category!=Integration after freeze, not on every packet. Commit green work on cursor/session-<yyyymmdd-hhmm> from develop; open a draft PR into develop — never into main. Do not ask me to cast. Stop recording if I say stop or the plugin goes away.
```

### Paste this (fork / crowd)

Same play rules. You do **not** share `ffxi-captures`. Search GitHub kparser2 issues/PRs for the divergence `code` first. Fork, one family or leftover id, scoped prove, PR **into `develop`**. No push to `main`, no kdev pin bump. Redact live player names on public fixtures; never commit a full session dump.

### Scan ticks (no agent sleeper)

The long-running job is **`kparser2.cli record`** (last-green exe). Cursor auto-review treats a second `Start-Sleep` + `AGENT_LOOP_WAKE {"prompt":...}` shell as an unattended agent workflow and will block it. Do not use Cursor `/loop` that way during a scan.

Do:

- `record … --checkpoint-ms 120000` and `notify_on_output` on `record checkpoint:` / `recording stopped:`
- On checkpoint, run `scripts/opcode-heat.ps1` on the live NDJSON (FileShare read). **`HEAT unchanged` (exit 0): skip reconcile and `--assert-settled`.** Fingerprint is **shape**, not volume: extra `0x0015` / entity spam, extra known yells of the same Kind, extra `0x28` of an already-seen `commandNo`, and extra `0x00D2` rows do not count.
- Reconcile + `--assert-settled` only when heat **changed**, on first checkpoint, or on `recording stopped:`
- Optional `watch --analytics` only if you need live plugin health and it is not a synthetic prompt injector

Do not:

- `Start-Sleep`; `echo AGENT_LOOP_WAKE_… {"prompt":"…"}`
- A second timer whose only job is to inject a follow-up prompt
- Re-rank a town idle slice because packet count grew
- Infer chocobo digging from town zone names (Rabao is not a dig zone)

### Heat vs cool

Ranked `--assert-settled` only moves when these families appear or their **shape** changes (new `0x17` Kind, new `0x000B` LogoutState). Volume of the same family does not.

| Heat (re-rank) | Opcodes | Notes |
|----------------|---------|--------|
| Combat finish | S2C `0x0028`, `0x0029` | Heat fingerprints `0x28` **commandNo** and `0x29` MessageNum, **not** `0x28` finish `message`. A new cmd-4 id on an already-seen command does not wake heat; it waits for another shape change or `recording stopped:` |
| XP / limit | S2C `0x002D` MessageNum | Extra XP of an already-seen id is cool |
| New chat Kind | S2C `0x0017` Kind byte @4 | Known Yell/`Standard` spam is cool |
| Zone-in / real logout | S2C `0x000A`; `0x000B` state 1/8/9 | State **2** is zone handoff — keep recording |
| Loot rows | S2C `0x00D2` / `0x00D3` | Decoded; unnamed pool is deferred |
| Chocobo dig | S2C `0x002F`, C2S `0x0063` | Only in `server/scripts/globals/hobbies/chocobo_digging/logic.lua` `diggingZoneList` (not towns) |

| Cool (do not wake a snapshot) | Opcodes |
|-------------------------------|---------|
| Position | C2S `0x0015` |
| Nearby entities | S2C `0x000D`, `0x000E`, `0x00DF` |
| Zone-in inventory / quests / equip | S2C `0x0020`, `0x001D`, `0x0050`, `0x0051`, `0x0056` |
| Delivery box / mailbox | S2C `0x004B`, C2S `0x004D` (not in DecoderRegistry; not a settled rank) |

### Last-green ingest

Live `record` / `watch` uses the last merged **`develop`** SHA, not a session WIP. Never overwrite a locked `kparser2.Cli.exe` mid-run. `main` is the current **release** only. GitHub may keep `main` as the default branch; scan and feature PRs still set base **`develop`**.

### Ranked `--assert-settled`

Priority: `start_as_harm-*` / `fourcc_as_spell-*` → `unknown_kind-*` → `unclassified_message-*` (prefer a ParseCodes / `msg.lua` **region** in bootstrap) → `nameless_self_unnamed` after local name is known. `unnamed_entities` is deferred. In-flight nameless self-chat before `localPlayerName` is not a halt. Cmd 8 without a finish is incomplete, not a miss.

Prove: `--assert-settled-code <code>` (prefix match, e.g. `unclassified_message`). Do not require a clean camp. `--skip-code` for spiral skip-list.

### Lookup before coding

1. Frozen NDJSON slice (`scripts/reconcile-capture.ps1` if the file is still open).
2. Root `XiPackets/world/server/0xNNNN/README.md` (C2S chat is `client/0x00B5`, not S2C Help Desk `0x00B5`).
3. VieweD on a PacketViewer `.log` if needed; prefer XiPackets for the `0x28` bitstream.
4. `server/scripts/enum/msg.lua` + `sql/spell_list.sql` as named ids only (Horizon live bytes still win).
5. Optional kparser `parity.*` by name. Dual-dump of constructed fixtures ≠ Horizon.

### Self-heal

Failed prove: discard the working tree; **do not commit**. One retry only if the first attempt skipped an oracle. Same `code` fails twice: stop auto-heal, skip that code, keep recording. Do not weaken tests, edit kparser, retcon NDJSON, or classify everything Unknown. Search open PRs for the same `code` before implementing (dedup).

### Git (scan agents)

GitFlow trunks plus [Conventional Branch 1.1.0](https://conventionalbranch.org/) names (`<type>/<description>`, lowercase, hyphens). Trunks `main` and `develop` have no prefix. Do not use `parity/` as a prefix. Conventional **commits** stay (`feat:`, `fix:`, `chore:`).

`main` = current release. Do not open scan PRs against `main`.

| Layer | Branch | Who |
|-------|--------|-----|
| Scan session (Cursor) | `cursor/session-yyyymmdd-hhmm` | Agent; green commits only |
| Human / crowd one-off | `feat/...`, `fix/...` | e.g. `fix/unclassified-message-236` (no underscores) |
| Integration | `develop` | **PR target**; last-green ingest; kdev pin this cycle |
| Release cut | `release/v0.1.0` | From `develop`; then PR into `main` ([docs/RELEASING.md](docs/RELEASING.md)) |
| Hotfix | `hotfix/...` | From `main` if production is broken |
| Production | `main` | Human release + tags only |

One conventional commit per green **family** (bootstrap) or leftover `code` (converge). Draft PR into `develop` after the first green commit; freeze when Ready (tester stops, 5 commits, ~400 lines excluding fixtures, or a split-worthy change). Merge session PRs with rebase or a merge commit — **not squash**. Further gaps after freeze wait on the next session branch.

Split off `develop`: kpacket2/wire, DTO/bitstream, spirals, WPF. Crowd: fork → PR to `develop` after dedup.

kdev pin bump: after **`develop`** advances, one `chore/bump-kparser2` on the parent — not per Kind, not from `main` until a release. Scan agents never push `main` and never auto-merge.

## Do not use

- Lua kpacket on port **6666** (deprecated)
- FsNetMQ / MessagePack paths (removed)
- Elmish.WPF (removed)
