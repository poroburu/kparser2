namespace kparser2.Decoders

open System
open System.Collections.Generic
open System.Text

module EntityRegistry =
    let private names = Dictionary<uint32, string>()

    let mutable private localPlayerId: uint32 option = None

    let private readFixedName (data: byte[]) (offset: int) (length: int) =
        if offset + length > data.Length then
            ""
        else
            data.[offset .. offset + length - 1]
            |> Array.takeWhile (fun b -> b <> 0uy)
            |> fun bytes -> Encoding.UTF8.GetString bytes
            |> fun text -> text.Trim()

    let observe (evt: kparser2.Protocol.PacketEvent) =
        match evt.PacketId with
        | 0x000Aus when evt.Data.Length >= 148 ->
            let entityId = BitConverter.ToUInt32(evt.Data, 4)
            let name = readFixedName evt.Data 132 16

            if not (String.IsNullOrWhiteSpace name) then
                names.[entityId] <- name
                localPlayerId <- Some entityId

        | 0x000Dus when evt.Data.Length >= 106 ->
            let entityId = BitConverter.ToUInt32(evt.Data, 4)
            let updateMask = evt.Data.[10]

            if updateMask &&& 0x08uy <> 0uy || updateMask = 0x1Fuy then
                let name = readFixedName evt.Data 90 16

                if not (String.IsNullOrWhiteSpace name) then
                    names.[entityId] <- name

        | _ -> ()

    let tryGetName (entityId: uint32) =
        match names.TryGetValue entityId with
        | true, name -> Some name
        | _ -> None

    let formatEntity (entityId: uint32) =
        match tryGetName entityId with
        | Some name -> name
        | None -> $"Entity {entityId}"

    let localPlayerName () =
        match localPlayerId with
        | Some id -> tryGetName id
        | None -> None

    let reset () =
        names.Clear()
        localPlayerId <- None

    let resolveChatSpeaker (speaker: string) (packetId: uint16) =
        if not (String.IsNullOrWhiteSpace speaker) then
            speaker
        elif packetId = 0x00B5us then
            localPlayerName () |> Option.defaultValue "Unknown"
        else
            localPlayerName () |> Option.defaultValue "System"
