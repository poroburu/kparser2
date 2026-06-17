# `.kparse2.json` report schema

Version: **1**

kparser2 exports session analytics as a portable JSON document. Legacy kparser `.sdf` files are **not** supported.

## Top-level shape

```json
{
  "meta": {
    "schema_version": 1,
    "title": "string",
    "zone": "string",
    "recorded_at": "ISO-8601",
    "kparser2_version": "string"
  },
  "combatants": [],
  "fights": [],
  "events": [],
  "chat": [],
  "loot": [],
  "itemUses": [],
  "experience": [],
  "summaries": {
    "offense_by_category": { "Melee": 1234 }
  }
}
```

## Fields

| Field | Description |
|---|---|
| `meta.schema_version` | Must be `1` for current kparser2 builds |
| `combatants` | Players, mobs, pets seen in the session |
| `fights` | Segmented battles (open on harm-to-mob, close on kill/idle/zone) |
| `events` | Classified combat interactions (harm, aid, death) |
| `chat` | Decoded chat messages |
| `loot` | Trophy / pool loot records |
| `itemUses` | Item use events (0x37) |
| `experience` | XP records from MsgBasic 0x29 and system chat |
| `summaries` | Pre-aggregated query totals for quick restore |

## CLI

```powershell
dotnet run --project kparser2.Cli -- analytics snapshot fixtures/sessions/combat_basic.ndjson --json
dotnet run --project kparser2.Cli -- export report fixtures/sessions/combat_basic.ndjson -o fight.kparse2.json
dotnet run --project kparser2.Cli -- import report fight.kparse2.json --validate
```

## Round-trip guarantee

Export → import must preserve interaction and fight counts. Offense totals should match within the same filter defaults.

## Future work

- `IReportPublisher` for horizonxilogs upload (not implemented)
- `ILegacyParseImporter` interface only — no default `.sdf` importer
