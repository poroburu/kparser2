# Changelog

All notable changes to kparser2 are documented here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [0.1.0] - 2026-06-16

First public release: packet-native rewrite of KParser with offline replay, analytics, and headless CLI.

### Added

- **Ingest:** NetMQ subscriber for kpacket2 (`5555`), REQ command client (`5556`), NDJSON record/replay
- **Decoders:** 0x17/0xB5 chat, 0x28 combat action, 0x29 battle message, 0xD2/0xD3 trophy/loot
- **Entity registry** from 0x00E spawns and 0x00DF vitals; zone lookup from 0x00A / 0x00DF
- **Analytics:** fight segmentation, offense/defense/recovery/deaths, buffs, skillchains, job-specific queries, XP parsing
- **WPF UI:** packet monitor, chat, combat, item drops, and full analytics tab catalog
- **CLI:** `replay`, `decode`, `record`, `probe`, `watch`, `analytics snapshot`, `export report`, `import report`, `import packetviewer`, lookup exporters
- **Report interchange:** `.kparse2.json` schema v1 ([docs/report-schema.md](docs/report-schema.md))
- **Golden fixtures** under `fixtures/sessions/` for CI and agent oracles
- **Data lookups** (`data/items.json`, `actions.json`, `zones.json`, `mob_xp.json`) generated from LandSandBoat SQL
- **Documentation:** README, CONTRIBUTING, CREDITS, RELEASING, AGENTS guides

### Changed from KParser

- Data source: in-game chat log RAM → kpacket2 world packets
- Runtime: .NET Framework 4 + SQL CE → .NET 8 + NDJSON / JSON reports
- Parsing: English text templates → binary opcode decoders (XiPackets / LandSandBoat aligned)
- Testing: manual in-game only → fixtures, unit tests, CLI decode oracles, PacketViewer import

### Removed / not carried forward

- SQL Server Compact `.sdf` database format
- Per-client-build memory offsets
- Lua kpacket (port 6666) and MessagePack ingest paths
- Elmish.WPF UI stack

### Known limitations

- Subset of opcodes decoded; most packets appear as raw in monitor view
- Legacy KParser `.sdf` files cannot be imported
- WPF host requires Windows; CLI core libraries run on .NET 8 anywhere

[0.1.0]: https://github.com/poroburu/kparser2/releases/tag/v0.1.0
