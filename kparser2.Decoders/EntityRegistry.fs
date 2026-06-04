namespace kparser2.Decoders

open System
open System.Collections.Generic
open System.Text

module EntityRegistry =
    type EntityKind =
        | Player
        | Mob
        | Pet
        | Fellow
        | Unknown

    let private names = Dictionary<uint32, string>()
    let private kinds = Dictionary<uint32, EntityKind>()
    let private jobs = Dictionary<uint32, string>()

    let mutable private localPlayerId: uint32 option = None
    let mutable private pendingLocalPlayerName: string option = None
    let mutable private zoneId: int option = None

    let private readFixedName (data: byte[]) (offset: int) (length: int) =
        if offset >= data.Length then
            ""
        else
            let length = min length (data.Length - offset)

            data.[offset .. offset + length - 1]
            |> Array.takeWhile (fun b -> b <> 0uy)
            |> fun bytes -> Encoding.UTF8.GetString bytes
            |> fun text -> text.Trim()

    let private hasLocalPlayerName () =
        match localPlayerId with
        | Some id -> names.ContainsKey id
        | None -> false

    let private isValidName (name: string) =
        not (String.IsNullOrWhiteSpace name)
        && name.Length > 0
        && name.[0] > ' '
        && not (name.Equals("NULL", StringComparison.OrdinalIgnoreCase))

    let private registerName (entityId: uint32) (name: string) (kind: EntityKind) =
        if isValidName name then
            names.[entityId] <- name
            kinds.[entityId] <- kind

    let private applyPendingLocalPlayerName () =
        match localPlayerId, pendingLocalPlayerName with
        | Some id, Some name when isValidName name ->
            registerName id name EntityKind.Player
            pendingLocalPlayerName <- None
        | _ -> ()

    let registerLocalPlayerName (name: string) =
        if not (isValidName name) then
            ()
        else
            match localPlayerId with
            | Some id ->
                registerName id name EntityKind.Player
                pendingLocalPlayerName <- None
            | None -> pendingLocalPlayerName <- Some name

    let private setZoneId (id: int) =
        if id > 0 then
            zoneId <- Some id

    let private registerJob (entityId: uint32) (job: string) =
        if not (String.IsNullOrWhiteSpace job) then
            jobs.[entityId] <- job

    let observe (evt: kparser2.Protocol.PacketEvent) =
        match evt.PacketId with
        | 0x000Aus when evt.Data.Length >= 148 ->
            let entityId = BitConverter.ToUInt32(evt.Data, 4)
            let name = readFixedName evt.Data 132 16

            registerName entityId name EntityKind.Player
            localPlayerId <- Some entityId
            applyPendingLocalPlayerName ()

            if evt.Data.Length >= 50 then
                setZoneId (int (BitConverter.ToUInt16(evt.Data, 48)))

            if evt.Data.Length >= 88 then
                let mainJob = int evt.Data.[84]
                let subJob = int evt.Data.[85]

                if mainJob > 0 then
                    registerJob entityId $"Job {mainJob}/{subJob}"

        | 0x000Dus when evt.Data.Length >= 91 ->
            let entityId = BitConverter.ToUInt32(evt.Data, 4)
            let updateMask = evt.Data.[10]
            let hasNameFlag = updateMask &&& 0x08uy <> 0uy || updateMask = 0x1Fuy
            let name = readFixedName evt.Data 90 16

            if hasNameFlag then
                registerName entityId name EntityKind.Player
            elif localPlayerId = Some entityId && not (hasLocalPlayerName ()) && isValidName name then
                registerName entityId name EntityKind.Player

            applyPendingLocalPlayerName ()

            if evt.Data.Length >= 94 then
                let mainJob = int evt.Data.[86]
                let subJob = int evt.Data.[87]

                if mainJob > 0 then
                    registerJob entityId $"Job {mainJob}/{subJob}"

        | 0x000Eus when evt.Data.Length >= 68 ->
            let entityId = BitConverter.ToUInt32(evt.Data, 4)
            let updateMask = evt.Data.[10]

            if updateMask &&& 0x08uy <> 0uy || updateMask = 0x1Fuy then
                let name = readFixedName evt.Data 52 16
                registerName entityId name EntityKind.Mob

        | 0x00DFus when evt.Data.Length >= 28 ->
            let entityId = BitConverter.ToUInt32(evt.Data, 4)

            if localPlayerId.IsNone then
                localPlayerId <- Some entityId

            if names.ContainsKey entityId then
                kinds.[entityId] <- EntityKind.Player

            applyPendingLocalPlayerName ()

            let zoneNo = BitConverter.ToUInt16(evt.Data, 26)

            if zoneNo <> 0us then
                setZoneId (int zoneNo)

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

    let tryLocalPlayerId () = localPlayerId

    let tryGetZoneId () = zoneId

    let tryGetJob (entityId: uint32) =
        match jobs.TryGetValue entityId with
        | true, job -> Some job
        | _ -> None

    let tryGetEntityKind (entityId: uint32) =
        match kinds.TryGetValue entityId with
        | true, kind -> Some kind
        | _ -> None

    let isLocalPlayer (entityId: uint32) =
        match localPlayerId with
        | Some id -> id = entityId
        | None -> false

    let allEntityIds () =
        names.Keys |> Seq.toList

    let reset () =
        names.Clear()
        kinds.Clear()
        jobs.Clear()
        localPlayerId <- None
        pendingLocalPlayerName <- None
        zoneId <- None

    let private isLocalPlayerSpeech (packetId: uint16) (modeId: int) =
        packetId = 0x00B5us
        || (packetId = 0x0017us && ChatCommon.isNamelessSelfKind modeId)

    let private localPlayerFallback () =
        localPlayerName () |> Option.defaultValue "Unknown"

    /// Bootstrap local player name from chat packet metadata when safe.
    let observeChatBootstrap (chat: ChatDecoded) (packetId: uint16) =
        if not (String.IsNullOrWhiteSpace chat.Speaker) then
            if ChatCommon.isSayCopyKind chat.ModeId then
                registerLocalPlayerName chat.Speaker
            elif chat.ModeId = 0x03 then
                match ChatCommon.tryParseTellTarget chat.Message with
                | Some target when String.Equals(target, chat.Speaker, StringComparison.OrdinalIgnoreCase) ->
                    registerLocalPlayerName chat.Speaker
                | _ -> ()

    let resolveChatSpeaker (speaker: string) (packetId: uint16) (modeId: int) =
        let resolved =
            if not (String.IsNullOrWhiteSpace speaker) then
                speaker
            elif isLocalPlayerSpeech packetId modeId then
                localPlayerFallback ()
            else
                localPlayerName () |> Option.defaultValue "System"

        if String.IsNullOrWhiteSpace resolved then
            "Unknown"
        else
            resolved

    /// When s2c echo matches a recent outgoing chat, adopt speaker as local player name.
    let learnLocalPlayerFromEcho (speaker: string) (mode: string) (message: string) =
        if isValidName speaker && localPlayerName().IsNone then
            registerLocalPlayerName speaker
