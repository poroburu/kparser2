namespace kparser2.Analytics.Tests

open kparser2.Analytics
open kparser2.Decoders
open kparser2.Decoders.Tests
open kparser2.Protocol

module InteractionTestHelpers =
    let resetEntities () =
        EntityRegistry.reset ()
        InteractionBuilder.reset ()

    let registerLocalPlayer (entityId: uint32) (name: string) =
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
              Data = Fixtures.groupAttrPacket entityId 140us }

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
              Data = Fixtures.charPcPacket name entityId }

    let registerMob (entityId: uint32) (name: string) =
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

    let registerPartyMember (entityId: uint32) (name: string) =
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
              Data = Fixtures.partyMemberPacket entityId name }

    let buildInteraction
        (actorId: uint32)
        (targetId: uint32)
        (commandNo: int)
        (messageId: int)
        (miss: int)
        (value: int)
        =
        match Battle0x28.decode (Fixtures.combatActionPacket actorId targetId commandNo value messageId miss) with
        | None -> failwith "Expected battle action decode"
        | Some action ->
            InteractionBuilder.fromCombatAction 1000L None action |> List.head
