namespace kparser2.Analytics.Tests

open System
open System.IO
open System.Threading.Channels
open Xunit
open kparser2.Core
open kparser2.Ingest
open kparser2.Protocol

type private TestPacketSource(events: PacketEvent list) =
    let channel = Channel.CreateUnbounded<PacketEvent>()

    do
        events |> List.iter (fun evt -> channel.Writer.TryWrite(evt) |> ignore)
        channel.Writer.TryComplete() |> ignore

    interface IPacketSource with
        member _.Packets = channel.Reader
        member _.WaitForCompletion() = ()
        member _.Dispose() = ()

module private LiveBoundary =
    let eventFor messageId =
        { Topic = "kpacket.v1.world.s2c"
          Timestamp = messageId
          Direction = PacketDirection.Incoming
          PacketType = "world"
          PacketId = 0x0017us
          PacketName = "GP_SERV_COMMAND_CHAT_STD"
          Size = 4u
          Injected = false
          Blocked = false
          MessageId = messageId
          SessionUuid = "session-a"
          Version = "test"
          Data = [| 0uy |] }

    let readAll (reader: ChannelReader<PacketEvent>) =
        let mutable result = []
        let mutable running = true

        while running do
            if reader.WaitToReadAsync().AsTask().GetAwaiter().GetResult() then
                let mutable evt = Unchecked.defaultof<PacketEvent>
                while reader.TryRead(&evt) do
                    result <- evt :: result
            else
                running <- false

        List.rev result

module LiveBoundaryTests =
    [<Fact>]
    let ``hello exposes the relay message boundary`` () =
        let response =
            """{"version":"1","session_uuid":"session-a","last_message_id":42,"capabilities":{"message_id_cursor":true}}"""

        let hello = ConnectionProbe.tryParseHello response

        Assert.True(hello.IsSome)
        Assert.Equal("session-a", hello.Value.session_uuid)
        Assert.Equal(42UL, hello.Value.last_message_id)
        Assert.True(ConnectionProbe.hasMessageCursor hello.Value)

    [<Fact>]
    let ``legacy hello without cursor capability is rejected`` () =
        let response =
            """{"version":"1","session_uuid":"session-a","last_message_id":0,"capabilities":{"echo":true}}"""

        let hello = ConnectionProbe.tryParseHello response

        Assert.True(hello.IsSome)
        Assert.False(ConnectionProbe.hasMessageCursor hello.Value)
        Assert.True(ConnectionProbe.boundaryCapabilityFromHello hello |> Result.isError)

    [<Fact>]
    let ``desktop source excludes the boundary and earlier messages`` () =
        let source =
            new DesktopPacketSource(
                (new TestPacketSource([ LiveBoundary.eventFor 41UL
                                        LiveBoundary.eventFor 42UL
                                        LiveBoundary.eventFor 43UL ])
                 :> IPacketSource),
                IO.Path.GetTempFileName(),
                false,
                "session-a",
                42UL,
                true,
                "exact",
                "exact",
                "")

        try
            let events = LiveBoundary.readAll (source :> IPacketSource).Packets
            Assert.Equal< uint64 list >([ 43UL ], events |> List.map (fun evt -> evt.MessageId))
            Assert.Equal(43UL, source.LastMessageId)
        finally
            (source :> IDisposable).Dispose()

    [<Fact>]
    let ``degraded source keeps the session live without claiming a cursor partition`` () =
        let source =
            new DesktopPacketSource(
                (new TestPacketSource([ LiveBoundary.eventFor 41UL
                                        LiveBoundary.eventFor 42UL ])
                 :> IPacketSource),
                IO.Path.GetTempFileName(),
                false,
                "session-a",
                42UL,
                false,
                "degraded",
                "unavailable",
                "legacy kpacket")

        try
            let events = LiveBoundary.readAll (source :> IPacketSource).Packets
            Assert.Equal< uint64 list >([ 41UL; 42UL ], events |> List.map (fun evt -> evt.MessageId))
        finally
            (source :> IDisposable).Dispose()

    [<Fact>]
    let ``capture header preserves boundary quality and reason`` () =
        let path = Path.GetTempFileName()

        try
            use writer = new StreamWriter(path)
            Ndjson.writeSessionHeaderWithBoundary
                writer
                None
                123L
                "session-a"
                42UL
                "degraded"
                "unavailable"
                "legacy kpacket"

            writer.Flush()
            writer.Dispose()
            let header = Ndjson.tryReadSessionHeader path

            Assert.True(header.IsSome)
            Assert.Equal("degraded", header.Value.boundary_mode)
            Assert.Equal("unavailable", header.Value.boundary_quality)
            Assert.Equal("legacy kpacket", header.Value.boundary_reason)
            Assert.Equal("session-a", header.Value.boundary_session_uuid)
            Assert.Equal(42UL, header.Value.boundary_message_id)
        finally
            File.Delete(path)
