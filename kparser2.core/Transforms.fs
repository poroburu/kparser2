namespace kparser2.Core

open kparser2.Decoders
open kparser2.Protocol

type ChatExtract =
    { Mode: string
      IsGm: bool
      Speaker: string
      Message: string }

type LootExtract =
    { EventType: string
      ItemId: int
      ItemName: string
      Quantity: int
      Gil: int
      PoolSlot: int
      ActorName: string
      Source: string
      Detail: string }

type CombatExtract =
    { EventKind: string
      ActorId: uint32
      TargetId: uint32
      CommandNo: int
      CommandArg: uint32
      MessageNum: int
      MessageType: int
      Param1: int
      Param2: int
      Miss: int
      Damage: int
      MessageId: int
      Summary: string }

type TransformResult =
    { ChatEvents: ChatExtract list
      LootEvents: LootExtract list
      CombatEvents: CombatExtract list }

module Transforms =
    let private missLabel miss =
        match miss with
        | 0 -> "hit"
        | 1 -> "miss"
        | 2 -> "guard"
        | 3 -> "parry"
        | 4 -> "block"
        | 9 -> "evade"
        | n -> $"state-{n}"

    let private lootTypeLabel eventType =
        match eventType with
        | LootEventType.Found -> "Found"
        | LootEventType.Lot -> "Lot"
        | LootEventType.Pass -> "Pass"
        | LootEventType.Won -> "Won"
        | LootEventType.Floor -> "Floor"
        | LootEventType.Lost -> "Lost"

    let private resolveActorName (name: string) (entityId: int option) =
        if not (System.String.IsNullOrWhiteSpace name) then
            name
        else
            match entityId with
            | Some id when id <> 0 -> EntityRegistry.formatEntity (uint32 id)
            | _ -> "Unknown"

    let private mapCombatMessage (message: CombatMessageDecoded) =
        let caster = EntityRegistry.formatEntity message.CasterId
        let target = EntityRegistry.formatEntity message.TargetId

        [ { EventKind = "Message"
            ActorId = message.CasterId
            TargetId = message.TargetId
            CommandNo = 0
            CommandArg = 0u
            MessageNum = int message.MessageNum
            MessageType = int message.MessageType
            Param1 = int message.Param1
            Param2 = int message.Param2
            Miss = -1
            Damage = 0
            MessageId = int message.MessageNum
            Summary =
                $"msg 0x{message.MessageNum:X} {caster} -> {target} p1={message.Param1} p2={message.Param2}" } ]

    let private mapCombatAction (action: CombatActionDecoded) =
        let actor = EntityRegistry.formatEntity action.ActorId

        action.Targets
        |> List.collect (fun target ->
            let targetName = EntityRegistry.formatEntity target.TargetId

            target.Effects
            |> List.map (fun effect ->
                { EventKind = "Action"
                  ActorId = action.ActorId
                  TargetId = target.TargetId
                  CommandNo = action.CommandNo
                  CommandArg = action.CommandArg
                  MessageNum = effect.MessageId
                  MessageType = action.CommandNo
                  Param1 = int action.Info
                  Param2 = 0
                  Miss = effect.Miss
                  Damage = effect.Value
                  MessageId = effect.MessageId
                  Summary =
                      $"cmd {action.CommandNo} {actor} -> {targetName} {missLabel effect.Miss} value={effect.Value}" }))

    let runFromDecoded (evt: PacketEvent) (decoded: DecoderResult) =
        let chatEvents =
            decoded.Events
            |> List.choose (
                function
                | DecoderEvent.Chat chat ->
                    EntityRegistry.observeChatBootstrap chat evt.PacketId

                    Some
                        { Mode = chat.Mode
                          IsGm = chat.IsGm
                          Speaker = EntityRegistry.resolveChatSpeaker chat.Speaker evt.PacketId chat.ModeId
                          Message = chat.Message }
                | _ -> None
            )

        let lootEvents =
            decoded.Events
            |> List.choose (
                function
                | DecoderEvent.Loot loot ->
                    Some
                        { EventType = lootTypeLabel loot.EventType
                          ItemId = loot.ItemId
                          ItemName = loot.ItemName
                          Quantity = loot.Quantity
                          Gil = loot.Gil
                          PoolSlot = loot.PoolSlot
                          ActorName = resolveActorName loot.ActorName loot.ActorId
                          Source = evt.PacketName
                          Detail = loot.Detail }
                | _ -> None
            )

        let combatEvents =
            decoded.Events
            |> List.collect (
                function
                | DecoderEvent.CombatMessage message -> mapCombatMessage message
                | DecoderEvent.CombatAction action -> mapCombatAction action
                | _ -> []
            )

        { ChatEvents = chatEvents
          LootEvents = lootEvents
          CombatEvents = combatEvents }

    let run (evt: PacketEvent) =
        EntityRegistry.observe evt
        let decoded = DecoderRegistry.decode evt
        runFromDecoded evt decoded
