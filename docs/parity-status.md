# Historical live parity snapshot

Snapshot: **2026-09-08**

This records the September 8 run only; it is not current readiness or backlog.
Use [report-oracle.md](report-oracle.md) for the current process and GitHub
issues #4/#9 for work and decisions. Later evidence does not retroactively
validate this window. Live packet bytes remain authoritative; do not edit
legacy kparser to make a comparison pass.

## Green

- kparser2 non-integration tests: **193 passed**.
- kparser2, legacy kparser, and kpacket2 builds succeeded.
- Live kpacket ingest received packets with no relay parse errors.
- An elevated dual capture completed successfully:
  - legacy kparser: **34 ChatLines**
  - kparser2: **415 packets**
  - both capture processes exited with code `0`

## Degraded but usable

The currently loaded kpacket release is the older RC build and does not
advertise `message_id_cursor`. Live records therefore use an explicit
`degraded` boundary. This supports exploratory QA without restarting the game,
but it does not prove an exact packet partition for synchronized evidence.

The cursor-capable kpacket2 build is staged locally. Deploy it only before a
future game launch; do not replace a DLL held by a running game.

## In progress

The elevated window generated these local, uncommitted artifacts:

```text
ffxi-captures/ndjson/oracle_20260908_173953.elevated.compare.json
```

The comparator reported:

- interactions: legacy `26`, kparser2 `24`
- chat: legacy `5`, kparser2 `2`

This is an unresolved live mismatch, not a clean parity result. The chat
projection also contains legacy formatter/system rows that are not part of
kparser2's incoming `0x17` projection, so the rows must be reconciled against
the raw capture before classifying the difference.

## Not yet validated

The desktop UI control endpoints were not observed during this run. The
elevated CLI capture validates the raw ChatLine stream, but it does not validate
the visual UI surfaces or the `rendering-only` observation path.

Do not promote this window to a fixture. The capture files remain local under
`ffxi-captures/` and are intentionally not committed.
