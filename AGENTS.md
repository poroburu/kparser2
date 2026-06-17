# Agent guide — kparser2

This document helps Cursor agents iterate on kparser2 **without Ashita running**.

## Quick commands

```powershell
# Build everything
dotnet build C:\Users\porob\git\kparser2\kparser2.sln

# Run all unit tests (sync ingest path; ~2 min)
dotnet test C:\Users\porob\git\kparser2\kparser2.sln

# CI / agent fast path — skip retail NDJSON slices (~3100 lines each)
dotnet test C:\Users\porob\git\kparser2\kparser2.sln --filter "Category!=Integration"

# Replay golden fixture (preferred verification)
dotnet run --project C:\Users\porob\git\kparser2\kparser2.Cli\kparser2.Cli.fsproj -- replay C:\Users\porob\git\kparser2\fixtures\sessions\sample.ndjson

# Structured decoder output (no WPF required)
dotnet run --project C:\Users\porob\git\kparser2\kparser2.Cli\kparser2.Cli.fsproj -- decode C:\Users\porob\git\kparser2\fixtures\sessions\sample.ndjson

# Filter by opcode
dotnet run --project C:\Users\porob\git\kparser2\kparser2.Cli\kparser2.Cli.fsproj -- decode C:\Users\porob\git\kparser2\fixtures\sessions\sample.ndjson --filter 0x17 --json

# Regenerate item name lookup from LandSandBoat SQL
dotnet run --project C:\Users\porob\git\kparser2\kparser2.Cli\kparser2.Cli.fsproj -- export-items

# Regenerate ability name lookup from LandSandBoat SQL
dotnet run --project C:\Users\porob\git\kparser2\kparser2.Cli\kparser2.Cli.fsproj -- export-actions

# Regenerate zone name lookup from LandSandBoat SQL
dotnet run --project C:\Users\porob\git\kparser2\kparser2.Cli\kparser2.Cli.fsproj -- export-zones

# Import PacketViewer .log → NDJSON (fixed s2c/c2s topics)
dotnet run --project C:\Users\porob\git\kparser2\kparser2.Cli\kparser2.Cli.fsproj -- import packetviewer --full C:\path\to\full.log -o capture.ndjson

# Validate imported capture (entity/opcode/analytics summary)
dotnet run --project C:\Users\porob\git\kparser2\kparser2.Cli\kparser2.Cli.fsproj -- import packetviewer --validate capture.ndjson

# Analytics snapshot (waits for replay completion; no 500 ms race)
dotnet run --project C:\Users\porob\git\kparser2\kparser2.Cli\kparser2.Cli.fsproj -- analytics snapshot capture.ndjson

# Regenerate synthetic fixtures with valid packet bytes
powershell -File C:\Users\porob\git\kparser2\scripts\generate-fixtures.ps1

# Live plugin health (requires game + kpacket2 loaded)
dotnet run --project C:\Users\porob\git\kparser2\kparser2.Cli\kparser2.Cli.fsproj -- probe

# Long BST camp session (20 min record + post-session oracles)
dotnet run --project C:\Users\porob\git\kparser2\kparser2.Cli\kparser2.Cli.fsproj -- record C:\Users\porob\git\ffxi-captures\ndjson\bst_leveling.ndjson --duration-ms 1200000
dotnet run --project C:\Users\porob\git\kparser2\kparser2.Cli\kparser2.Cli.fsproj -- watch --analytics --duration-ms 300000 --interval-ms 5000
dotnet run --project C:\Users\porob\git\kparser2\kparser2.Cli\kparser2.Cli.fsproj -- analytics snapshot capture.ndjson --assert-combat --min-battles 2
dotnet run --project C:\Users\porob\git\kparser2\kparser2.Cli\kparser2.Cli.fsproj -- report fights capture.ndjson
dotnet run --project C:\Users\porob\git\kparser2\kparser2.Cli\kparser2.Cli.fsproj -- report offense capture.ndjson
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
dotnet test C:\Users\porob\git\kparser2\kparser2.Analytics.Tests\kparser2.Analytics.Tests.fsproj --filter "FullyQualifiedName~ParseCodesParity|FullyQualifiedName~InteractionParity|FullyQualifiedName~FixtureReplay"

# Retail integration slices (bcmn30_petrifying_pair, bst_camp_multi, bst_loot_name)
dotnet test C:\Users\porob\git\kparser2\kparser2.Analytics.Tests\kparser2.Analytics.Tests.fsproj --filter "Category=Integration"

# Regenerate synthetic combat parity fixtures
powershell -File C:\Users\porob\git\kparser2\scripts\generate-fixtures.ps1
```

**Test reliability:** Analytics tests use synchronous `ReplayHelpers.ingestFixture` (not `PacketSessionFactory.fromReplayDefault`) so replay does not spawn background threads or block on `WaitForReplayComplete`. `PacketSession` replay is validated via `kparser2.cli analytics snapshot` (unit test skipped — xUnit sync-over-async deadlock). `xunit.runner.json` disables parallelization to avoid `EntityRegistry` races.

Parity matrix rows are named after kparser test methods (`TestPlayerHitMob`, `FailSelfBuff`, `region_enfeeble`, etc.). Mark a row done when Tier 1–3 tests pass for that scenario.

Reference captures (local, not committed): `C:\Users\porob\git\ffxi-captures\` — NDJSON recordings and retail unpacks for VieweD + CLI decode oracles. Promote small slices into `fixtures/sessions/` for golden tests.

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

1. Build kpacket2 plugin: `C:\Users\porob\git\kpacket2\build.ps1`
2. Load in Ashita: `/load kpacket`
3. Run `packet_monitor.exe` as oracle
4. Record: `kparser2.cli record capture.ndjson --duration-ms 30000`
5. Decode: `kparser2.cli decode capture.ndjson --json`
6. Run kparser2 WPF with **Session → Use Live Feed**

## Do not use

- Lua kpacket on port **6666** (deprecated)
- FsNetMQ / MessagePack paths (removed)
- Elmish.WPF (removed)
