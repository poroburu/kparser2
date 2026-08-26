# kparser vs kparser2 inequalities

This scan’s parity is **bitstream → interaction**, not English chatlog reconstruction. XiPackets `0x0028` `BattleResult.message` is the id the client uses to pick a DAT formatter and print the line (`CXiSchStatus::PutMessage`). Live NDJSON plus that layout plus `msg.lua` / BtlMess names the event. `--assert-settled` proves we **classified** the id; the lookup proves the label. kparser RAM lines and `report fights` on constructed Motenten fixtures are not this camp.

Classify a dual-dump or live disagreement **before coding**. Do not edit kparser. Do not retarget kparser2 `HarmType` to kparser’s Damage/Enfeeble/Drain labels. Diff `parity.interactions` by **name**.

| Bucket | Meaning | Agent action |
|--------|---------|--------------|
| **kparser-only** | Chatlog / RAM artifacts kparser2 must not clone | Do not halt |
| **kparser2-missing** | Packets or TestParser semantics we still lack | Halt/implement if live bytes show it |
| **kparser2-extra** | Packet events kparser never had | Not a miss |
| **deferred** | Real, but not this scan | Skip-list (`--skip-code` / default deferred codes) |

## kparser-only

| Fact | Notes |
|------|--------|
| `RamReader` / English `MessageLine` | kparser scrapes the client chat log |
| Chatline ParseCodes ≠ `0x28 message` / `cmd_no` | Parallel taxonomies; kparser can be wrong |
| SQL CE `.sdf`, WinForms pixels | Not the CLI snapshot contract |
| Autoincrement entity ids | kparser2 uses packet UniqueNo |
| Pet-death pending queue | kparser snapshot v1 skips it |
| System lines vs `ChatMessages` | kparser `parity.chat` projects system; native `chat[]` may not |
| Constructed dual-dump agreement | `generate-fixtures.ps1` proves CLI agreement, not Horizon layout |
| kparser `HarmType` Damage/Enfeeble/Drain | kparser2 interaction harm is Melee/Ranged/Spell/… |
| `kparser.cli` fights/offense / RAM attach | Snapshot is chatline-file only. `RamReader` is WinForms. Do not add those dumps as a parity gate |

## kparser2-missing (implement from live bytes)

| Fact | Notes |
|------|--------|
| Empty ParseCodes *regions* | Bootstrap: one family/commit when `msg.lua` agrees |
| Unclassified live `0x28` finish `message` | `--assert-settled` `unclassified_message-*` |
| Unknown chat Kind | `unknown_kind-0xNN` — look up XiPackets `0x0017` |
| Start cmd classified as Harm | `start_as_harm-*` — decode corruption |
| Fourcc used as spell id | `fourcc_as_spell-*` — start `cmd_arg` only |

## kparser2-extra (not a miss)

| Fact | Notes |
|------|--------|
| Magic **start** `cmd_no` 8 | Skip; not damage |
| Interrupt `sp*` fourcc, same `cmd_no` | Incomplete, not Harm |
| `0xFD` auto-translate in Mes | VieweD encoding |
| Packet entity ids | Not kparser DataSet ids |
| Outgoing `0xB5` | kparser chatlines are incoming log |

## deferred

| Code | Why |
|------|-----|
| `unnamed_entities` | Often missing `0x00E` name@0x34, not a classifier bug |
| `melee_name_pairing` | Out of scan scope unless it is the ranked priority item |
| `party_alliance_filter` | `0x00DD` names party members (alliance flags unused). Offense/defense include every nearby Player/Pet/Fellow |
| `pet_owner_map` | Local jug only: `0x0068` owner=self + `0x00E` claimer=self. No owner→pet for alliance pets |

Append a row when a session discovers a *structural* fact. Do not “fix” kparser so a dump matches.
