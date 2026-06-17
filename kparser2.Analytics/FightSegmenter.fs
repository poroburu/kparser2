namespace kparser2.Analytics

open kparser2.Decoders

module FightSegmenter =
    type State =
        { Battles: Battle list
          CurrentBattleId: int option
          LastEventMs: int64
          NextBattleId: int
          PendingExperience: (int64 * int * int) list }

    let initial =
        { Battles = []
          CurrentBattleId = None
          LastEventMs = 0L
          NextBattleId = 1
          PendingExperience = [] }

    let private xpLookbackMs = 15_000L

    let private isMobTarget (targetId: uint32) (actorId: uint32) =
        if EntityRegistry.isLocalPlayer targetId then
            false
        else
            match EntityRegistry.tryGetEntityKind targetId with
            | Some EntityRegistry.EntityKind.Mob -> true
            | Some EntityRegistry.EntityKind.Player -> false
            | Some EntityRegistry.EntityKind.Pet -> false
            | Some EntityRegistry.EntityKind.Fellow -> false
            | _ ->
                not (EntityRegistry.isLocalPlayer targetId)
                && (EntityRegistry.isLocalPlayer actorId
                    || EntityRegistry.isLocalPet actorId)

    let private isDefeatedEnemy (targetId: uint32) =
        if EntityRegistry.isLocalPlayer targetId || EntityRegistry.isLocalPet targetId then
            false
        else
            match EntityRegistry.tryGetEntityKind targetId with
            | Some EntityRegistry.EntityKind.Player -> false
            | Some EntityRegistry.EntityKind.Pet -> false
            | Some EntityRegistry.EntityKind.Fellow -> false
            | Some EntityRegistry.EntityKind.Mob -> true
            | _ -> not (EntityRegistry.isLocalPlayer targetId)

    let private enemyName (targetId: uint32) =
        EntityRegistry.formatEntity targetId

    let private applyXpToBattle (battles: Battle list) battleId xp chain =
        battles
        |> List.map (fun b ->
            if b.Id = battleId then
                { b with
                    ExperiencePoints = if xp > 0 then xp else b.ExperiencePoints
                    ExperienceChain = if chain > 0 then chain else b.ExperienceChain }
            else
                b)

    let private findRecentKilledBattle (battles: Battle list) timestampMs requireZeroXp =
        battles
        |> List.filter (fun b -> b.Killed)
        |> List.filter (fun b -> not requireZeroXp || b.ExperiencePoints = 0)
        |> List.filter (fun b ->
            let endMs = b.EndMs |> Option.defaultValue b.StartMs
            timestampMs - endMs <= xpLookbackMs && timestampMs >= endMs)
        |> List.sortByDescending (fun b -> b.EndMs |> Option.defaultValue b.StartMs)
        |> List.tryHead
        |> Option.map (fun b -> b.Id)

    let private findXpTargetBattle state timestampMs xp =
        match state.CurrentBattleId with
        | Some id -> Some id
        | None ->
            if xp > 0 then
                findRecentKilledBattle state.Battles timestampMs true
            else
                findRecentKilledBattle state.Battles timestampMs false

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

            match state.PendingExperience, killed with
            | (_, xp, chain) :: rest, true ->
                { state with
                    Battles = applyXpToBattle battles battleId xp chain
                    CurrentBattleId = None
                    LastEventMs = timestampMs
                    PendingExperience = rest }
            | _ ->
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
            | InteractionType.Harm, None when isMobTarget interaction.TargetId interaction.ActorId ->
                openBattle state interaction.TimestampMs interaction.TargetId
            | _ -> state

        let battleId =
            match state.CurrentBattleId with
            | Some id -> Some id
            | None -> None

        let state =
            match interaction.InteractionType with
            | InteractionType.Death when isDefeatedEnemy interaction.TargetId ->
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
        if xp <= 0 && chain <= 0 then
            state, None
        else
            match findXpTargetBattle state timestampMs xp with
            | Some battleId ->
                let battles = applyXpToBattle state.Battles battleId xp chain

                { state with
                    Battles = battles
                    LastEventMs = timestampMs },
                Some battleId
            | None ->
                let pending = (timestampMs, xp, chain) :: state.PendingExperience |> List.truncate 8

                { state with
                    PendingExperience = pending
                    LastEventMs = timestampMs },
                None
