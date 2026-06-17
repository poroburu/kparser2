# Contributing to kparser2

Thank you for helping maintain the packet-native successor to KParser. This guide is written so someone **without prior kparser context** can build, verify, extend, and ship changes.

## Prerequisites

| Tool | Purpose |
|------|---------|
| [.NET 8 SDK](https://dotnet.microsoft.com/download) | Build and test |
| Windows 10/11 | WPF host (CLI works on any .NET 8 platform) |
| Git | Source control |
| (Optional) Ashita + kpacket2 | Live capture validation |
| (Optional) [LandSandBoat/server](https://github.com/LandSandBoat/server) clone | Regenerate `data/*.json` lookups |
| (Optional) [VieweD](https://github.com/ZeromusXYZ/VieweD) | Field-level packet oracle |

## Repository layout

```
kparser2.Abstractions/   C#  DTOs, session interfaces
kparser2.Protocol/       F#  PacketEvent, JSON meta
kparser2.Ingest/       F#  NetMQ, NDJSON, command client
kparser2.Decoders/     F#  Opcode decoders
kparser2.Analytics/    F#  Fight model, queries, reports
kparser2.Core/         F#  PacketStore, transforms
kparser2.Cli/          F#  Headless entry point
kparser2/              C#  WPF application
data/                  JSON lookups (from LandSandBoat SQL)
fixtures/sessions/     Golden NDJSON captures
scripts/               Export and conversion helpers
```

## First-time setup

```powershell
git clone https://github.com/poroburu/kparser2.git
cd kparser2
dotnet build kparser2.sln
dotnet test kparser2.sln
```

Verify decoders without a game:

```powershell
dotnet run --project kparser2.Cli/kparser2.Cli.fsproj -- decode fixtures/sessions/sample.ndjson
dotnet run --project kparser2.Cli/kparser2.Cli.fsproj -- analytics snapshot fixtures/sessions/combat_basic.ndjson --assert-combat
```

## Development workflow

### 1. Prefer offline oracles

Most work should **not** require FFXI running:

1. **Unit tests** — hex fixtures in `kparser2.Decoders.Tests` and `kparser2.Analytics.Tests`
2. **CLI decode** — `decode` / `replay` on `fixtures/sessions/*.ndjson`
3. **Analytics snapshot** — `analytics snapshot <file> --assert-combat`
4. **PacketViewer import** — convert retail or archived `.log` files when Ashita is unavailable
5. **VieweD** — cross-check ambiguous field offsets against `VieweD/data/ffxi/rules/ffxi.xml`

See [AGENTS.md](AGENTS.md) for command cheat sheet and [docs/AGENT_DEV.md](docs/AGENT_DEV.md) for live-session setup.

### 2. Live validation (when needed)

```powershell
# Build kpacket2, load in Ashita: /load kpacket
dotnet run --project kparser2.Cli/kparser2.Cli.fsproj -- probe
dotnet run --project kparser2.Cli/kparser2.Cli.fsproj -- record capture.ndjson --duration-ms 30000
```

Compare opcode counts with kpacket2's `packet_monitor.exe`. If ingest diverges, fix **Ingest** before touching views or analytics.

### 3. Add an opcode decoder

1. Read the packet doc in [XiPackets](https://github.com/atom0s/XiPackets) (e.g. `world/server/0x0017/README.md`)
2. Cross-check offsets with LandSandBoat handlers and VieweD rules when ambiguous
3. Add `kparser2.Decoders/<Name>0xNN.fs` using `Binary.fs` or `Bitstream.fs`
4. Register in `DecoderRegistry.fs`
5. Map events in `kparser2.Core/Transforms.fs` if needed for the UI
6. Add a hex unit test in `kparser2.Decoders.Tests`
7. Add or extend an NDJSON golden fixture
8. Run `dotnet test` and `decode` on the fixture

**Offset rule:** payloads include the 4-byte world header; decoder offsets start at byte 4.

### 4. Extend analytics

1. Classify interactions in `InteractionClassification.fs` / `InteractionBuilder.fs`
2. Add query logic in `AnalyticsQueries.fs`
3. Register a view in `kparser2/Views/AnalyticsViews.cs` if user-facing
4. Add analytics tests with fixture NDJSON
5. Validate with `analytics snapshot` and `report <queryId>`

### 5. Add a WPF view tab

1. Create `UserControl` + ViewModel under `kparser2/Views/` and `kparser2/ViewModels/`
2. Implement `IPacketView` or `IAnalyticsView` in `PacketViews.cs` / `AnalyticsViews.cs`
3. Register in `Services/ViewRegistry.cs`

### 6. Regenerate lookup data

Point scripts at a local LandSandBoat SQL tree (default: `../server/sql/`):

```powershell
dotnet run --project kparser2.Cli/kparser2.Cli.fsproj -- export-items
dotnet run --project kparser2.Cli/kparser2.Cli.fsproj -- export-actions
dotnet run --project kparser2.Cli/kparser2.Cli.fsproj -- export-zones
```

Commit updated `data/*.json` when game data changes matter for your change.

### 7. Promote captures to fixtures

Keep fixtures **small and focused** (login, one fight, one loot drop). Do not commit long live sessions.

```powershell
dotnet run --project kparser2.Cli/kparser2.Cli.fsproj -- record my_slice.ndjson --duration-ms 15000
# Trim manually or script, then copy to fixtures/sessions/
```

## Pull request checklist

- [ ] `dotnet build kparser2.sln` succeeds
- [ ] `dotnet test kparser2.sln` passes
- [ ] New behavior has a fixture or unit test where practical
- [ ] `decode` / `analytics snapshot` run clean on affected fixtures
- [ ] Public CLI behavior documented if commands or flags changed
- [ ] Upstream sources credited in code comments or [docs/CREDITS.md](docs/CREDITS.md) when referencing external layouts

## Coding conventions

- **F#** for protocol, decoders, analytics, ingest — match existing module style
- **C#** for WPF and abstractions — nullable reference types enabled
- Minimize scope: one opcode or one query per PR when possible
- Comments only for non-obvious packet layout or server quirks

## Becoming a maintainer

kparser2 is designed for handoff:

1. Read [README.md](README.md) for the KParser → packet rewrite rationale
2. Read [docs/CREDITS.md](docs/CREDITS.md) for upstream dependencies
3. Use CLI + fixtures for day-to-day work; live game only for ingest plugin contract changes
4. Cut releases per [docs/RELEASING.md](docs/RELEASING.md)

Open a GitHub issue or discussion if you want commit access or to coordinate larger architectural changes.

## What not to use

- Lua kpacket on port **6666** (deprecated)
- FsNetMQ / MessagePack ingest paths (removed)
- KParser `.sdf` as a data source (not implemented)
