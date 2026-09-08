namespace kparser2.Analytics

open System

/// Stable, name-keyed projections shared by headless parity tooling.
/// Entity IDs, battle IDs, timestamps, and packet ordering are intentionally
/// omitted from the comparison keys.
module ParityProjection =
    let private optionText value =
        value |> Option.map string |> Option.defaultValue ""

    let interactions (snap: AnalyticsSnapshot) =
        snap.Interactions
        |> List.map (fun i ->
            let harmType = optionText i.HarmType
            let aidType = optionText i.AidType
            let actionType =
                if not (String.IsNullOrWhiteSpace harmType) then
                    harmType
                elif not (String.IsNullOrWhiteSpace aidType) then
                    aidType
                else
                    string i.InteractionType

            {| actorName = i.ActorName
               targetName = i.TargetName
               interactionType = string i.InteractionType
               actionType = actionType
               harmType = harmType
               aidType = aidType
               amount = i.Value
               success = i.Success
               commandNo = i.CommandNo
               messageId = i.MessageId
               sourcePacketId = i.SourcePacketId |> Option.defaultValue "" |})

    let chat (snap: AnalyticsSnapshot) =
        snap.ChatMessages
        |> List.filter (fun m ->
            String.IsNullOrWhiteSpace m.Direction
            || m.Direction.Equals("incoming", StringComparison.OrdinalIgnoreCase))
        |> List.map (fun m ->
            let speaker =
                if String.IsNullOrWhiteSpace m.Speaker then
                    "System"
                else
                    m.Speaker

            {| speaker = speaker
               mode = m.Mode
               message = m.Message |})
