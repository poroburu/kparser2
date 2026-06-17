# Compatibility

Paired release tags for the kpacket2 + kparser2 stack:

| kparser2 | kpacket2 | Wire protocol |
|----------|----------|---------------|
| v0.1.0-rc.1 | v0.1.0-rc.1 | `kpacket.v1` |

## Version layers

- **Git tag** (`v0.1.0-rc.1`) — distribution and release notes; keep both repos on the same tag for RC testing.
- **Wire protocol** (`v1` in topics and JSON meta) — unchanged for this RC; a breaking topic/meta/port change would require `v2` in kpacket2 first.
- **Ashita plugin API** (`GetVersion()` = `1.0`) — Ashita convention; unrelated to git semver.
- **Report schema** (`.kparse2.json` `schema_version` = `1`) — kparser2 export format.

## Mismatched tags

Clients and plugins sharing wire `v1` usually interoperate across git tags, but RC validation should use the paired tags above.

## Live capture requirements

- [kpacket2](https://github.com/poroburu/kpacket2) loaded in Ashita: `/load kpacket`
- ZMQ endpoints: `tcp://localhost:5555` (packets), `tcp://localhost:5556` (commands)
- Legacy Lua kpacket on port **6666** is **not** supported
