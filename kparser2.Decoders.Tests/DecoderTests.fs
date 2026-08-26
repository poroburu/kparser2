namespace kparser2.Decoders.Tests

open kparser2.Decoders
open kparser2.Protocol
open Xunit

[<Collection("EntityRegistry")>]
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
    let ``Chat0x17 yell carries zone in Data and Kind 0x1A`` () =
        let data = Fixtures.chatPacketWithZone "Wish" "SMN or WHM LFG Sagelord Elimination" 0x1Auy 50us

        match Chat0x17.decode data with
        | None -> failwith "Expected yell decode"
        | Some chat ->
            Assert.Equal("Yell", chat.Mode)
            Assert.Equal(0x1A, chat.ModeId)
            Assert.Equal("Wish", chat.Speaker)
            Assert.Equal("SMN or WHM LFG Sagelord Elimination", chat.Message)
            Assert.Equal(Some 50, chat.ZoneId)

    [<Fact>]
    let ``Chat0x17 replaces 0xFD auto-translate tokens`` () =
        let token = [| 0xFDuy; 0x02uy; 0x02uy; 0x12uy; 0x06uy; 0xFDuy |]
        let suffix = System.Text.Encoding.ASCII.GetBytes(" /t")
        let data = Fixtures.chatPacketBytes "Alastar" (Array.append token suffix) 0x1Auy 50us

        match Chat0x17.decode data with
        | None -> failwith "Expected auto-translate decode"
        | Some chat ->
            Assert.Equal("Yell", chat.Mode)
            Assert.Equal("Alastar", chat.Speaker)
            Assert.Equal("[02021206] /t", chat.Message)

    [<Fact>]
    let ``ChatCommon maps LS3 and Standard kinds from XiPackets`` () =
        Assert.Equal("Linkshell3", ChatCommon.modeLabel 0x1E)
        Assert.Equal("Linkshell3", ChatCommon.modeLabel 0x1F)
        Assert.Equal("Standard", ChatCommon.modeLabel 0x20)
        Assert.Equal("Unity", ChatCommon.modeLabel 0x21)
        Assert.True(ChatCommon.isNamelessSelfKind 0x1F)
        Assert.False(ChatCommon.isGmAttr 0x08)
        Assert.True(ChatCommon.isGmAttr 0x01)

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
        Assert.Equal("Mychar", EntityRegistry.resolveChatSpeaker "" 0x00B5us 0x00)

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
    let ``EntityRegistry tracks mob names from npc update`` () =
        EntityRegistry.reset()

        let evt =
            { Topic = "test"
              Timestamp = 1UL
              Direction = PacketDirection.Incoming
              PacketType = "world_s2c"
              PacketId = 0x000Eus
              PacketName = "GP_SERV_COMMAND_CHAR_NPC"
              Size = 68u
              Injected = false
              Blocked = false
              SessionUuid = "test"
              Version = "v1"
              MessageId = 1UL
              Data = Fixtures.npcUpdatePacket "Kalamainu" 0x108A5A5u }

        EntityRegistry.observe evt
        Assert.Equal("Kalamainu", EntityRegistry.formatEntity 0x108A5A5u)
        Assert.Equal(Some EntityRegistry.EntityKind.Mob, EntityRegistry.tryGetEntityKind 0x108A5A5u)

    [<Fact>]
    let ``EntityRegistry sets local player from group attr`` () =
        EntityRegistry.reset()

        let evt =
            { Topic = "test"
              Timestamp = 1UL
              Direction = PacketDirection.Incoming
              PacketType = "world_s2c"
              PacketId = 0x00DFus
              PacketName = "GP_SERV_COMMAND_GROUP_ATTR"
              Size = 40u
              Injected = false
              Blocked = false
              SessionUuid = "test"
              Version = "v1"
              MessageId = 1UL
              Data = Fixtures.groupAttrPacket 0x950F5u 140us }

        EntityRegistry.observe evt
        Assert.Equal(Some 0x950F5u, EntityRegistry.tryLocalPlayerId())
        Assert.Equal(Some 140, EntityRegistry.tryGetZoneId())

    [<Fact>]
    let ``EntityRegistry ignores npc update without name flag`` () =
        EntityRegistry.reset()

        let data = Fixtures.npcUpdatePacket "HiddenMob" 0x99999u
        data.[10] <- 0x01uy

        let evt =
            { Topic = "test"
              Timestamp = 1UL
              Direction = PacketDirection.Incoming
              PacketType = "world_s2c"
              PacketId = 0x000Eus
              PacketName = "GP_SERV_COMMAND_CHAR_NPC"
              Size = uint32 data.Length
              Injected = false
              Blocked = false
              SessionUuid = "test"
              Version = "v1"
              MessageId = 1UL
              Data = data }

        EntityRegistry.observe evt
        Assert.Equal("Entity 629145", EntityRegistry.formatEntity 0x99999u)

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
        let data = Fixtures.battleMessagePacketSimple 100u 200u 0x0033us

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

    [<Fact>]
    let ``resolveChatSpeaker uses local player for outgoing say`` () =
        EntityRegistry.reset()

        let dfEvt =
            { Topic = "test"
              Timestamp = 1UL
              Direction = PacketDirection.Incoming
              PacketType = "world_s2c"
              PacketId = 0x00DFus
              PacketName = "GP_SERV_COMMAND_GROUP_ATTR"
              Size = 40u
              Injected = false
              Blocked = false
              SessionUuid = "test"
              Version = "v1"
              MessageId = 1UL
              Data = Fixtures.groupAttrPacket 0x950F5u 140us }

        EntityRegistry.observe dfEvt
        Assert.Equal("Unknown", EntityRegistry.resolveChatSpeaker "" 0x00B5us 0x00)

        let tellChat =
            { Mode = "Tell"
              ModeId = 0x03
              IsGm = false
              Speaker = "Poroburu"
              Message = ">>Poroburu hello"
              ZoneId = None }

        EntityRegistry.observeChatBootstrap tellChat 0x0017us
        Assert.Equal(Some "Poroburu", EntityRegistry.localPlayerName())
        Assert.Equal("Poroburu", EntityRegistry.resolveChatSpeaker "" 0x00B5us 0x00)

    [<Fact>]
    let ``resolveChatSpeaker uses local player for nameless say echo`` () =
        EntityRegistry.reset()

        let loginEvt =
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
              Data = Fixtures.loginPacket "Poroburu" 0x950F5u }

        EntityRegistry.observe loginEvt
        Assert.Equal("Poroburu", EntityRegistry.resolveChatSpeaker "" 0x0017us 0x0D)

    [<Fact>]
    let ``char pc update registers local player name after group attr`` () =
        EntityRegistry.reset()
        EntityRegistry.observe
            { Topic = "test"
              Timestamp = 1UL
              Direction = PacketDirection.Incoming
              PacketType = "world_s2c"
              PacketId = 0x00DFus
              PacketName = "GP_SERV_COMMAND_GROUP_ATTR"
              Size = 40u
              Injected = false
              Blocked = false
              SessionUuid = "test"
              Version = "v1"
              MessageId = 1UL
              Data = Fixtures.groupAttrPacket 0x950F5u 140us }

        EntityRegistry.observe
            { Topic = "test"
              Timestamp = 2UL
              Direction = PacketDirection.Incoming
              PacketType = "world_s2c"
              PacketId = 0x000Dus
              PacketName = "GP_SERV_COMMAND_CHAR_PC"
              Size = 106u
              Injected = false
              Blocked = false
              SessionUuid = "test"
              Version = "v1"
              MessageId = 2UL
              Data = Fixtures.charPcPacket "Poroburu" 0x950F5u }

        Assert.Equal(Some "Poroburu", EntityRegistry.localPlayerName())
        Assert.Equal("Poroburu", EntityRegistry.resolveChatSpeaker "" 0x00B5us 0x00)

    [<Fact>]
    let ``char pc update without name flag still registers local player name`` () =
        EntityRegistry.reset()
        EntityRegistry.observe
            { Topic = "test"
              Timestamp = 1UL
              Direction = PacketDirection.Incoming
              PacketType = "world_s2c"
              PacketId = 0x00DFus
              PacketName = "GP_SERV_COMMAND_GROUP_ATTR"
              Size = 40u
              Injected = false
              Blocked = false
              SessionUuid = "test"
              Version = "v1"
              MessageId = 1UL
              Data = Fixtures.groupAttrPacket 0x950F5u 140us }

        EntityRegistry.observe
            { Topic = "test"
              Timestamp = 2UL
              Direction = PacketDirection.Incoming
              PacketType = "world_s2c"
              PacketId = 0x000Dus
              PacketName = "GP_SERV_COMMAND_CHAR_PC"
              Size = 106u
              Injected = false
              Blocked = false
              SessionUuid = "test"
              Version = "v1"
              MessageId = 2UL
              Data = Fixtures.charPcPacketNoNameFlag "Poroburu" 0x950F5u }

        Assert.Equal(Some "Poroburu", EntityRegistry.localPlayerName())

    [<Fact>]
    let ``packetviewer sized char pc registers local player name`` () =
        EntityRegistry.reset()
        EntityRegistry.observe
            { Topic = "test"
              Timestamp = 1UL
              Direction = PacketDirection.Incoming
              PacketType = "world_s2c"
              PacketId = 0x00DFus
              PacketName = "GP_SERV_COMMAND_GROUP_ATTR"
              Size = 40u
              Injected = false
              Blocked = false
              SessionUuid = "test"
              Version = "v1"
              MessageId = 1UL
              Data = Fixtures.groupAttrPacket 0x950F5u 140us }

        EntityRegistry.observe
            { Topic = "test"
              Timestamp = 2UL
              Direction = PacketDirection.Incoming
              PacketType = "world_s2c"
              PacketId = 0x000Dus
              PacketName = "GP_SERV_COMMAND_CHAR_PC"
              Size = 100u
              Injected = false
              Blocked = false
              SessionUuid = "test"
              Version = "v1"
              MessageId = 2UL
              Data = Fixtures.charPcPacketPvSized "Nowakii" 0x950F5u }

        Assert.Equal(Some "Nowakii", EntityRegistry.localPlayerName())

    [<Fact>]
    let ``registerLocalPlayerName applies after group attr when id was unknown`` () =
        EntityRegistry.reset()
        EntityRegistry.registerLocalPlayerName "Poroburu"

        EntityRegistry.observe
            { Topic = "test"
              Timestamp = 1UL
              Direction = PacketDirection.Incoming
              PacketType = "world_s2c"
              PacketId = 0x00DFus
              PacketName = "GP_SERV_COMMAND_GROUP_ATTR"
              Size = 40u
              Injected = false
              Blocked = false
              SessionUuid = "test"
              Version = "v1"
              MessageId = 1UL
              Data = Fixtures.groupAttrPacket 0x950F5u 140us }

        Assert.Equal(Some "Poroburu", EntityRegistry.localPlayerName())

    [<Fact>]
    let ``incoming 0x0037 server status UniqueNo is the local player`` () =
        EntityRegistry.reset()
        EntityRegistry.registerLocalPlayerName "Porobururu"

        EntityRegistry.observe
            { Topic = "test"
              Timestamp = 1UL
              Direction = PacketDirection.Incoming
              PacketType = "world_s2c"
              PacketId = 0x0037us
              PacketName = "GP_SERV_COMMAND_SERVERSTATUS"
              Size = 96u
              Injected = false
              Blocked = false
              SessionUuid = "test"
              Version = "v1"
              MessageId = 1UL
              Data = Fixtures.serverStatusPacket 20149u }

        Assert.Equal(Some 20149u, EntityRegistry.tryLocalPlayerId())
        Assert.Equal(Some "Porobururu", EntityRegistry.localPlayerName())
        Assert.Equal("Porobururu", EntityRegistry.formatEntity 20149u)

    [<Fact>]
    let ``outgoing 0x0037 item-use is not treated as local player status`` () =
        EntityRegistry.reset()

        EntityRegistry.observe
            { Topic = "test"
              Timestamp = 1UL
              Direction = PacketDirection.Outgoing
              PacketType = "world_c2s"
              PacketId = 0x0037us
              PacketName = "GP_CLI_COMMAND_ITEM_USE"
              Size = 20u
              Injected = false
              Blocked = false
              SessionUuid = "test"
              Version = "v1"
              MessageId = 1UL
              Data = Fixtures.serverStatusPacket 20149u }

        Assert.Equal(None, EntityRegistry.tryLocalPlayerId())

    [<Fact>]
    let ``loot roll registers local player name from 0xD3`` () =
        EntityRegistry.reset()

        EntityRegistry.observe
            { Topic = "test"
              Timestamp = 1UL
              Direction = PacketDirection.Incoming
              PacketType = "world_s2c"
              PacketId = 0x00DFus
              PacketName = "GP_SERV_COMMAND_GROUP_ATTR"
              Size = 40u
              Injected = false
              Blocked = false
              SessionUuid = "test"
              Version = "v1"
              MessageId = 1UL
              Data = Fixtures.groupAttrPacket 5485u 0us }

        EntityRegistry.observe
            { Topic = "test"
              Timestamp = 2UL
              Direction = PacketDirection.Incoming
              PacketType = "world_s2c"
              PacketId = 0x00D3us
              PacketName = "GP_SERV_COMMAND_TROPHY_SOLUTION"
              Size = 60u
              Injected = false
              Blocked = false
              SessionUuid = "test"
              Version = "v1"
              MessageId = 2UL
              Data = Fixtures.trophySolutionPacketWithIds 5485u 5485u "Poroburu" 1 0 }

        Assert.Equal(Some "Poroburu", EntityRegistry.localPlayerName())

    [<Fact>]
    let ``pet status labels prey target from 0x68`` () =
        EntityRegistry.reset()

        EntityRegistry.observe
            { Topic = "test"
              Timestamp = 1UL
              Direction = PacketDirection.Incoming
              PacketType = "world_s2c"
              PacketId = 0x00DFus
              PacketName = "GP_SERV_COMMAND_GROUP_ATTR"
              Size = 40u
              Injected = false
              Blocked = false
              SessionUuid = "test"
              Version = "v1"
              MessageId = 1UL
              Data = Fixtures.groupAttrPacket 5485u 0us }

        EntityRegistry.observe
            { Topic = "test"
              Timestamp = 2UL
              Direction = PacketDirection.Incoming
              PacketType = "world_s2c"
              PacketId = 0x0068us
              PacketName = "GP_SERV_COMMAND_PET_STATUS"
              Size = 44u
              Injected = false
              Blocked = false
              SessionUuid = "test"
              Version = "v1"
              MessageId = 2UL
              Data = Fixtures.petStatusPacket 5485u 99999u "Snoll" }

        Assert.Equal("Snoll", EntityRegistry.formatEntity 99999u)
        Assert.Equal(Some EntityRegistry.EntityKind.Mob, EntityRegistry.tryGetEntityKind 99999u)

    [<Fact>]
    let ``jug pet status does not rename prey from 0x68`` () =
        EntityRegistry.reset()

        EntityRegistry.observe
            { Topic = "test"
              Timestamp = 1UL
              Direction = PacketDirection.Incoming
              PacketType = "world_s2c"
              PacketId = 0x00DFus
              PacketName = "GP_SERV_COMMAND_GROUP_ATTR"
              Size = 40u
              Injected = false
              Blocked = false
              SessionUuid = "test"
              Version = "v1"
              MessageId = 1UL
              Data = Fixtures.groupAttrPacket 5485u 0us }

        EntityRegistry.observe
            { Topic = "test"
              Timestamp = 2UL
              Direction = PacketDirection.Incoming
              PacketType = "world_s2c"
              PacketId = 0x0068us
              PacketName = "GP_SERV_COMMAND_PET_STATUS"
              Size = 44u
              Injected = false
              Blocked = false
              SessionUuid = "test"
              Version = "v1"
              MessageId = 2UL
              Data = Fixtures.petStatusPacket 5485u 0u "LullabyMelodia" }

        EntityRegistry.observe
            { Topic = "test"
              Timestamp = 3UL
              Direction = PacketDirection.Incoming
              PacketType = "world_s2c"
              PacketId = 0x0068us
              PacketName = "GP_SERV_COMMAND_PET_STATUS"
              Size = 44u
              Injected = false
              Blocked = false
              SessionUuid = "test"
              Version = "v1"
              MessageId = 3UL
              Data = Fixtures.petStatusPacket 5485u 0x99999u "LullabyMelodia" }

        Assert.Equal(Some "LullabyMelodia", EntityRegistry.tryLocalJugPetName ())
        Assert.Equal("Entity 629145", EntityRegistry.formatEntity 0x99999u)
        Assert.True(EntityRegistry.tryGetEntityKind 99999u |> Option.isNone)

    [<Fact>]
    let ``party member update registers player name from 0xDD`` () =
        EntityRegistry.reset()

        EntityRegistry.observe
            { Topic = "test"
              Timestamp = 1UL
              Direction = PacketDirection.Incoming
              PacketType = "world_s2c"
              PacketId = 0x00DDus
              PacketName = "GP_SERV_COMMAND_PARTY_MEMBER"
              Size = 54u
              Injected = false
              Blocked = false
              SessionUuid = "test"
              Version = "v1"
              MessageId = 1UL
              Data = Fixtures.partyMemberPacket 12345u "Alice" }

        Assert.Equal("Alice", EntityRegistry.formatEntity 12345u)
        Assert.Equal(Some EntityRegistry.EntityKind.Player, EntityRegistry.tryGetEntityKind 12345u)

    [<Theory>]
    [<InlineData("Mandragora", 0x26B9u)>]
    [<InlineData("Greater Colibri", 0x26BAu)>]
    [<InlineData("Lamia No.9", 0x26BBu)>]
    [<InlineData("Ga'Dho Softstep", 0x26BDu)>]
    [<InlineData("Maymun 53", 0x26BEu)>]
    [<InlineData("Fantoccini", 0x26BFu)>]
    [<InlineData("Moo Ouzi the Swi", 0x26BCu)>]
    [<InlineData("Tzee Xicu's Elem", 0x26C0u)>]
    [<InlineData("Mamool Ja's Wyve", 0x26C1u)>]
    [<InlineData("Lamia's Elementa", 0x26C2u)>]
    let ``mob name parity TestMobNames`` (name: string, entityId: uint32) =
        EntityRegistry.reset()

        EntityRegistry.observe
            { Topic = "test"
              Timestamp = 1UL
              Direction = PacketDirection.Incoming
              PacketType = "world_s2c"
              PacketId = 0x000Eus
              PacketName = "GP_SERV_COMMAND_CHAR_NPC"
              Size = 68u
              Injected = false
              Blocked = false
              SessionUuid = "test"
              Version = "v1"
              MessageId = 1UL
              Data = Fixtures.npcUpdatePacket name entityId }

        Assert.Equal(name, EntityRegistry.formatEntity entityId)
