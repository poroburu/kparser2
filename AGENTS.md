# Agent guide — kparser2

This document helps Cursor agents iterate on kparser2 **without Ashita running**.

## Quick commands

```powershell
# Build everything
dotnet build C:\Users\porob\git\kparser2\kparser2.sln

# Replay golden fixture (preferred verification)
dotnet run --project C:\Users\porob\git\kparser2\kparser2.Cli\kparser2.Cli.fsproj -- replay C:\Users\porob\git\kparser2\fixtures\sessions\sample.ndjson

# Filter by opcode
dotnet run --project C:\Users\porob\git\kparser2\kparser2.Cli\kparser2.Cli.fsproj -- replay C:\Users\porob\git\kparser2\fixtures\sessions\sample.ndjson --filter 0x17

# JSON output for assertions
dotnet run --project C:\Users\porob\git\kparser2\kparser2.Cli\kparser2.Cli.fsproj -- replay C:\Users\porob\git\kparser2\fixtures\sessions\sample.ndjson --json

# Stats via replay
dotnet run --project C:\Users\porob\git\kparser2\kparser2.Cli\kparser2.Cli.fsproj -- stats --replay C:\Users\porob\git\kparser2\fixtures\sessions\item_drop.ndjson

# Live plugin health (requires game + kpacket2 loaded)
dotnet run --project C:\Users\porob\git\kparser2\kparser2.Cli\kparser2.Cli.fsproj -- hello
```

## Fixture paths

| File | Contents |
|------|----------|
| `fixtures/sessions/sample.ndjson` | 3 packets: chat, item, action |
| `fixtures/sessions/login.ndjson` | zone + chat |
| `fixtures/sessions/item_drop.ndjson` | loot + item |

Expected replay output for `sample.ndjson` (non-JSON mode):

```
0x0017 incoming GP_SERV_CHAT ...
0x00D2 incoming GP_SERV_ITEM ...
0x001A outgoing GP_CLI_ACTION ...
Processed 3 matching packets (total 3)
```

## Project map

```
kparser2.Abstractions/   C#  DTOs, IPacketSession
kparser2.Protocol/       F#  PacketEvent, JSON meta
kparser2.Ingest/         F#  NetMQ, NDJSON, CommandClient
kparser2.Core/           F#  PacketStore, transforms, PacketSession
kparser2.Cli/            F#  headless entry
kparser2/                C#  WPF host + views
```

## Common tasks

### Add opcode transform

1. Edit `kparser2.Core/Transforms.fs`
2. Verify with CLI replay + fixture
3. Check Chat or Item Drops view if applicable

### Add view tab

1. Create `UserControl` + ViewModel under `kparser2/Views/` and `kparser2/ViewModels/`
2. Implement `IPacketView` in `kparser2/Views/PacketViews.cs`
3. Register in `kparser2/Services/ViewRegistry.cs`

### Fix ingest

1. Compare against `packet_monitor.exe` from kpacket2 build
2. Ensure multipart frame 2 (raw bytes) is read — JSON meta alone is incomplete

## Live session (only when task says `live`)

1. Build kpacket2 plugin: `cmake --build` in kpacket2 repo
2. Load in Ashita: `/load kpacket`
3. Run `packet_monitor.exe` as oracle
4. Run kparser2 with **Session → Use Live Feed**

## Do not use

- Lua kpacket on port **6666** (deprecated)
- FsNetMQ / MessagePack paths (removed)
- Elmish.WPF (removed)
