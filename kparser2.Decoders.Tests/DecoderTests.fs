namespace kparser2.Decoders.Tests

open kparser2.Decoders
open kparser2.Protocol
open Xunit

module DecoderTests =
    [<Fact>]
    let ``Chat0x17 decodes speaker and message`` () =
        let data = Fixtures.chatPacket "Alice" "Hello world" 0x00uy

        match Chat0x17.decode data with
        | None -> failwith "Expected chat decode"
        | Some chat ->
            Assert.Equal("Say", chat.Mode)
            Assert.Equal("Alice", chat.Speaker)
            Assert.Equal("Hello world", chat.Message)
            Assert.False chat.IsGm

    [<Fact>]
    let ``Chat0xB5 decodes outgoing say`` () =
        let data = Fixtures.outgoingChatPacket "Hello from me" 0x00uy

        match Chat0xB5.decode data with
        | None -> failwith "Expected outgoing chat decode"
        | Some chat ->
            Assert.Equal("Say", chat.Mode)
            Assert.Equal("", chat.Speaker)
            Assert.Equal("Hello from me", chat.Message)

    [<Fact>]
    let ``EntityRegistry tracks local player from login`` () =
        EntityRegistry.reset()

        let evt =
            { Topic = "test"
              Timestamp = 1UL
              Direction = PacketDirection.Incoming
              PacketType = "world_s2c"
              PacketId = 0x000Aus
              PacketName = "GP_SERV_COMMAND_LOGIN"
              Size = 148u
              Injected = false
              Blocked = false
              SessionUuid = "test"
              Version = "v1"
              MessageId = 1UL
              Data = Fixtures.loginPacket "Mychar" 0x12345u }

        EntityRegistry.observe evt
        Assert.Equal(Some "Mychar", EntityRegistry.localPlayerName())
        Assert.Equal("Mychar", EntityRegistry.resolveChatSpeaker "" 0x00B5us)

    [<Fact>]
    let ``EntityRegistry tracks player names from char update`` () =
        EntityRegistry.reset()

        let evt =
            { Topic = "test"
              Timestamp = 1UL
              Direction = PacketDirection.Incoming
              PacketType = "world_s2c"
              PacketId = 0x000Dus
              PacketName = "GP_SERV_COMMAND_CHAR_PC"
              Size = 106u
              Injected = false
              Blocked = false
              SessionUuid = "test"
              Version = "v1"
              MessageId = 1UL
              Data = Fixtures.charPcPacket "Bob" 0x54321u }

        EntityRegistry.observe evt
        Assert.Equal("Bob", EntityRegistry.formatEntity 0x54321u)

    [<Fact>]
    let ``DecoderRegistry routes outgoing chat opcode`` () =
        let evt =
            { Topic = "test"
              Timestamp = 1UL
              Direction = PacketDirection.Outgoing
              PacketType = "world_c2s"
              PacketId = 0x00B5us
              PacketName = "GP_CLI_COMMAND_CHAT_STD"
              Size = 32u
              Injected = false
              Blocked = false
              SessionUuid = "test"
              Version = "v1"
              MessageId = 2UL
              Data = Fixtures.outgoingChatPacket "testing say" 0x00uy }

        let result = DecoderRegistry.decode evt
        Assert.Contains(result.Events, function DecoderEvent.Chat c -> c.Message = "testing say" | _ -> false)

    [<Fact>]
    let ``Trophy0xD2 decodes found item`` () =
        let data = Fixtures.trophyListPacket 704 1 12345u

        match Trophy0xD2.decode data with
        | None -> failwith "Expected trophy list decode"
        | Some loot ->
            Assert.Equal(LootEventType.Found, loot.EventType)
            Assert.Equal(704, loot.ItemId)
            Assert.Equal(1, loot.Quantity)
            Assert.Equal(0, loot.PoolSlot)

    [<Fact>]
    let ``Trophy0xD3 decodes won lot`` () =
        let data = Fixtures.trophySolutionPacket 2 1 "Bob"

        match Trophy0xD3.decode data with
        | None -> failwith "Expected trophy solution decode"
        | Some loot ->
            Assert.Equal(LootEventType.Won, loot.EventType)
            Assert.Equal(2, loot.PoolSlot)
            Assert.Equal("Bob", loot.ActorName)

    [<Fact>]
    let ``Battle0x29 decodes battle message`` () =
        let data = Fixtures.battleMessagePacket 100u 200u 0x0033us

        match Battle0x29.decode data with
        | None -> failwith "Expected battle message decode"
        | Some message ->
            Assert.Equal(100u, message.CasterId)
            Assert.Equal(200u, message.TargetId)
            Assert.Equal(0x0033us, message.MessageNum)

    [<Fact>]
    let ``Battle0x28 decodes action packet`` () =
        let data = Fixtures.battle2Packet()

        match Battle0x28.decode data with
        | None -> failwith "Expected battle2 decode"
        | Some action ->
            Assert.Equal(1u, action.ActorId)
            Assert.NotEmpty action.Targets

    [<Fact>]
    let ``DecoderRegistry routes chat opcode`` () =
        let evt =
            { Topic = "test"
              Timestamp = 1UL
              Direction = PacketDirection.Incoming
              PacketType = "world_s2c"
              PacketId = 0x0017us
              PacketName = "GP_SERV_COMMAND_CHAT_STD"
              Size = 64u
              Injected = false
              Blocked = false
              SessionUuid = "test"
              Version = "v1"
              MessageId = 1UL
              Data = Fixtures.chatPacket "Bob" "meow" 0x04uy }

        let result = DecoderRegistry.decode evt
        Assert.Contains(result.Events, function DecoderEvent.Chat c -> c.Speaker = "Bob" | _ -> false)
