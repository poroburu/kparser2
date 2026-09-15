namespace kparser2.Decoders

open System

/// Verified subset of parameterized system text; unknown templates stay unsupported.
module ParameterizedChat =
    let statusExpiry (message: CombatMessageDecoded) =
        // server/scripts/enum/{msg,effect}.lua; paired BST ChatLines.
        let label =
            match message.Param1 with
            | 5u -> Some "blindness"
            | 7u -> Some "petrification"
            | 42u -> Some "Regen"
            | 66u -> Some "Copy Image"
            | 148u -> Some "Evasion Down"
            | _ -> None
        if message.MessageNum <> 206us then None
        else
            label |> Option.map (fun effect ->
                let actor = EntityRegistry.formatEntity message.CasterId
                { Mode = "System"; ModeId = -1; IsGm = false; Speaker = "System"
                  Message = $"{actor}'s {effect} effect wears off."; ZoneId = None })

    let emote (data: byte[]) =
        // XiPackets 0x005A MOTIONMES; enum/emote.lua STARE=23.
        if data.Length < 24 || BitConverter.ToUInt16(data, 16) <> 23us || data.[22] > 1uy then None
        else
            let actor = EntityRegistry.formatEntity (BitConverter.ToUInt32(data, 4))
            let target = BitConverter.ToUInt32(data, 8)
            let body = if target = 0u then $"{actor} stares..." else $"{actor} stares at {EntityRegistry.formatEntity target}..."
            Some { Mode = "Emote"; ModeId = 8; IsGm = false; Speaker = actor; Message = body; ZoneId = None }
