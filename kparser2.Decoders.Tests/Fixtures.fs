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

    let chatPacket (speaker: string) (message: string) (kind: byte) =
        let header = [| 0x20uy; 0uy; 0x17uy; 0uy |]
        let nameBytes = padName speaker
        let messageBytes = Encoding.UTF8.GetBytes(message)
        Array.concat [ header; [| kind; 0uy; 0uy; 0uy |]; nameBytes; messageBytes; [| 0uy |] ]

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

    let battleMessagePacket (casterId: uint32) (targetId: uint32) (messageNum: uint16) =
        let data = Array.create 28 0uy
        data.[2] <- 0x29uy
        BitConverter.GetBytes(casterId).CopyTo(data, 4)
        BitConverter.GetBytes(targetId).CopyTo(data, 8)
        BitConverter.GetBytes(messageNum).CopyTo(data, 24)
        data.[26] <- 0uy
        data

    let battle2Packet () =
        let mutable bits = ResizeArray<int>()

        let addBits (value: uint32) (count: int) =
            for i in 0 .. count - 1 do
                bits.Add(int ((value >>> i) &&& 1u))

        addBits 1u 32
        addBits 1u 6
        addBits 0u 4
        addBits 1u 4
        addBits 0u 32
        addBits 0u 32
        addBits 2u 32
        addBits 1u 4
        addBits 0u 3
        addBits 0u 2
        addBits 0u 12
        addBits 0u 5
        addBits 0u 5
        addBits 42u 17
        addBits 1u 10
        addBits 0u 31
        addBits 0u 1
        addBits 0u 1

        let payloadBytes =
            let byteCount = (bits.Count + 7) / 8
            Array.init byteCount (fun bi ->
                let mutable value = 0

                for bit in 0 .. 7 do
                    let idx = bi * 8 + bit

                    if idx < bits.Count && bits.[idx] = 1 then
                        value <- value ||| (1 <<< bit)

                byte value)

        Array.concat [ [| 0x20uy; 0uy; 0x28uy; 0uy; byte payloadBytes.Length |]; payloadBytes ]
