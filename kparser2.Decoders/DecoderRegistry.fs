namespace kparser2.Decoders

open kparser2.Protocol

module DecoderRegistry =
    let decode (evt: PacketEvent) =
        let chat =
            match evt.PacketId with
            | 0x0017us when evt.Direction = PacketDirection.Incoming ->
                Chat0x17.decode evt.Data
                |> Option.map (fun c -> DecoderEvent.Chat c)
                |> Option.toList
            | 0x00B5us when evt.Direction = PacketDirection.Outgoing ->
                Chat0xB5.decode evt.Data
                |> Option.map (fun c -> DecoderEvent.Chat c)
                |> Option.toList
            | 0x005Aus when evt.Direction = PacketDirection.Incoming ->
                ParameterizedChat.emote evt.Data |> Option.map DecoderEvent.Chat |> Option.toList
            | 0x0029us when evt.Direction = PacketDirection.Incoming ->
                Battle0x29.decode evt.Data |> Option.bind ParameterizedChat.statusExpiry
                |> Option.map DecoderEvent.Chat |> Option.toList
            | _ -> []

        let loot =
            match evt.PacketId with
            | 0x00D2us ->
                Trophy0xD2.decode evt.Data
                |> Option.map (fun l -> DecoderEvent.Loot l)
                |> Option.toList
            | 0x00D3us ->
                Trophy0xD3.decode evt.Data
                |> Option.map (fun l -> DecoderEvent.Loot l)
                |> Option.toList
            | _ -> []

        let combatMessage =
            match evt.PacketId with
            | 0x0029us ->
                Battle0x29.decode evt.Data
                |> Option.map (fun m -> DecoderEvent.CombatMessage m)
                |> Option.toList
            | 0x002Dus ->
                Battle0x2D.decode evt.Data
                |> Option.map (fun m -> DecoderEvent.CombatMessage m)
                |> Option.toList
            | _ -> []

        let combatActions =
            if evt.PacketId = 0x0028us then
                Battle0x28.decode evt.Data
                |> Option.map (fun a -> DecoderEvent.CombatAction a)
                |> Option.toList
            else
                []

        { Events = chat @ loot @ combatMessage @ combatActions }

    let decodeToResult (evt: PacketEvent) = decode evt
