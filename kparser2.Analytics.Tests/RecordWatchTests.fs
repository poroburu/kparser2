namespace kparser2.Analytics.Tests

open System
open Xunit
open kparser2.Core
open kparser2.Protocol

module RecordWatchTests =
    [<Fact>]
    let ``plugin stop when hello is unreachable`` () =
        Assert.Equal(Some RecordWatch.StopReason.PluginOffline, RecordWatch.tryPluginStop false)
        Assert.Equal(None, RecordWatch.tryPluginStop true)

    [<Fact>]
    let ``session stop ignores empty uuids and fires on change`` () =
        Assert.Equal(None, RecordWatch.trySessionStop "" "abc")
        Assert.Equal(None, RecordWatch.trySessionStop "abc" "")
        Assert.Equal(None, RecordWatch.trySessionStop "abc" "abc")

        match RecordWatch.trySessionStop "aaa" "bbb" with
        | Some(RecordWatch.StopReason.SessionChanged(prev, next)) ->
            Assert.Equal("aaa", prev)
            Assert.Equal("bbb", next)
        | other -> failwithf "unexpected %A" other

    [<Fact>]
    let ``stall stop waits for written packets and idle window`` () =
        let t0 = DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        Assert.Equal(None, RecordWatch.tryStallStop 180_000 0 10L 10L t0 (t0.AddMinutes 5.0))
        Assert.Equal(None, RecordWatch.tryStallStop 0 5 10L 10L t0 (t0.AddMinutes 5.0))
        Assert.Equal(None, RecordWatch.tryStallStop 180_000 5 11L 10L t0 (t0.AddMinutes 5.0))
        Assert.Equal(None, RecordWatch.tryStallStop 180_000 5 10L 10L t0 (t0.AddSeconds 10.0))
        Assert.Equal(Some RecordWatch.StopReason.PacketStall, RecordWatch.tryStallStop 180_000 5 10L 10L t0 (t0.AddMinutes 3.0))

    let private logoutPacket (state: byte) (nextZoneIp: uint32) =
        let data = Array.zeroCreate 28
        data.[0] <- 0x0Buy
        data.[1] <- 0x0Euy
        data.[4] <- state
        BitConverter.GetBytes(nextZoneIp).CopyTo(data, 8)
        data

    [<Fact>]
    let ``zone change 0x000B does not stop the capture`` () =
        // Live Horizon slice: LogoutState=2, Iwasaki = next-zone IPP.
        let zone = logoutPacket 2uy 1923044674u
        Assert.Equal(Some RecordWatch.LogoutState.ZoneChange, RecordWatch.LogoutState.read zone)
        Assert.Equal(None, RecordWatch.tryLogoutStop 0x000Bus PacketDirection.Incoming zone)
        Assert.Equal(None, RecordWatch.tryLogoutStop 0x000Bus PacketDirection.Incoming (logoutPacket 4uy 0u))

    [<Fact>]
    let ``logout 0x000B stops only for session-ending states`` () =
        Assert.Equal(
            Some RecordWatch.StopReason.Logout,
            RecordWatch.tryLogoutStop 0x000Bus PacketDirection.Incoming (logoutPacket 1uy 0u)
        )
        Assert.Equal(
            Some RecordWatch.StopReason.Logout,
            RecordWatch.tryLogoutStop 0x000Bus PacketDirection.Incoming (logoutPacket 8uy 0u)
        )
        Assert.Equal(
            Some RecordWatch.StopReason.Logout,
            RecordWatch.tryLogoutStop 0x000Bus PacketDirection.Incoming (logoutPacket 9uy 0u)
        )
        Assert.Equal(None, RecordWatch.tryLogoutStop 0x000Bus PacketDirection.Outgoing (logoutPacket 1uy 0u))
        Assert.Equal(None, RecordWatch.tryLogoutStop 0x0017us PacketDirection.Incoming (logoutPacket 1uy 0u))
        Assert.Equal(None, RecordWatch.tryLogoutStop 0x000Bus PacketDirection.Incoming Array.empty)
