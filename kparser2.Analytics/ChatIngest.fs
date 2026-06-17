namespace kparser2.Analytics

open System
open System.Text.RegularExpressions
open kparser2.Decoders
open kparser2.Protocol

module ChatIngest =
    type PendingOutgoing =
        { TimestampMs: int64
          Mode: string
          Message: string }

    let private placeholderSpeakers = Set.ofList [ "Unknown"; "System" ]

    let tryParseTellTarget (message: string) = ChatCommon.tryParseTellTarget message

    let isPlaceholderSpeaker speaker =
        String.IsNullOrWhiteSpace speaker || placeholderSpeakers.Contains speaker

    let normalizeSpeaker speaker =
        if String.IsNullOrWhiteSpace speaker then
            "Unknown"
        else
            speaker

    let buildRecord
        (timestampMs: int64)
        (evt: PacketEvent)
        (chat: ChatDecoded)
        (speaker: string)
        =
        let speaker = normalizeSpeaker speaker

        let isLocal =
            evt.PacketId = 0x00B5us
            || ChatCommon.isNamelessSelfKind chat.ModeId
            || (not (isPlaceholderSpeaker speaker)
                && (EntityRegistry.localPlayerName ()
                    |> Option.map (fun n -> String.Equals(n, speaker, StringComparison.OrdinalIgnoreCase))
                    |> Option.defaultValue false))

        { TimestampMs = timestampMs
          Mode = chat.Mode
          ModeId = chat.ModeId
          IsGm = chat.IsGm
          Speaker = speaker
          Message = chat.Message
          PacketId = int evt.PacketId
          Direction = evt.Direction.ToString().ToLowerInvariant()
          IsLocalPlayer = isLocal
          TargetName = tryParseTellTarget chat.Message }

    let private isDuplicate (a: ChatMessageRecord) (b: ChatMessageRecord) =
        a.Mode = b.Mode
        && a.Message = b.Message
        && abs (a.TimestampMs - b.TimestampMs) <= 500L

    let private preferSpeaker left right =
        let left = normalizeSpeaker left
        let right = normalizeSpeaker right

        if isPlaceholderSpeaker left && not (isPlaceholderSpeaker right) then
            right
        elif isPlaceholderSpeaker right && not (isPlaceholderSpeaker left) then
            left
        elif left = right then
            left
        else
            left

    /// Merge duplicate 0xB5 + 0x17 echo rows; keep the row with the best speaker label.
    let appendChat (messages: ChatMessageRecord list) (record: ChatMessageRecord) =
        match messages with
        | head :: tail when isDuplicate head record ->
            let mergedSpeaker = preferSpeaker head.Speaker record.Speaker

            let baseRecord =
                if record.Direction = "incoming" then
                    record
                elif head.Direction = "incoming" then
                    head
                else
                    record

            let merged =
                { baseRecord with
                    Speaker = mergedSpeaker
                    IsLocalPlayer = head.IsLocalPlayer || record.IsLocalPlayer }

            merged :: tail
        | _ -> record :: messages

    let tryMatchOutgoingEcho (pending: PendingOutgoing list) (timestampMs: int64) (mode: string) (message: string) (speaker: string) =
        pending
        |> List.tryFind (fun p ->
            p.Mode = mode
            && p.Message = message
            && abs (p.TimestampMs - timestampMs) <= 500L)
        |> Option.map (fun _ -> speaker)
