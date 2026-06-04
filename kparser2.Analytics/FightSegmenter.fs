namespace kparser2.Analytics

open kparser2.Decoders

module FightSegmenter =
    type State =
        { Battles: Battle list
          CurrentBattleId: int option
          LastEventMs: int64
          NextBattleId: int }

    let initial =
        { Battles = []
          CurrentBattleId = None
          LastEventMs = 0L
          NextBattleId = 1 }

    let private isMob (entityId: uint32) =
        match EntityRegistry.tryGetEntityKind entityId with
        | Some EntityRegistry.EntityKind.Mob -> true
        | _ -> false

    let private enemyName (targetId: uint32) =
        EntityRegistry.formatEntity targetId

    let private openBattle state timestampMs targetId =
        let battle =
            { Id = state.NextBattleId
              EnemyName = enemyName targetId
              EnemyId = Some targetId
              StartMs = timestampMs
              EndMs = None
              Killed = false
              KillerId = None
              ExperiencePoints = 0
              ExperienceChain = 0 }

        { state with
            Battles = battle :: state.Battles
            CurrentBattleId = Some battle.Id
            NextBattleId = state.NextBattleId + 1
            LastEventMs = timestampMs }

    let private closeBattle state timestampMs killed killerId =
        match state.CurrentBattleId with
        | None -> state
        | Some battleId ->
            let battles =
                state.Battles
                |> List.map (fun b ->
                    if b.Id = battleId then
                        { b with
                            EndMs = Some timestampMs
                            Killed = killed
                            KillerId = killerId }
                    else
                        b)

            { state with
                Battles = battles
                CurrentBattleId = None
                LastEventMs = timestampMs }

    let private shouldCloseIdle state timestampMs =
        match state.CurrentBattleId, state.LastEventMs with
        | Some _, last when timestampMs - last > 30_000L -> true
        | _ -> false

    let applyInteraction state (interaction: Interaction) =
        let state =
            if shouldCloseIdle state interaction.TimestampMs then
                closeBattle state interaction.TimestampMs false None
            else
                state

        let state =
            match interaction.InteractionType, state.CurrentBattleId with
            | InteractionType.Harm, None when isMob interaction.TargetId ->
                openBattle state interaction.TimestampMs interaction.TargetId
            | _ -> state

        let battleId =
            match state.CurrentBattleId with
            | Some id -> Some id
            | None -> None

        let state =
            match interaction.InteractionType with
            | InteractionType.Death when isMob interaction.TargetId ->
                closeBattle state interaction.TimestampMs true (Some interaction.ActorId)
            | InteractionType.Death ->
                state
            | _ ->
                { state with LastEventMs = interaction.TimestampMs }

        state, battleId

    let onZoneChange state timestampMs =
        let state = closeBattle state timestampMs false None
        { state with LastEventMs = timestampMs }

    let applyExperience state timestampMs xp chain =
        match state.CurrentBattleId with
        | None -> state
        | Some battleId ->
            let battles =
                state.Battles
                |> List.map (fun b ->
                    if b.Id = battleId then
                        { b with
                            ExperiencePoints = b.ExperiencePoints + xp
                            ExperienceChain =
                                if chain > 0 then chain else b.ExperienceChain }
                    else
                        b)

            { state with
                Battles = battles
                LastEventMs = timestampMs }
