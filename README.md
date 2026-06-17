# kparser2

Packet-native parser and analytics tool for *Final Fantasy XI*, targeting HorizonXI and other classic-era private servers. kparser2 is a ground-up rewrite of [KParser](https://github.com/poroburu/kparser) that reads **network packets** instead of **in-game chat log memory**.

Live capture uses the [kpacket2](https://github.com/poroburu/kpacket2) Ashita v4 plugin. Offline development uses NDJSON session fixtures and PacketViewer log import — no game client required.

## Background: KParser and why kparser2 exists

### What KParser was

[KParser](https://github.com/poroburu/kparser) (originally by Wayward Gamers, ~2008) is a Windows desktop parser for FFXI. It attached to the running `pol.exe` process, followed a chain of **hard-coded memory offsets** to the client's internal chat log ring buffer, and read new chat lines as they appeared. Those lines were parsed as **text** — damage numbers, spell names, loot messages, XP gains — and stored in a local **SQL Server Compact** (`.sdf`) database for query tabs (offense, defense, fights, loot, and dozens more).

That design worked for years on retail and private servers, but had structural limits:

| KParser (chat log) | Problem |
|--------------------|---------|
| Memory offset per client build | Every game patch or private-server fork needs a new offset (e.g. EdenXI `0x0061D140`, HorizonXI `0x62d8f0`) |
| Text parsing | Combat detail depends on English message templates; ambiguous or custom server strings break parsers |
| RAM scraping | Requires running KParser alongside the game; fragile across POL versions |
| .NET Framework 4 + SQL CE | Hard to build, test headlessly, or hand off to new maintainers |

Hotfix releases for [EdenXI](https://github.com/poroburu/kparser/releases/tag/Eden-Hotfix) and [HorizonXI](https://github.com/poroburu/kparser/releases/tag/HorizonXI-Hotfix) kept KParser alive on private servers, but the architecture did not scale.

### What kparser2 changes

kparser2 keeps the **analytics goals** of KParser (fights, offense/defense, loot, XP, job-specific reports) but replaces the **data source**:

```
KParser:   FFXI RAM → chat log text → regex/templates → .sdf database → WPF queries
kparser2:  kpacket2 → raw packets → opcode decoders → analytics model → WPF / CLI / .kparse2.json
```

| Area | kparser2 approach |
|------|-------------------|
| Capture | [kpacket2](https://github.com/poroburu/kpacket2) Ashita plugin publishes world packets on ZMQ (`tcp://localhost:5555`) |
| Decode | F# opcode modules (0x17 chat, 0x28 action, 0x29 battle message, 0xD2/D3 loot, …) aligned with [XiPackets](https://github.com/atom0s/XiPackets) and [LandSandBoat](https://github.com/LandSandBoat/server) |
| Storage | NDJSON session files (replayable, diffable, CI-friendly) and `.kparse2.json` report export |
| UI | .NET 8 WPF host + headless CLI for agents and regression |
| Validation | Golden fixtures, unit tests, PacketViewer import, [VieweD](https://github.com/ZeromusXYZ/VieweD) field oracles |

**Not supported:** legacy KParser `.sdf` databases, Lua kpacket on port 6666, or per-build RAM offsets.

## Features (current)

### Packet views
- **Debug** — raw packet stream
- **Packet Monitor** — opcode histogram and live feed
- **Chat** — decoded 0x17 / 0xB5 chat
- **Item Drops** — trophy / loot packets
- **Combat** — live combat action and battle messages

### Analytics views
Fight segmentation, offense/defense (summary and detail), deaths, recovery, buffs/debuffs, skillchains, job-specific tabs (THF, COR, …), experience, loot, damage graph, and more — see `kparser2/Views/AnalyticsViews.cs` for the full catalog.

### CLI (headless)
```powershell
dotnet run --project kparser2.Cli/kparser2.Cli.fsproj -- decode fixtures/sessions/sample.ndjson
dotnet run --project kparser2.Cli/kparser2.Cli.fsproj -- analytics snapshot fixtures/sessions/combat_basic.ndjson
dotnet run --project kparser2.Cli/kparser2.Cli.fsproj -- export report fixtures/sessions/combat_basic.ndjson -o session.kparse2.json
dotnet run --project kparser2.Cli/kparser2.Cli.fsproj -- record capture.ndjson --duration-ms 60000
```

Run with no arguments for the full command list.

## Requirements

- **.NET 8 SDK**
- **Windows** (WPF host; CLI libraries run cross-platform)
- **Live capture:** [kpacket2](https://github.com/poroburu/kpacket2) built and loaded in Ashita (`/load kpacket`)

### kpacket2 wire contract

| Socket | Endpoint | Purpose |
|--------|----------|---------|
| PUB | `tcp://localhost:5555` | Multipart: topic + JSON meta + raw bytes |
| REP | `tcp://localhost:5556` | JSON commands (`status`, `stats`, `hello`) |

## Quick start

### Build
```powershell
dotnet build kparser2.sln
dotnet test kparser2.sln
```

### WPF (offline replay by default)
```powershell
dotnet run --project kparser2/kparser2.csproj
```

Use **Session → Open NDJSON…** for a fixture, **Session → Use Live Feed** for kpacket2, or **Session → Import PacketViewer…** for a `.log` capture.

### Verify without the game
```powershell
dotnet run --project kparser2.Cli/kparser2.Cli.fsproj -- replay fixtures/sessions/sample.ndjson
dotnet run --project kparser2.Cli/kparser2.Cli.fsproj -- decode fixtures/sessions/combat_action.ndjson --json
```

Expected `decode sample.ndjson` output:
```
0x0017 GP_SERV_COMMAND_CHAT_STD
  chat [Say] Alice: Hello from fixture
0x00D2 GP_SERV_COMMAND_TROPHY_LIST
  loot Found item=... (704) actor=Entity 12345
Decoded 2 packets with structured events from ...
```

## Architecture

| Project | Role |
|---------|------|
| `kparser2.Abstractions` | C# DTOs, `IPacketSession`, `IAnalyticsSession` |
| `kparser2.Protocol` | F# `PacketEvent`, JSON meta parsing |
| `kparser2.Ingest` | NetMQ subscriber (5555), REQ client (5556), NDJSON record/replay |
| `kparser2.Decoders` | F# opcode decoders, entity registry, item/zone lookups |
| `kparser2.Analytics` | Fight segmentation, interaction classification, query engine |
| `kparser2.Core` | `PacketStore`, transforms, session wiring |
| `kparser2.Cli` | Headless replay, decode, record, analytics, reports |
| `kparser2` | C# WPF host |

**F# backend + C# UI.** Packet payloads include the 4-byte world header; decoder field offsets start at **byte 4**.

## Fixtures

Golden sessions in `fixtures/sessions/`:

| File | Contents |
|------|----------|
| `sample.ndjson` | chat (0x17), trophy list (0xD2), action |
| `login.ndjson` | enter zone + system chat |
| `item_drop.ndjson` | trophy list + solution |
| `combat_basic.ndjson` | battle message (0x29) |
| `combat_action.ndjson` | combat action (0x28) melee + spell |
| `combat_death.ndjson` | defeat + falls to ground |
| `combat_recovery.ndjson` | cure via 0x29 + 0x28 |
| `chat_xp.ndjson` | MsgBasic XP + EXP chain |
| `bcmn30_petrifying_pair.ndjson` | retail BCMN30 slice (mobs, combat, defeat) |

## Documentation

| Doc | Audience |
|-----|----------|
| [CONTRIBUTING.md](CONTRIBUTING.md) | Developers and future maintainers |
| [CHANGELOG.md](CHANGELOG.md) | Release history |
| [docs/CREDITS.md](docs/CREDITS.md) | Upstream sources and attribution |
| [docs/RELEASING.md](docs/RELEASING.md) | Cutting a GitHub release |
| [docs/report-schema.md](docs/report-schema.md) | `.kparse2.json` interchange format |
| [AGENTS.md](AGENTS.md) | Cursor agent quick reference |
| [docs/AGENT_DEV.md](docs/AGENT_DEV.md) | Live session and oracle workflows |

## Roadmap / known gaps

- Opcode coverage is incremental — many packets decode as raw bytes only
- `IReportPublisher` for horizonxilogs upload is stubbed, not implemented
- No `.sdf` importer; analytics start from packets or `.kparse2.json`
- Retail is validated via PacketViewer captures; live retail + Ashita is the primary dev path for HorizonXI

## License

kparser2 is maintained by [poroburu](https://github.com/poroburu). See [docs/CREDITS.md](docs/CREDITS.md) for upstream projects. **Add an explicit LICENSE file before distributing binaries** if one is not yet present in the repository.

## Related projects

- [kparser](https://github.com/poroburu/kparser) — original chat-log parser (maintenance mode)
- [kpacket2](https://github.com/poroburu/kpacket2) — C++ Ashita packet capture plugin
- [XiPackets](https://github.com/atom0s/XiPackets) — packet structure reference
- [LandSandBoat](https://github.com/LandSandBoat/server) — emulator SQL used for item/zone/action lookups
- [VieweD](https://github.com/ZeromusXYZ/VieweD) — independent packet field oracle
