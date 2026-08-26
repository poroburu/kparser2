namespace kparser2.Core

open System
open kparser2.Protocol

/// When to close a live NDJSON capture (game DC / plugin reload / idle stall).
module RecordWatch =
    type StopReason =
        | PluginOffline
        | SessionChanged of previous: string * next: string
        | PacketStall
        | Logout

    let label reason =
        match reason with
        | PluginOffline -> "kpacket offline"
        | SessionChanged(prev, next) -> $"session changed ({prev} -> {next})"
        | PacketStall -> "packet stall"
        | Logout -> "logout (0x000B)"

    let tryPluginStop reachable =
        if reachable then
            None
        else
            Some PluginOffline

    let trySessionStop (initialUuid: string) (currentUuid: string) =
        if String.IsNullOrWhiteSpace initialUuid || String.IsNullOrWhiteSpace currentUuid then
            None
        elif initialUuid <> currentUuid then
            Some(SessionChanged(initialUuid, currentUuid))
        else
            None

    /// Stall only after at least one packet was written. idleMs <= 0 disables.
    let tryStallStop
        (idleMs: int)
        (packetsWritten: int)
        (published: int64)
        (lastPublished: int64)
        (lastProgressUtc: DateTime)
        (now: DateTime)
        =
        if idleMs <= 0 || packetsWritten <= 0 then
            None
        elif published <> lastPublished then
            None
        elif (now - lastProgressUtc).TotalMilliseconds >= float idleMs then
            Some PacketStall
        else
            None

    let isLogoutPacket (packetId: uint16) (direction: PacketDirection) =
        packetId = 0x000Bus && direction = PacketDirection.Incoming
