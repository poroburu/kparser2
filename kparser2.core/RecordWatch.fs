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

    /// XiPackets / LSB `GP_GAME_LOGOUT_STATE`. Wire slot is 4 bytes; value is a uint8.
    /// 0x000B is also the zone-server handoff (ZONECHANGE=2), not only `/logout`.
    module LogoutState =
        let Logout = 1
        let ZoneChange = 2
        let Cancel = 4
        let Timeout = 8
        let GmLogout = 9

        let read (data: byte[]) =
            if isNull data || data.Length < 5 then
                None
            else
                Some(int data.[4])

        let endsCapture =
            function
            | 1
            | 8
            | 9 -> true
            | _ -> false

    /// True only for incoming 0x000B that actually leaves the character session.
    /// Zone change, mog house, and logout-cancel keep recording.
    let tryLogoutStop (packetId: uint16) (direction: PacketDirection) (data: byte[]) =
        if packetId <> 0x000Bus || direction <> PacketDirection.Incoming then
            None
        else
            match LogoutState.read data with
            | Some state when LogoutState.endsCapture state -> Some Logout
            | _ -> None
