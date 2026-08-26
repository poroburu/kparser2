# Metadata gap fillers

Packet-native fields win. A filler is allowed only for a **named** gap until a packet source is proven. When the packet path is covered, remove the filler and add a test that fails if it is used.

| Id | Why packets can be insufficient | Current filler | Deprecate when |
|----|---------------------------------|----------------|----------------|
| `local-player-name` | Slice may lack `0x00A` / `0x00D`; `0x00DF` has id but not the name | kpacket `hello.player_name`; NDJSON session header `player_name`; S2C `0x0037` UniqueNo@36 (local status, not C2S item-use); `EntityRegistry.registerLocalPlayerName` (pending name is visible to `localPlayerName` before UniqueNo) | Login `0x00A` name@132, PC `0x00D` name when `SendFlg.Name`, or say-copy `0x18`/`0x19` on the same capture |
| `self-chat-speaker` | Nameless S2C kinds (`0x0D`–`0x10`, `0x1C`, `0x1F`) and C2S `0xB5` carry empty `sName`; speaker is the local player | Same as `local-player-name`, then `SessionStore` backfill / `resolveChatForSnapshot` | Local name is known from packets on that capture **before** treating empty speaker as a settled gap |
| `kparser-parity-chat-speaker` | Tiny replay slices with no login/hello at all | Optional kparser `parity.chat[].speaker` sidecar aligned by time/body — **read-only**; never edit kparser | A packet-native name exists (do not add sidecar ingest unless this row is still required) |

**Not fillers**

- Live `0x28` `message` — use LandSandBoat `scripts/enum/msg.lua` (`xi.msg.basic`) after the id appears in the capture
- Yell Kind `0x1A` — XiPackets `0x0017`
- Magic start `cmd_no` 8 fourcc (`cawh` / `cabl` / …) — not a spell id; finish `cmd_no` 4 `cmd_arg` is the spell id

See [parity-inequalities.md](parity-inequalities.md) before treating a kparser dual-dump mismatch as a missing filler.
