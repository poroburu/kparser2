namespace kparser2.Analytics

open System

type MobFilter =
    { GroupMobs: bool
      ExcludeZeroXp: bool
      SelectedMobName: string option
      SelectedBattleId: int option
      SelectedPlayerName: string option }

module MobFilter =
    let defaultFilter =
        { GroupMobs = true
          ExcludeZeroXp = false
          SelectedMobName = None
          SelectedBattleId = None
          SelectedPlayerName = None }

type QueryRow = { Label: string; Value: string; Count: int; Total: int }

module AnalyticsQueries =
    let private matchesMob (filter: MobFilter) (battle: Battle) =
        if filter.ExcludeZeroXp && not (MobXpLookup.hasXp battle.EnemyName) then
            false
        else
            match filter.SelectedBattleId with
            | Some id -> battle.Id = id
            | None ->
                match filter.SelectedMobName with
                | Some name when filter.GroupMobs -> battle.EnemyName.Equals(name, System.StringComparison.OrdinalIgnoreCase)
                | Some name -> battle.EnemyName = name
                | None -> true

    let private filterInteractions (snap: AnalyticsSnapshot) (filter: MobFilter) (predicate: Interaction -> bool) =
        snap.Interactions
        |> List.filter predicate
        |> List.filter (fun i ->
            match i.BattleId with
            | None -> true
            | Some bid ->
                snap.Battles
                |> List.tryFind (fun b -> b.Id = bid)
                |> Option.map (matchesMob filter)
                |> Option.defaultValue true)
        |> List.filter (fun i ->
            match filter.SelectedPlayerName with
            | None -> true
            | Some name ->
                i.ActorName.Equals(name, System.StringComparison.OrdinalIgnoreCase)
                || snap.Combatants
                   |> List.tryFind (fun c -> c.Id = i.ActorId)
                   |> Option.map (fun c -> c.Name.Equals(name, System.StringComparison.OrdinalIgnoreCase))
                   |> Option.defaultValue false)

    let fights (snap: AnalyticsSnapshot) =
        snap.Battles
        |> List.map (fun b ->
            { Label = b.EnemyName
              Value =
                $"#{b.Id} killed={b.Killed} start={b.StartMs} end={Option.defaultValue b.StartMs b.EndMs} xp={b.ExperiencePoints}"
              Count = 1
              Total = int (Option.defaultValue b.StartMs b.EndMs - b.StartMs) })

    let offenseSummary (snap: AnalyticsSnapshot) (filter: MobFilter) =
        filterInteractions snap filter (fun i ->
            i.InteractionType = InteractionType.Harm
            && i.IsLocalPlayerActor
            && i.Value > 0)
        |> List.groupBy (fun i -> InteractionClassification.categoryLabel i.Category)
        |> List.map (fun (label, rows) ->
            { Label = label
              Value = "damage"
              Count = rows.Length
              Total = rows |> List.sumBy (fun r -> r.Value) })

    let offenseDetail (snap: AnalyticsSnapshot) (filter: MobFilter) =
        filterInteractions snap filter (fun i ->
            i.InteractionType = InteractionType.Harm && i.IsLocalPlayerActor)
        |> List.groupBy (fun i -> $"{i.ActionName} — {i.Success}")
        |> List.map (fun (label, rows) ->
            { Label = label
              Value = InteractionClassification.categoryLabel rows.Head.Category
              Count = rows.Length
              Total = rows |> List.sumBy (fun r -> r.Value) })

    let defenseSummary (snap: AnalyticsSnapshot) (filter: MobFilter) =
        filterInteractions snap filter (fun i ->
            i.InteractionType = InteractionType.Harm
            && i.IsLocalPlayerTarget
            && i.Value > 0)
        |> List.groupBy (fun i -> InteractionClassification.categoryLabel i.Category)
        |> List.map (fun (label, rows) ->
            { Label = label
              Value = "damage taken"
              Count = rows.Length
              Total = rows |> List.sumBy (fun r -> r.Value) })

    let defenseDetail (snap: AnalyticsSnapshot) (filter: MobFilter) =
        filterInteractions snap filter (fun i ->
            i.InteractionType = InteractionType.Harm && i.IsLocalPlayerTarget)
        |> List.groupBy (fun i -> $"{i.ActionName} — {i.Success}")
        |> List.map (fun (label, rows) ->
            { Label = label
              Value = InteractionClassification.categoryLabel rows.Head.Category
              Count = rows.Length
              Total = rows |> List.sumBy (fun r -> r.Value) })

    let deaths (snap: AnalyticsSnapshot) (filter: MobFilter) =
        filterInteractions snap filter (fun i -> i.InteractionType = InteractionType.Death)
        |> List.map (fun i ->
            { Label = $"{i.ActorName} -> {i.TargetName}"
              Value = i.ActionName
              Count = 1
              Total = int i.TimestampMs })

    let recovery (snap: AnalyticsSnapshot) (filter: MobFilter) =
        filterInteractions snap filter (fun i ->
            i.InteractionType = InteractionType.Aid
            && i.AidType = Some AidType.Recovery
            && i.Value > 0)
        |> List.groupBy (fun i -> i.ActionName)
        |> List.map (fun (action, rows) ->
            { Label = action
              Value = "healing"
              Count = rows.Length
              Total = rows |> List.sumBy (fun r -> r.Value) })

    let buffs (snap: AnalyticsSnapshot) (filter: MobFilter) =
        filterInteractions snap filter (fun i ->
            i.InteractionType = InteractionType.Aid && i.AidType = Some AidType.Enhance)
        |> List.groupBy (fun i -> i.ActionName)
        |> List.map (fun (action, rows) ->
            { Label = action; Value = "buff"; Count = rows.Length; Total = 0 })

    let debuffs (snap: AnalyticsSnapshot) (filter: MobFilter) =
        filterInteractions snap filter (fun i ->
            i.InteractionType = InteractionType.Harm && i.HarmType = Some HarmType.Enfeeble)
        |> List.groupBy (fun i -> i.ActionName)
        |> List.map (fun (action, rows) ->
            { Label = action; Value = "debuff"; Count = rows.Length; Total = rows.Length })

    let itemUses (snap: AnalyticsSnapshot) =
        snap.ItemUses
        |> List.groupBy (fun u -> u.ActorName, u.ItemName)
        |> List.map (fun ((actor, item), rows) ->
            { Label = actor; Value = item; Count = rows.Length; Total = rows |> List.sumBy (fun r -> r.Quantity) })

    let chat (snap: AnalyticsSnapshot) (modeFilter: string option) (speakerFilter: string option) =
        snap.ChatMessages
        |> List.filter (fun c ->
            match modeFilter with
            | Some m when m <> "All" -> c.Mode.Equals(m, System.StringComparison.OrdinalIgnoreCase)
            | _ -> true)
        |> List.filter (fun c ->
            match speakerFilter with
            | Some s when s <> "All" -> c.Speaker.Equals(s, System.StringComparison.OrdinalIgnoreCase)
            | _ -> true)
        |> List.map (fun c ->
            let prefix = if c.IsLocalPlayer then "(you) " else ""
            { Label = c.Speaker
              Value = $"{prefix}[{c.Mode}] {c.Message}"
              Count = 1
              Total = int c.TimestampMs })

    let chatSummary (snap: AnalyticsSnapshot) (modeFilter: string option) (speakerFilter: string option) =
        snap.ChatMessages
        |> List.filter (fun c ->
            match modeFilter with
            | Some m when m <> "All" -> c.Mode.Equals(m, System.StringComparison.OrdinalIgnoreCase)
            | _ -> true)
        |> List.filter (fun c ->
            match speakerFilter with
            | Some s when s <> "All" -> c.Speaker.Equals(s, System.StringComparison.OrdinalIgnoreCase)
            | _ -> true)
        |> List.groupBy (fun c -> c.Mode, c.Speaker)
        |> List.map (fun ((mode, speaker), rows) ->
            { Label = speaker
              Value = mode
              Count = rows.Length
              Total = rows.Length })

    let experience (snap: AnalyticsSnapshot) =
        snap.ExperienceRecords
        |> List.groupBy (fun x -> x.ActorName)
        |> List.map (fun (actor, rows) ->
            { Label = actor
              Value = "xp"
              Count = rows.Length
              Total = rows |> List.sumBy (fun r -> r.ExperiencePoints) })

    let enfeebling (snap: AnalyticsSnapshot) (filter: MobFilter) =
        filterInteractions snap filter (fun i ->
            i.InteractionType = InteractionType.Harm && i.HarmType = Some HarmType.Enfeeble)
        |> List.groupBy (fun i -> i.ActionName)
        |> List.map (fun (action, rows) ->
            { Label = action; Value = "enfeeble"; Count = rows.Length; Total = rows.Length })

    let lootSummary (snap: AnalyticsSnapshot) =
        snap.LootRecords
        |> List.groupBy (fun l -> l.ItemName)
        |> List.map (fun (item, rows) ->
            { Label = item; Value = "drops"; Count = rows.Length; Total = rows |> List.sumBy (fun r -> r.Quantity) })

    let performance (snap: AnalyticsSnapshot) (filter: MobFilter) =
        let offense = offenseSummary snap filter |> List.sumBy (fun r -> r.Total)
        let durationMs = snap.Battles |> List.sumBy (fun b -> int (Option.defaultValue b.StartMs b.EndMs - b.StartMs) |> max 1)

        [ { Label = "Offense"; Value = "total damage"; Count = 1; Total = offense }
          { Label = "Duration"; Value = "ms in fights"; Count = snap.Battles.Length; Total = durationMs }
          { Label = "DPS"; Value = "approx"; Count = 1; Total = if durationMs > 0 then offense * 1000 / durationMs else 0 } ]

    let wsRates (snap: AnalyticsSnapshot) (filter: MobFilter) =
        filterInteractions snap filter (fun i ->
            i.InteractionType = InteractionType.Harm
            && i.Category = InteractionCategory.Weaponskill)
        |> List.groupBy (fun i -> i.ActionName)
        |> List.map (fun (ws, rows) ->
            { Label = ws; Value = "ws"; Count = rows.Length; Total = rows |> List.sumBy (fun r -> r.Value) })

    let skillchain (snap: AnalyticsSnapshot) (filter: MobFilter) =
        filterInteractions snap filter (fun i -> i.Category = InteractionCategory.Skillchain)
        |> List.groupBy (fun i -> i.ActionName)
        |> List.map (fun (action, rows) ->
            { Label = action; Value = "skillchain"; Count = rows.Length; Total = rows |> List.sumBy (fun r -> r.Value) })

    let extraAttacks (snap: AnalyticsSnapshot) (filter: MobFilter) =
        filterInteractions snap filter (fun i ->
            i.InteractionType = InteractionType.Harm
            && i.IsLocalPlayerActor
            && (i.Category = InteractionCategory.Melee || i.Category = InteractionCategory.MeleeCrit))
        |> List.groupBy (fun i -> i.ActorName)
        |> List.map (fun (actor, rows) ->
            { Label = actor; Value = "swings"; Count = rows.Length; Total = rows |> List.sumBy (fun r -> r.Value) })

    let additionalEffects (snap: AnalyticsSnapshot) (filter: MobFilter) =
        filterInteractions snap filter (fun i -> i.IsProc && i.ProcValue > 0)
        |> List.groupBy (fun i -> i.ActionName)
        |> List.map (fun (action, rows) ->
            { Label = action; Value = "proc"; Count = rows.Length; Total = rows |> List.sumBy (fun r -> r.ProcValue) })

    let timelineBuffs (snap: AnalyticsSnapshot) (filter: MobFilter) =
        filterInteractions snap filter (fun i -> i.AidType = Some AidType.Enhance)
        |> List.sortBy (fun i -> i.TimestampMs)
        |> List.map (fun i ->
            { Label = i.ActionName; Value = $"{i.TimestampMs}ms"; Count = 1; Total = int i.TimestampMs })

    let defenseByTime (snap: AnalyticsSnapshot) (filter: MobFilter) =
        filterInteractions snap filter (fun i ->
            i.AidType = Some AidType.Enhance
            && (i.IsLocalPlayerActor || i.IsLocalPlayerTarget)
            && BattleMessageCatalog.isDefensiveBuff i.ActionName)
        |> List.sortBy (fun i -> i.TimestampMs)
        |> List.map (fun i ->
            { Label = i.ActionName; Value = $"{i.TimestampMs}ms"; Count = 1; Total = int i.TimestampMs })

    let thiefStats (snap: AnalyticsSnapshot) (filter: MobFilter) =
        filterInteractions snap filter (fun i ->
            i.IsLocalPlayerActor
            && (BattleMessageCatalog.isThiefAction i.ActionName
                || i.Category = InteractionCategory.Weaponskill))
        |> List.groupBy (fun i -> i.ActionName)
        |> List.map (fun (action, rows) ->
            { Label = action
              Value = "thief"
              Count = rows.Length
              Total = rows |> List.sumBy (fun r -> r.Value) })

    let corsairRolls (snap: AnalyticsSnapshot) (filter: MobFilter) =
        filterInteractions snap filter (fun i ->
            i.IsLocalPlayerActor && BattleMessageCatalog.isCorsairRoll i.ActionName)
        |> List.groupBy (fun i -> i.ActionName)
        |> List.map (fun (action, rows) ->
            { Label = action; Value = "roll"; Count = rows.Length; Total = rows.Length })

    let abysseaChests (snap: AnalyticsSnapshot) =
        snap.Battles
        |> List.filter (fun b -> b.Killed && b.ExperiencePoints > 0)
        |> List.map (fun b ->
            { Label = b.EnemyName
              Value = $"#{b.Id} xp={b.ExperiencePoints} chain={b.ExperienceChain}"
              Count = 1
              Total = b.ExperiencePoints })

    let players (snap: AnalyticsSnapshot) =
        snap.Combatants
        |> List.filter (fun c -> c.Kind = EntityKind.Player)
        |> List.map (fun c ->
            { Label = c.Name
              Value = if String.IsNullOrWhiteSpace c.Job then Option.defaultValue "" c.PlayerInfo else c.Job
              Count = 1
              Total = int c.Id })

    let mobs (snap: AnalyticsSnapshot) =
        snap.Battles
        |> List.map (fun b -> b.EnemyName)
        |> List.distinct
        |> List.map (fun name -> { Label = name; Value = "mob"; Count = 1; Total = MobXpLookup.tryGetXp name |> Option.defaultValue 0 })
