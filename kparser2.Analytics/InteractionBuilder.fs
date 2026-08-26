namespace kparser2.Analytics

open kparser2.Decoders

module InteractionBuilder =
    let private nextId = ref 0

    let reset () = nextId := 0

    let private allocId () =
        let id = !nextId
        nextId := id + 1
        id

    let private isLocalPlayer (entityId: uint32) = EntityRegistry.isLocalPlayer entityId

    let private buildInteraction
        (timestampMs: int64)
        (battleId: int option)
        (actorId: uint32)
        (targetId: uint32)
        (interactionType: InteractionType)
        (harmType: HarmType option)
        (aidType: AidType option)
        (actionName: string)
        (value: int)
        (success: string)
        (commandNo: int)
        (messageId: int)
        (isProc: bool)
        (procValue: int)
        =
        let damageModifier = InteractionClassification.classifyDamageModifier messageId isProc

        let category =
            InteractionClassification.classifyCategory interactionType harmType aidType damageModifier actionName

        { Id = allocId ()
          BattleId = battleId
          TimestampMs = timestampMs
          InteractionType = interactionType
          HarmType = harmType
          AidType = aidType
          Category = category
          DamageModifier = damageModifier
          ActorId = actorId
          TargetId = targetId
          ActorName = EntityRegistry.formatEntity actorId
          TargetName = EntityRegistry.formatEntity targetId
          ActionName = actionName
          Value = value
          Success = success
          CommandNo = commandNo
          MessageId = messageId
          IsProc = isProc
          ProcValue = procValue
          IsLocalPlayerActor = isLocalPlayer actorId
          IsLocalPlayerTarget = isLocalPlayer targetId }

    let fromCombatAction (timestampMs: int64) (battleId: int option) (action: CombatActionDecoded) =
        action.Targets
        |> List.collect (fun target ->
            target.Effects
            |> List.choose (fun effect ->
                if BattleMessageCatalog.isActionStartCommand action.CommandNo then
                    None
                else
                    let interactionType, harmType, aidType =
                        BattleMessageCatalog.classifyActionEffect action.CommandNo effect.MessageId effect.Miss effect.Value

                    if interactionType = InteractionType.Unknown && effect.Value = 0 && effect.Miss = 0 then
                        None
                    else
                        Some(
                            buildInteraction
                                timestampMs
                                battleId
                                action.ActorId
                                target.TargetId
                                interactionType
                                harmType
                                aidType
                                (BattleMessageCatalog.actionName action.CommandNo (int action.CommandArg) effect.MessageId)
                                effect.Value
                                (BattleMessageCatalog.successLabelForEffect effect.MessageId effect.Miss effect.Value)
                                action.CommandNo
                                effect.MessageId
                                effect.HasProc
                                effect.ProcValue
                        )))

    let fromCombatMessage (timestampMs: int64) (battleId: int option) (message: CombatMessageDecoded) =
        let interactionType, harmType, aidType =
            BattleMessageCatalog.classifyCombatMessage (int message.MessageNum) (int message.MessageType)

        let actionName = MsgBasicCatalog.messageLabel (int message.MessageNum)

        let category =
            if actionName.Contains("Skillchain", System.StringComparison.OrdinalIgnoreCase) then
                InteractionCategory.Skillchain
            else
                InteractionClassification.classifyCategory interactionType harmType aidType DamageModifier.Normal actionName

        [ { Id = allocId ()
            BattleId = battleId
            TimestampMs = timestampMs
            InteractionType = interactionType
            HarmType = harmType
            AidType = aidType
            Category = category
            DamageModifier = DamageModifier.Normal
            ActorId = message.CasterId
            TargetId = message.TargetId
            ActorName = EntityRegistry.formatEntity message.CasterId
            TargetName = EntityRegistry.formatEntity message.TargetId
            ActionName = actionName
            Value =
                match ExperienceParser.tryParseBattleMessage (int message.MessageNum) message.Param1 message.Param2 with
                | Some parsed when parsed.Points > 0 -> parsed.Points
                | Some parsed -> parsed.Chain
                | None -> int message.Param1
            Success = "message"
            CommandNo = 0
            MessageId = int message.MessageNum
            IsProc = false
            ProcValue = 0
            IsLocalPlayerActor = isLocalPlayer message.CasterId
            IsLocalPlayerTarget = isLocalPlayer message.TargetId } ]

    let fromDecoderEvents (timestampMs: int64) (battleId: int option) (events: DecoderEvent list) =
        events
        |> List.collect (
            function
            | DecoderEvent.CombatAction action -> fromCombatAction timestampMs battleId action
            | DecoderEvent.CombatMessage message -> fromCombatMessage timestampMs battleId message
            | _ -> []
        )
