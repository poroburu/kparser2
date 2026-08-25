# Agent development — live sessions

## kpacket2 setup

1. Build the C++ plugin from [kpacket2](https://github.com/poroburu/kpacket2):
   ```powershell
   cmake --preset=default
   cmake --build out/build/x86-release-win --config Release
   ```
2. Copy `kpacket.dll` to Ashita `plugins/kpacket/`
3. In game: `/load kpacket`

## Ports

| Port | Pattern | Direction |
|------|---------|-----------|
| 5555 | PUB/SUB | Packet streaming |
| 5556 | REQ/REP | Commands |
| 5557 | PUSH/PULL | Reliable queue (optional, not used by kparser2 MVP) |

## Validation oracle

Build and run kpacket2's reference client:

```powershell
cmake --build out/build/x86-release-win --target packet_monitor
.\out\build\x86-release-win\examples\packet_monitor.exe
```

Compare opcode counts and metadata with:

```powershell
dotnet run --project kparser2.Cli/kparser2.Cli.fsproj -- record live.ndjson --duration-ms 30000
dotnet run --project kparser2.Cli/kparser2.Cli.fsproj -- replay live.ndjson
```

If they diverge, the bug is in ingest — not views.

## Recording new fixtures

```powershell
dotnet run --project kparser2.Cli/kparser2.Cli.fsproj -- record C:\Users\porob\git\kdev\ffxi-captures\ndjson\my_session.ndjson --duration-ms 15000
```

Commit small, focused fixtures (login, chat, drop) rather than long captures.

## Health check

```powershell
dotnet run --project kparser2.Cli/kparser2.Cli.fsproj -- hello
dotnet run --project kparser2.Cli/kparser2.Cli.fsproj -- stats
```

Both require kpacket2 running with REP bound on 5556.

## PacketViewer retail captures

When Ashita is unavailable, validate against PV logs under `ffxi-captures/retail/_extract/`:

```powershell
dotnet run --project kparser2.Cli/kparser2.Cli.fsproj -- import packetviewer `
  --full C:\Users\porob\git\kdev\ffxi-captures\retail\_extract\PetrifyingPair\packetviewer\full.log `
  -o C:\Users\porob\git\kdev\ffxi-captures\ndjson\petrifying_pair.ndjson

dotnet run --project kparser2.Cli/kparser2.Cli.fsproj -- import packetviewer --validate `
  C:\Users\porob\git\kdev\ffxi-captures\ndjson\petrifying_pair.ndjson

dotnet run --project kparser2.Cli/kparser2.Cli.fsproj -- analytics snapshot `
  C:\Users\porob\git\kdev\ffxi-captures\ndjson\petrifying_pair.ndjson
```

WPF: **Session → Import PacketViewer…** — select `full.log` or `incoming.log` + `outgoing.log`; converts to temp NDJSON and replays.

Oracle chain: PV import → `--validate` → `analytics snapshot` → VieweD field dump for ambiguous opcodes.

Mid-capture logs often lack **0x00A**; local player is inferred from **0x00DF UniqueNo@4**. Zone id may be zero in instanced battles until **0x00DF ZoneNo@26** or **0x00A@0x30** carries a non-zero value.

Golden regression fixture: `fixtures/sessions/bcmn30_petrifying_pair.ndjson` (retail BCMN30 slice with mob names, combat, defeat).
