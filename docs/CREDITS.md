# Credits and upstream sources

kparser2 stands on work from the FFXI reverse-engineering and private-server communities. When extending decoders or analytics, cite these sources in code comments and keep this file updated.

## KParser (original)

| | |
|---|---|
| **Project** | [poroburu/kparser](https://github.com/poroburu/kparser) (fork/maintenance of Google Code archive) |
| **Original authors** | Wayward Gamers (2008–2011) |
| **What we inherited** | Analytics *concepts* — fight segmentation, offense/defense queries, loot/XP tracking, WPF tab model |
| **What we replaced** | RAM chat-log scraping (`RamReader`), text `MessageLine` parsing, SQL CE `.sdf` storage, per-build memory offsets |

KParser read the client's internal chat log via `ReadProcessMemory` and parsed English combat strings. kparser2 decodes the same *information* from structured packets where possible.

## Packet capture

| Project | Role | Link |
|---------|------|------|
| **kpacket2** | C++ Ashita v4 plugin; ZMQ PUB on `:5555`, REQ on `:5556` | [poroburu/kpacket2](https://github.com/poroburu/kpacket2) |
| **kpacket** (legacy) | Early Lua Ashita proof-of-concept (port 6666) — **not supported** | [poroburu/kpacket](https://github.com/poroburu/kpacket) |
| **Ashita** | FFXI client framework hosting kpacket2 | [Ashita](https://ashitaxi.com/) |

## Packet structure reference

| Project | Role | Link |
|---------|------|------|
| **XiPackets** | Opcode names, field layouts, handler cross-refs (primary decoder oracle) | [atom0s/XiPackets](https://github.com/atom0s/XiPackets) |
| **VieweD** | Independent field dump oracle; `ffxi.xml` rules | [ZeromusXYZ/VieweD](https://github.com/ZeromusXYZ/VieweD) |
| **PacketViewer** | Retail capture format; import via `import packetviewer` | Historical Windower/community tool; logs validated through VieweD |

Decoder offsets in kparser2 should agree with XiPackets first, then LandSandBoat server handlers, then VieweD rules for ambiguous fields (e.g. 0x00E name @ 0x34, 0x00DF vitals).

## Game data lookups

Generated JSON under `data/` comes from **LandSandBoat** SQL (GPL-3.0):

| File | SQL source |
|------|------------|
| `data/items.json` | `item_basic.sql` |
| `data/actions.json` | `skills.sql` / action tables (see `scripts/export-actions.ps1`) |
| `data/zones.json` | `zone_settings.sql` |
| `data/mob_xp.json` | mob XP tables (see `scripts/export-mob-xp.ps1`) |

| | |
|---|---|
| **Project** | [LandSandBoat/server](https://github.com/LandSandBoat/server) |
| **License** | GNU GPL v3 |
| **Usage** | Lookup tables only; kparser2 does not embed or ship the emulator |

Regenerate after updating your local `server` SQL checkout:

```powershell
dotnet run --project kparser2.Cli/kparser2.Cli.fsproj -- export-items
dotnet run --project kparser2.Cli/kparser2.Cli.fsproj -- export-actions
dotnet run --project kparser2.Cli/kparser2.Cli.fsproj -- export-zones
```

## MsgBasic / battle message codes

`kparser2.Analytics/ParseCodesTables.fs` maps 0x29 `GP_SERV_COMMAND_BATTLE_MESSAGE` message numbers to interaction categories. Values align with LandSandBoat `MsgBasic` enumerations and KParser parse-code tables.

## Libraries

| Package | Use |
|---------|-----|
| [NetMQ](https://github.com/zeromq/netmq) | ZMQ ingest |
| [FSharp.SystemTextJson](https://github.com/Zaid-Ajaj/FSharp.SystemTextJson) | JSON in F# projects |
| [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) | WPF view models |
| [LiveCharts2](https://github.com/beto-rodriguez/LiveCharts2) | Damage graph |
| [System.Reactive](https://github.com/dotnet/reactive) | Packet session streams |
| [xUnit](https://xunit.net/) | Unit tests |

## Private server targets

kparser2 is developed and tested primarily against **HorizonXI**. [EdenXI](https://github.com/poroburu/kparser/releases/tag/Eden-Hotfix) compatibility is a goal where packet layouts match; report server-specific divergences as issues with a captured NDJSON slice or PacketViewer log.

## Maintainer note

If you fork or take over maintenance:

1. Keep this file accurate when adding data sources or oracles
2. Do not strip Wayward Gamers / KParser attribution from historical context
3. Respect LandSandBoat GPL when redistributing regenerated `data/*.json` — document the SQL commit used
4. Add a root `LICENSE` file before publishing binaries if the repository does not yet include one
