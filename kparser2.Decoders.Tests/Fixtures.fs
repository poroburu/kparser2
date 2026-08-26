namespace kparser2.Decoders.Tests

open System
open System.Text

module Fixtures =
    let private padName (name: string) =
        let bytes = Array.create 15 0uy
        let encoded = Encoding.UTF8.GetBytes(name)

        Array.Copy(
            encoded,
            0,
            bytes,
            0,
            min 15 encoded.Length
        )

        bytes

    let chatPacketBytes (speaker: string) (messageBytes: byte[]) (kind: byte) (zoneId: uint16) =
        let header = [| 0x20uy; 0uy; 0x17uy; 0uy |]
        let nameBytes = padName speaker
        let zone = BitConverter.GetBytes(zoneId)
        Array.concat [ header; [| kind; 0uy; zone.[0]; zone.[1] |]; nameBytes; messageBytes; [| 0uy |] ]

    let chatPacketWithZone (speaker: string) (message: string) (kind: byte) (zoneId: uint16) =
        chatPacketBytes speaker (Encoding.UTF8.GetBytes(message)) kind zoneId

    let chatPacket (speaker: string) (message: string) (kind: byte) =
        chatPacketWithZone speaker message kind 0us

    let outgoingChatPacket (message: string) (kind: byte) =
        let header = [| 0x10uy; 0uy; 0xB5uy; 0uy |]
        let messageBytes = Encoding.UTF8.GetBytes(message)
        Array.concat [ header; [| kind; 0uy |]; messageBytes; [| 0uy |] ]

    let loginPacket (name: string) (entityId: uint32) =
        let data = Array.create 148 0uy
        data.[2] <- 0x0Auy
        BitConverter.GetBytes(entityId).CopyTo(data, 4)

        let nameBytes = Encoding.UTF8.GetBytes(name)
        Array.Copy(nameBytes, 0, data, 132, min 16 nameBytes.Length)
        data

    let charPcPacket (name: string) (entityId: uint32) =
        let data = Array.create 106 0uy
        data.[2] <- 0x0Duy
        data.[10] <- 0x08uy
        BitConverter.GetBytes(entityId).CopyTo(data, 4)

        let nameBytes = Encoding.UTF8.GetBytes(name)
        Array.Copy(nameBytes, 0, data, 90, min 16 nameBytes.Length)
        data

    let charPcPacketNoNameFlag (name: string) (entityId: uint32) =
        let data = Array.create 106 0uy
        data.[2] <- 0x0Duy
        data.[10] <- 0x01uy
        BitConverter.GetBytes(entityId).CopyTo(data, 4)

        let nameBytes = Encoding.UTF8.GetBytes(name)
        Array.Copy(nameBytes, 0, data, 90, min 16 nameBytes.Length)
        data

    /// PacketViewer captures often truncate 0x000D to ~100 bytes while still carrying the name @90.
    let charPcPacketPvSized (name: string) (entityId: uint32) =
        let data = Array.create 100 0uy
        data.[2] <- 0x0Duy
        data.[10] <- 0x08uy
        BitConverter.GetBytes(entityId).CopyTo(data, 4)

        let nameBytes = Encoding.UTF8.GetBytes(name)
        Array.Copy(nameBytes, 0, data, 90, min 10 nameBytes.Length)
        data

    let npcUpdatePacket (name: string) (entityId: uint32) =
        let data = Array.create 68 0uy
        data.[2] <- 0x0Euy
        data.[10] <- 0x08uy
        BitConverter.GetBytes(entityId).CopyTo(data, 4)

        let nameBytes = Encoding.UTF8.GetBytes(name)
        Array.Copy(nameBytes, 0, data, 52, min 16 nameBytes.Length)
        data

    let groupAttrPacket (entityId: uint32) (zoneId: uint16) =
        let data = Array.create 40 0uy
        data.[2] <- 0xDFuy
        BitConverter.GetBytes(entityId).CopyTo(data, 4)
        BitConverter.GetBytes(zoneId).CopyTo(data, 26)
        data

    let trophyListPacket (itemId: int) (quantity: int) (dropperId: uint32) =
        let data = Array.create 60 0uy
        data.[2] <- 0xD2uy
        BitConverter.GetBytes(uint32 quantity).CopyTo(data, 4)
        BitConverter.GetBytes(dropperId).CopyTo(data, 8)
        BitConverter.GetBytes(uint16 itemId).CopyTo(data, 16)
        data.[20] <- 0uy
        data.[21] <- 0uy
        data.[22] <- 0uy
        data

    let trophySolutionPacket (poolSlot: int) (judgeFlag: int) (actorName: string) =
        let data = Array.create 60 0uy
        data.[2] <- 0xD3uy
        data.[20] <- byte poolSlot
        data.[21] <- byte judgeFlag
        let nameBytes = padName actorName
        Array.Copy(nameBytes, 0, data, 22, min 15 nameBytes.Length)
        data

    let trophySolutionPacketWithIds (highestId: uint32) (currentId: uint32) (actorName: string) (poolSlot: int) (judgeFlag: int) =
        let data = Array.create 60 0uy
        data.[2] <- 0xD3uy
        BitConverter.GetBytes(highestId).CopyTo(data, 4)
        BitConverter.GetBytes(currentId).CopyTo(data, 8)
        data.[20] <- byte poolSlot
        data.[21] <- byte judgeFlag
        let nameBytes = padName actorName
        Array.Copy(nameBytes, 0, data, 22, min 15 nameBytes.Length)
        Array.Copy(nameBytes, 0, data, 38, min 15 nameBytes.Length)
        data

    let petStatusPacket (ownerId: uint32) (targetId: uint32) (petName: string) =
        let data = Array.create 44 0uy
        data.[2] <- 0x68uy
        BitConverter.GetBytes(ownerId).CopyTo(data, 8)
        BitConverter.GetBytes(targetId).CopyTo(data, 20)
        let nameBytes = padName petName
        Array.Copy(nameBytes, 0, data, 24, min 15 nameBytes.Length)
        data

    let partyMemberPacket (playerId: uint32) (name: string) =
        let data = Array.create 54 0uy
        data.[2] <- 0xDDuy
        BitConverter.GetBytes(playerId).CopyTo(data, 4)
        let nameBytes = padName name
        Array.Copy(nameBytes, 0, data, 38, min 15 nameBytes.Length)
        data

    let battleMessagePacket
        (casterId: uint32)
        (targetId: uint32)
        (messageNum: uint16)
        (param1: uint32)
        (param2: uint32)
        (messageType: byte)
        =
        let data = Array.create 28 0uy
        data.[2] <- 0x29uy
        BitConverter.GetBytes(casterId).CopyTo(data, 4)
        BitConverter.GetBytes(targetId).CopyTo(data, 8)
        BitConverter.GetBytes(param1).CopyTo(data, 12)
        BitConverter.GetBytes(param2).CopyTo(data, 16)
        BitConverter.GetBytes(messageNum).CopyTo(data, 24)
        data.[26] <- messageType
        data

    let battleMessagePacketSimple (casterId: uint32) (targetId: uint32) (messageNum: uint16) =
        battleMessagePacket casterId targetId messageNum 0u 0u 0uy

    let private bitsToBytes (bits: ResizeArray<int>) =
        let byteCount = (bits.Count + 7) / 8

        Array.init byteCount (fun bi ->
            let mutable value = 0

            for bit in 0 .. 7 do
                let idx = bi * 8 + bit

                if idx < bits.Count && bits.[idx] = 1 then
                    value <- value ||| (1 <<< bit)

            byte value)

    let combatActionPacketEx
        (actorId: uint32)
        (targetId: uint32)
        (commandNo: int)
        (commandArg: uint32)
        (damage: int)
        (messageId: int)
        (miss: int)
        =
        let bits = ResizeArray<int>()

        let addBits (value: uint32) (count: int) =
            for i in 0 .. count - 1 do
                bits.Add(int ((value >>> i) &&& 1u))

        addBits actorId 32
        addBits 1u 6
        addBits 0u 4
        addBits (uint32 commandNo) 4
        addBits commandArg 32
        addBits 0u 32
        addBits targetId 32
        addBits 1u 4
        addBits (uint32 miss) 3
        addBits 0u 2
        addBits 0u 12
        addBits 0u 5
        addBits 0u 5
        addBits (uint32 damage) 17
        addBits (uint32 messageId) 10
        addBits 0u 31
        addBits 0u 1
        addBits 0u 1

        let payloadBytes = bitsToBytes bits
        Array.concat [ [| 0x20uy; 0uy; 0x28uy; 0uy; byte payloadBytes.Length |]; payloadBytes ]

    let combatActionPacket
        (actorId: uint32)
        (targetId: uint32)
        (commandNo: int)
        (damage: int)
        (messageId: int)
        (miss: int)
        =
        combatActionPacketEx actorId targetId commandNo 0u damage messageId miss

    /// Default melee hit fixture (actor=1, target=2, damage=42).
    let battle2Packet () =
        combatActionPacket 1u 2u 1 42 1 0
