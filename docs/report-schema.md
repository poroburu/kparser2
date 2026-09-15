# `.kparse2.json` report schema

Version: **2**

kparser2 exports session analytics as a portable JSON document. Legacy kparser `.sdf` files are **not** supported.

## Top-level shape

```json
{
  "Meta": {
    "schema_version": 2,
    "title": "string",
    "zone": "string",
    "recorded_at": "ISO-8601",
    "kparser2_version": "string"
  },
  "SessionStartMs": 0,
  "Combatants": [],
  "Fights": [],
  "Events": [],
  "Chat": [],
  "Loot": [],
  "ItemUses": [],
  "experience": [],
  "Summaries": {
    "offense_by_category": { "Melee": 1234 },
    "defense_by_category": {},
    "recovery_by_action": {},
    "chat_by_mode": {},
    "chat_by_speaker": {},
    "experience_by_actor": {},
    "loot_by_item": {}
  }
}
```

## Fields

| Field | Description |
|---|---|
| `Meta.schema_version` | Must be `2` for current kparser2 exports; version `1` remains import-compatible |
| `SessionStartMs` | UTC Unix milliseconds for the source session, when available |
| `Combatants` | Players, mobs, pets seen in the session |
| `Fights` | Segmented battles (open on harm-to-mob, close on kill/idle/zone) |
| `Events` | Classified combat interactions (harm, aid, death) |
| `Chat` | Decoded chat messages |
| `Loot` | Trophy / pool loot records |
| `ItemUses` | Item use events (0x37) |
| `experience` | XP records from MsgBasic 0x29 and system chat; optional in version 1 reports |
| `Summaries` | Pre-aggregated offense, defense, recovery, chat, experience, and loot totals for quick restore |

## CLI

```powershell
dotnet run --project kparser2.Cli -- analytics snapshot fixtures/sessions/combat_basic.ndjson --json
dotnet run --project kparser2.Cli -- export report fixtures/sessions/combat_basic.ndjson -o fight.kparse2.json
dotnet run --project kparser2.Cli -- import report fight.kparse2.json --validate
```

## Round-trip guarantee

Export → import must preserve interaction and fight counts. Offense totals should match within the same filter defaults.

## Parity projection

The analytics CLI also exposes a separate, stable parity projection:

```powershell
dotnet run --project kparser2.Cli -- analytics snapshot `
  fixtures/sessions/chat_yell.ndjson --parity -o kparser2-parity.json
```

This projection contains only name-keyed interaction and incoming chat rows.
It is compared with legacy kparser's `parity.interactions` and `parity.chat`
by `scripts/compare-parity.ps1`; IDs, timestamps, and packet ordering are not
part of the comparison contract.

## Future work

- `IReportPublisher` for horizonxilogs upload (not implemented)
- `ILegacyParseImporter` interface only — no default `.sdf` importer
