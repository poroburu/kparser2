# kparser2

Packet-native FFXI parser for HorizonXI, built on the [kpacket2](https://github.com/poroburu/kpacket2) C++ Ashita plugin.

## Architecture

| Project | Role |
|---------|------|
| `kparser2.Abstractions` | C# DTOs and `IPacketSession` boundary |
| `kparser2.Protocol` | F# packet types and JSON meta parsing |
| `kparser2.Ingest` | NetMQ subscriber (5555), REQ client (5556), NDJSON record/replay |
| `kparser2.Core` | F# `PacketStore`, transforms, session |
| `kparser2.Cli` | Headless replay/stats for agents and CI |
| `kparser2` | C# WPF host with Debug, Monitor, Chat, and Item Drops views |

**F# backend + C# UI.** Elmish.WPF, FsNetMQ, and MessagePack (Lua kpacket PoC) have been removed.

## Requirements

- .NET 8 SDK
- Windows (WPF host)
- [kpacket2](https://github.com/poroburu/kpacket2) Ashita v4 plugin loaded in-game for live capture

### kpacket2 wire contract

| Socket | Endpoint | Purpose |
|--------|----------|---------|
| PUB | `tcp://localhost:5555` | Multipart: topic + JSON meta + raw bytes |
| REP | `tcp://localhost:5556` | JSON commands (`status`, `stats`, `hello`) |

> **Note:** The legacy [Lua kpacket](https://github.com/poroburu/kpacket) addon on port **6666** / MessagePack is deprecated and no longer supported by kparser2.

## Build

```powershell
dotnet build kparser2.sln
```

## Run

### WPF (offline replay by default)

```powershell
dotnet run --project kparser2/kparser2.csproj
```

Use **Session** menu to switch between fixture replay and live feed (`5555`).

### CLI

```powershell
dotnet run --project kparser2.Cli/kparser2.Cli.fsproj -- replay fixtures/sessions/sample.ndjson
dotnet run --project kparser2.Cli/kparser2.Cli.fsproj -- replay fixtures/sessions/sample.ndjson --filter 0x17
dotnet run --project kparser2.Cli/kparser2.Cli.fsproj -- stats --replay fixtures/sessions/item_drop.ndjson
dotnet run --project kparser2.Cli/kparser2.Cli.fsproj -- hello
dotnet run --project kparser2.Cli/kparser2.Cli.fsproj -- record capture.ndjson --duration-ms 10000
```

## Fixtures

Golden session files live in `fixtures/sessions/`:

- `sample.ndjson` — mixed chat, item, action packets
- `login.ndjson` — zone + chat
- `item_drop.ndjson` — loot + item packets

Agents and tests should prefer replay over a live game session.

## Validation oracle

When testing live ingest, compare against kpacket2's `packet_monitor.exe` on the same session. If opcode counts or metadata diverge, fix ingest before building views.

See [AGENTS.md](AGENTS.md) and [docs/AGENT_DEV.md](docs/AGENT_DEV.md) for agent workflows.
