# Changelog

All notable changes to kparser2 are documented here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

## [0.1.0-rc.2] - 2026-08-26

Second pre-release. Live HorizonXI parity work on `develop` since rc.1.

### Added

- **0x002D** XP/limit battle-message decoder; live EXP chain uses Data as XP and Data2 as chain
- Spell name lookup (`data/spells.json`, `export-spells`)
- HorizonXI yell layout (zone in Data) and `0xFD` auto-translate in chat
- `analytics snapshot --parity-chat` / `--assert-chat` and `scripts/compare-chat-parity.ps1`
- Settled-gap ranking: `--assert-settled`, `--assert-settled-code`, `--skip-code`; opcode-heat skip via `scripts/opcode-heat.ps1`
- Record session lifecycle: stop on DC, stall, or session UUID change; keep recording through `0x000B` zone handoff; `--checkpoint-ms` stdout
- Capture helpers: `scripts/reconcile-capture.ps1`, `scripts/wait-kpacket-session.ps1`
- Live `0x28`/`0x29` classifiers: magic finish (skip start), drain, burst, erase, extra targeting, status wear-off, interrupted, out of range, unable-to-see, `/check` evasion, JA blocked/timer/jug, must-have-pet-food
- Golden slices: `chat_yell_live`, `combat_magic_live`
- CI on `develop` PRs

### Changed

- GitFlow: integration PRs target `develop`; `main` is the release line
- Experience-report base XP and exclude-0-XP use session awards (kparser chain reverse), not a stub catalog

### Fixed

- Local player resolution on mid-capture replay (no `0x00A`)
- Live `0x002D` EXP chain XP from Data

### Removed

- Stub `data/mob_xp.json`, `MobXpLookup`, and `scripts/export-mob-xp.ps1`

### Notes

- Pair with [kpacket2 v0.1.0-rc.1](https://github.com/poroburu/kpacket2/releases/tag/v0.1.0-rc.1) for live capture; wire protocol `kpacket.v1` unchanged
- `kparser2.cli echo` needs kpacket2 Ashita chat echo (not in kpacket2 rc.1)

## [0.1.0-rc.1] - 2026-06-17

First pre-release of the packet-native FFXI parser and analytics tool for the kpacket2 stack.

### Added

- **Ingest:** NetMQ subscriber for kpacket2 (`tcp://localhost:5555`), REQ command client (`5556`), NDJSON record/replay with optional `kparser2.session` header line
- **Wire contract:** multipart ZMQ topic + JSON meta + raw bytes; topics `kpacket.v1.world.s2c.0xHHHH` / `kpacket.v1.world.c2s.0xHHHH`
- **Decoders:** 0x17/0xB5 chat, 0x28 combat action, 0x29 battle message, 0xD2/0xD3 trophy/loot
- **Entity registry** from 0x00A login, 0x00D/0x00E spawns, 0x00DF vitals, 0x0068/0x00DD updates; zone lookup from 0x00A / 0x00DF
- **Analytics:** fight segmentation, offense/defense/recovery/deaths, buffs, skillchains, job-specific queries, XP parsing
- **WPF UI:** packet monitor, chat, combat, item drops, and full analytics tab catalog (Windows / .NET 8)
- **CLI:** `replay`, `decode`, `record`, `probe`, `watch`, `hello`, `stats`, `analytics snapshot`, `report`, `export report`, `import report`, `import packetviewer`, lookup exporters (`export-items`, `export-actions`, `export-zones`)
- **CLI validation flags:** `analytics snapshot --assert-combat`, `--assert-names`, `--min-battles N`
- **Report interchange:** `.kparse2.json` schema v1 with `kparser2_version` stamped from assembly semver ([docs/report-schema.md](docs/report-schema.md))
- **Golden fixtures:** 27 NDJSON sessions under `fixtures/sessions/` including `bcmn30_petrifying_pair`, `bst_camp_multi`, `bst_loot_name`, and synthetic combat parity suite
- **kparser parity:** ParseCodes alignment tests (Tier 1–3), InteractionParity, FixtureReplayParity
- **Data lookups:** `data/items.json`, `actions.json`, `zones.json` generated from LandSandBoat SQL
- **Version stamping:** `Directory.Build.props` semver (`0.1.0-rc.1`) in published binaries and report exports
- **Documentation:** README, CONTRIBUTING, CREDITS, RELEASING, COMPATIBILITY, AGENTS guides
- **CI:** GitHub Actions build + test workflow (`Category!=Integration`)
- **LICENSE:** MIT

### Changed from KParser

- Data source: in-game chat log RAM → kpacket2 world packets
- Runtime: .NET Framework 4 + SQL CE → .NET 8 + NDJSON / JSON reports
- Parsing: English text templates → binary opcode decoders (XiPackets / LandSandBoat aligned)
- Testing: manual in-game only → fixtures, unit tests, CLI decode oracles, PacketViewer import

### Fixed

- Offline NDJSON replay no longer blocks probing live kpacket on `:5556` when the game is not running
- `CommandClient` uses a 500 ms receive timeout instead of indefinite blocking

### Notes

- Pair with [kpacket2 v0.1.0-rc.1](https://github.com/poroburu/kpacket2/releases/tag/v0.1.0-rc.1) for live capture; wire protocol `kpacket.v1` unchanged
- WPF host requires Windows; CLI core libraries run on .NET 8 anywhere
- Opcode subset only — most packets appear as raw in monitor view
- Legacy KParser `.sdf` files and Lua kpacket (port 6666) are not supported

## [0.1.0] - 2026-06-16 (planned GA)

Planned first stable release. Scope matches the RC feature set above; GA will drop the `-rc.N` suffix and incorporate RC feedback.

[0.1.0-rc.2]: https://github.com/poroburu/kparser2/releases/tag/v0.1.0-rc.2
[0.1.0-rc.1]: https://github.com/poroburu/kparser2/releases/tag/v0.1.0-rc.1
[0.1.0]: https://github.com/poroburu/kparser2/releases/tag/v0.1.0
