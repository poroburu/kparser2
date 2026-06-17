namespace kparser2.Analytics

open System
open kparser2.Decoders

module ReportAggregators =
    let private matchesMob (filter: MobFilter) (battle: Battle) =
        if filter.ExcludeZeroXp && not (MobXpLookup.hasXp battle.EnemyName) then
            false
        else
            match filter.SelectedBattleId with
            | Some id -> battle.Id = id
            | None ->
                match filter.SelectedMobName with
                | Some name when filter.GroupMobs ->
                    battle.EnemyName.Equals(name, StringComparison.OrdinalIgnoreCase)
                | Some name -> battle.EnemyName = name
                | None -> true

    let filterBattles (snap: AnalyticsSnapshot) (filter: MobFilter) =
        snap.Battles |> List.filter (matchesMob filter)

    let filterInteractions (snap: AnalyticsSnapshot) (filter: MobFilter) (predicate: Interaction -> bool) =
        snap.Interactions
        |> List.map (fun i ->
            { i with
                ActorName = EntityRegistry.formatEntity i.ActorId
                TargetName = EntityRegistry.formatEntity i.TargetId })
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
                i.ActorName.Equals(name, StringComparison.OrdinalIgnoreCase)
                || i.TargetName.Equals(name, StringComparison.OrdinalIgnoreCase)
                || snap.Combatants
                   |> List.tryFind (fun c -> c.Id = i.ActorId || c.Id = i.TargetId)
                   |> Option.map (fun c -> c.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                   |> Option.defaultValue false)

    let playerNames (snap: AnalyticsSnapshot) =
        snap.Combatants
        |> List.filter (fun c -> c.Kind = EntityKind.Player)
        |> List.map (fun c -> c.Name)
        |> List.distinct

    let private isHit (success: string) =
        not (success.Contains("miss", StringComparison.OrdinalIgnoreCase)
             || success.Contains("evad", StringComparison.OrdinalIgnoreCase)
             || success.Contains("fail", StringComparison.OrdinalIgnoreCase))

    let private isMiss (success: string) = not (isHit success)

    type CategoryDamage =
        { Melee: int
          MeleeCrit: int
          Ranged: int
          RangedCrit: int
          Ability: int
          Weaponskill: int
          Spell: int
          Skillchain: int
          OtherPhysical: int
          OtherMagical: int
          Other: int }

    let emptyCategoryDamage =
        { Melee = 0
          MeleeCrit = 0
          Ranged = 0
          RangedCrit = 0
          Ability = 0
          Weaponskill = 0
          Spell = 0
          Skillchain = 0
          OtherPhysical = 0
          OtherMagical = 0
          Other = 0 }

    let addCategoryDamage category value dmg =
        match category with
        | InteractionCategory.Melee -> { dmg with Melee = dmg.Melee + value }
        | InteractionCategory.MeleeCrit -> { dmg with MeleeCrit = dmg.MeleeCrit + value }
        | InteractionCategory.Ranged -> { dmg with Ranged = dmg.Ranged + value }
        | InteractionCategory.RangedCrit -> { dmg with RangedCrit = dmg.RangedCrit + value }
        | InteractionCategory.Ability -> { dmg with Ability = dmg.Ability + value }
        | InteractionCategory.Weaponskill -> { dmg with Weaponskill = dmg.Weaponskill + value }
        | InteractionCategory.Spell -> { dmg with Spell = dmg.Spell + value }
        | InteractionCategory.Skillchain -> { dmg with Skillchain = dmg.Skillchain + value }
        | InteractionCategory.OtherPhysical -> { dmg with OtherPhysical = dmg.OtherPhysical + value }
        | InteractionCategory.OtherMagical -> { dmg with OtherMagical = dmg.OtherMagical + value }
        | _ -> { dmg with Other = dmg.Other + value }

    type HitStats =
        { Hits: int
          Misses: int
          TotalDmg: int
          MinDmg: int
          MaxDmg: int
          CritHits: int
          CritDmg: int
          CritMin: int
          CritMax: int
          ZeroHits: int
          Failures: int
          MagicBursts: int }

    let emptyHitStats =
        { Hits = 0
          Misses = 0
          TotalDmg = 0
          MinDmg = Int32.MaxValue
          MaxDmg = 0
          CritHits = 0
          CritDmg = 0
          CritMin = Int32.MaxValue
          CritMax = 0
          ZeroHits = 0
          Failures = 0
          MagicBursts = 0 }

    let addHitStats (i: Interaction) stats =
        let value = max 0 i.Value
        let isCrit = i.DamageModifier = DamageModifier.Critical
        let isBurst = i.DamageModifier = DamageModifier.MagicBurst

        let stats =
            if isHit i.Success && value > 0 then
                { stats with
                    Hits = stats.Hits + 1
                    TotalDmg = stats.TotalDmg + value
                    MinDmg = min stats.MinDmg value
                    MaxDmg = max stats.MaxDmg value }
            elif isMiss i.Success then
                { stats with Misses = stats.Misses + 1 }
            elif value = 0 && isHit i.Success then
                { stats with ZeroHits = stats.ZeroHits + 1 }
            else
                { stats with Failures = stats.Failures + 1 }

        let stats =
            if isCrit && value > 0 then
                { stats with
                    CritHits = stats.CritHits + 1
                    CritDmg = stats.CritDmg + value
                    CritMin = min stats.CritMin value
                    CritMax = max stats.CritMax value }
            else
                stats

        if isBurst then
            { stats with MagicBursts = stats.MagicBursts + 1 }
        else
            stats

    type PlayerOffenseStats =
        { Name: string
          Categories: CategoryDamage
          Melee: HitStats
          Ranged: HitStats
          Ability: HitStats
          Weaponskill: HitStats
          Spell: HitStats
          MeleeCrit: HitStats
          Skillchains: Map<string, HitStats> }

    let emptyPlayerOffense name =
        { Name = name
          Categories = emptyCategoryDamage
          Melee = emptyHitStats
          Ranged = emptyHitStats
          Ability = emptyHitStats
          Weaponskill = emptyHitStats
          Spell = emptyHitStats
          MeleeCrit = emptyHitStats
          Skillchains = Map.empty }

    let offenseInteractions (snap: AnalyticsSnapshot) (filter: MobFilter) =
        filterInteractions snap filter (fun i ->
            i.InteractionType = InteractionType.Harm && i.Value >= 0)

    let defenseInteractions (snap: AnalyticsSnapshot) (filter: MobFilter) =
        filterInteractions snap filter (fun i ->
            i.InteractionType = InteractionType.Harm && i.Value >= 0)

    let private isPerformanceParticipant (snap: AnalyticsSnapshot) (name: string) =
        match
            snap.Combatants
            |> List.tryFind (fun c -> c.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
        with
        | Some c -> c.Kind = EntityKind.Player || c.Kind = EntityKind.Pet || c.Kind = EntityKind.Fellow
        | None ->
            not (
                snap.Battles
                |> List.exists (fun b -> b.EnemyName.Equals(name, StringComparison.OrdinalIgnoreCase))
            )

    let buildPlayerOffense (snap: AnalyticsSnapshot) (filter: MobFilter) =
        offenseInteractions snap filter
        |> List.filter (fun i -> isPerformanceParticipant snap i.ActorName)
        |> List.groupBy (fun i -> i.ActorName)
        |> List.map (fun (name, rows) ->
            let mutable stats = emptyPlayerOffense name

            for i in rows do
                let value = max 0 i.Value
                stats <-
                    { stats with
                        Categories = addCategoryDamage i.Category value stats.Categories }

                stats <-
                    match i.Category with
                    | InteractionCategory.Melee -> { stats with Melee = addHitStats i stats.Melee }
                    | InteractionCategory.MeleeCrit -> { stats with MeleeCrit = addHitStats i stats.MeleeCrit }
                    | InteractionCategory.Ranged | InteractionCategory.RangedCrit ->
                        { stats with Ranged = addHitStats i stats.Ranged }
                    | InteractionCategory.Ability -> { stats with Ability = addHitStats i stats.Ability }
                    | InteractionCategory.Weaponskill -> { stats with Weaponskill = addHitStats i stats.Weaponskill }
                    | InteractionCategory.Spell -> { stats with Spell = addHitStats i stats.Spell }
                    | InteractionCategory.Skillchain ->
                        let key = i.ActionName
                        let existing = stats.Skillchains |> Map.tryFind key |> Option.defaultValue emptyHitStats
                        { stats with Skillchains = stats.Skillchains |> Map.add key (addHitStats i existing) }
                    | _ -> stats

            stats)
        |> List.sortBy (fun s -> s.Name)

    type PlayerDefenseStats =
        { Name: string
          Categories: CategoryDamage
          Melee: HitStats
          Ranged: HitStats
          Ability: HitStats
          Weaponskill: HitStats
          Spell: HitStats
          Skillchains: Map<string, HitStats> }

    let emptyPlayerDefense name =
        { Name = name
          Categories = emptyCategoryDamage
          Melee = emptyHitStats
          Ranged = emptyHitStats
          Ability = emptyHitStats
          Weaponskill = emptyHitStats
          Spell = emptyHitStats
          Skillchains = Map.empty }

    let buildPlayerDefense (snap: AnalyticsSnapshot) (filter: MobFilter) =
        defenseInteractions snap filter
        |> List.groupBy (fun i -> i.TargetName)
        |> List.map (fun (name, rows) ->
            let mutable stats = emptyPlayerDefense name

            for i in rows do
                let value = max 0 i.Value
                stats <-
                    { stats with
                        Categories = addCategoryDamage i.Category value stats.Categories }

                stats <-
                    match i.Category with
                    | InteractionCategory.Melee | InteractionCategory.MeleeCrit ->
                        { stats with Melee = addHitStats i stats.Melee }
                    | InteractionCategory.Ranged | InteractionCategory.RangedCrit ->
                        { stats with Ranged = addHitStats i stats.Ranged }
                    | InteractionCategory.Ability -> { stats with Ability = addHitStats i stats.Ability }
                    | InteractionCategory.Weaponskill -> { stats with Weaponskill = addHitStats i stats.Weaponskill }
                    | InteractionCategory.Spell -> { stats with Spell = addHitStats i stats.Spell }
                    | InteractionCategory.Skillchain ->
                        let key = i.ActionName
                        let existing = stats.Skillchains |> Map.tryFind key |> Option.defaultValue emptyHitStats
                        { stats with Skillchains = stats.Skillchains |> Map.add key (addHitStats i existing) }
                    | _ -> stats

            stats)
        |> List.sortBy (fun s -> s.Name)

    let totalCategoryDamage (c: CategoryDamage) =
        c.Melee + c.MeleeCrit + c.Ranged + c.RangedCrit + c.Ability + c.Weaponskill + c.Spell + c.Skillchain + c.OtherPhysical + c.OtherMagical + c.Other

    let meleeTotal (c: CategoryDamage) = c.Melee + c.MeleeCrit
    let rangedTotal (c: CategoryDamage) = c.Ranged + c.RangedCrit
    let otherTotal (c: CategoryDamage) = c.OtherPhysical + c.OtherMagical + c.Other

    let hitMissString (stats: HitStats) =
        sprintf "%d/%d" stats.Hits stats.Misses

    let lowHighString minVal maxVal =
        if minVal = Int32.MaxValue then "0/0" else sprintf "%d/%d" minVal maxVal

    let avgNonCrit (stats: HitStats) =
        let nonCritHits = stats.Hits - stats.CritHits - stats.ZeroHits

        if nonCritHits > 0 then
            float (stats.TotalDmg - stats.CritDmg) / float nonCritHits
        else
            0.0

    let avgCrit (stats: HitStats) =
        if stats.CritHits > 0 then float stats.CritDmg / float stats.CritHits else 0.0

    let hitRate (stats: HitStats) =
        let total = stats.Hits + stats.Misses
        if total > 0 then float stats.Hits / float total else 0.0

    let critRate (stats: HitStats) =
        if stats.Hits > 0 then float stats.CritHits / float stats.Hits else 0.0

    type FightRow =
        { Number: int
          Enemy: string
          Killed: string
          Killer: string
          StartTime: string
          EndTime: string
          Length: string
          Exp: int
          Chain: int }

    let buildFightRows (snap: AnalyticsSnapshot) (filter: MobFilter) =
        filterBattles snap filter
        |> List.sortBy (fun b -> b.StartMs)
        |> List.mapi (fun idx b ->
            let endMs = b.EndMs |> Option.defaultValue b.StartMs
            let lengthMs = max 0L (endMs - b.StartMs)

            let killer =
                b.KillerId
                |> Option.map (fun id ->
                    snap.Combatants
                    |> List.tryFind (fun c -> c.Id = id)
                    |> Option.map (fun c -> c.Name)
                    |> Option.defaultWith (fun () -> EntityRegistry.formatEntity id))
                |> Option.defaultValue "-"

            let startTime =
                DateTimeOffset.FromUnixTimeMilliseconds(b.StartMs + snap.SessionStartMs).LocalDateTime

            let endTime =
                DateTimeOffset.FromUnixTimeMilliseconds(endMs + snap.SessionStartMs).LocalDateTime

            { Number = idx + 1
              Enemy = b.EnemyName
              Killed = if b.Killed then "Yes" else "No"
              Killer = killer
              StartTime = startTime.ToString("HH:mm:ss")
              EndTime = endTime.ToString("HH:mm:ss")
              Length = TimeSpanFormat.formatMs lengthMs false
              Exp = b.ExperiencePoints
              Chain = b.ExperienceChain })

    type PlayerParticipation =
        { Name: string
          FightCount: int
          FightParticipation: float
          TimeFightingMs: int64
          TotalFightLengthMs: int64
          AvgTimePerFightSec: float
          FightTimePercent: float
          OverallTimePercent: float
          MeleeDps: float
          RangedDps: float
          WsDps: float
          MagicDps: float
          OtherDps: float
          TotalDps: float }

    let buildPerformance (snap: AnalyticsSnapshot) (filter: MobFilter) =
        let battles = filterBattles snap filter
        let totalFightMs = battles |> List.sumBy (fun b -> let e = b.EndMs |> Option.defaultValue b.StartMs in max 0L (e - b.StartMs))
        let numBattles = battles.Length
        let offense = buildPlayerOffense snap filter

        let playerFightTimes =
            offense
            |> List.map (fun p ->
                let playerBattles =
                    battles
                    |> List.filter (fun b ->
                        snap.Interactions
                        |> List.exists (fun i ->
                            i.BattleId = Some b.Id
                            && i.ActorName.Equals(p.Name, StringComparison.OrdinalIgnoreCase)
                            && i.InteractionType = InteractionType.Harm))

                let timeMs =
                    playerBattles
                    |> List.sumBy (fun b ->
                        let e = b.EndMs |> Option.defaultValue b.StartMs
                        max 0L (e - b.StartMs))

                let fightLengthMs =
                    playerBattles
                    |> List.sumBy (fun b ->
                        let e = b.EndMs |> Option.defaultValue b.StartMs
                        max 0L (e - b.StartMs))

                p.Name, playerBattles.Length, timeMs, fightLengthMs)
            |> List.map (fun (name, fc, tf, fl) -> name, (fc, tf, fl))
            |> Map.ofList

        offense
        |> List.map (fun p ->
            let totalDmg = float (totalCategoryDamage p.Categories)
            let durationSec = if totalFightMs > 0L then float totalFightMs / 1000.0 else 1.0

            let fightCount, timeFighting, fightLengths =
                match playerFightTimes |> Map.tryFind p.Name with
                | Some (fc, tf, fl) -> fc, tf, fl
                | None -> 0, 0L, 0L

            let partPct = if numBattles > 0 then float fightCount / float numBattles else 0.0
            let fightTimePct = if totalFightMs > 0L then float timeFighting / float totalFightMs else 0.0
            let overallPct = fightTimePct

            { Name = p.Name
              FightCount = fightCount
              FightParticipation = partPct
              TimeFightingMs = timeFighting
              TotalFightLengthMs = fightLengths
              AvgTimePerFightSec = if fightCount > 0 then float timeFighting / float fightCount / 1000.0 else 0.0
              FightTimePercent = fightTimePct
              OverallTimePercent = overallPct
              MeleeDps = float (meleeTotal p.Categories) / durationSec
              RangedDps = float (rangedTotal p.Categories) / durationSec
              WsDps = float p.Categories.Weaponskill / durationSec
              MagicDps = float p.Categories.Spell / durationSec
              OtherDps = float (otherTotal p.Categories) / durationSec
              TotalDps = totalDmg / durationSec })

    type BuffStat =
        { Buff: string
          Target: string
          Count: int
          MinIntervalMs: int64
          MaxIntervalMs: int64
          AvgIntervalMs: int64 }

    let frequencyBuckets (interactions: Interaction list) =
        interactions
        |> List.filter (fun i -> i.Value > 0)
        |> List.groupBy (fun i -> i.Value)
        |> List.sortBy fst
        |> List.map (fun (amount, rows) -> amount, rows.Length)

    let recoveryByPlayer (snap: AnalyticsSnapshot) (filter: MobFilter) =
        filterInteractions snap filter (fun i ->
            i.InteractionType = InteractionType.Aid
            && i.AidType = Some AidType.Recovery
            && i.Value > 0)
        |> List.groupBy (fun i -> i.ActorName)
        |> List.map (fun (name, rows) ->
            let byAction = rows |> List.groupBy (fun i -> i.ActionName)

            let actions =
                byAction |> List.map (fun (a, rs) -> a, rs.Length, rs |> List.sumBy (fun r -> r.Value))

            (name, (rows |> List.sumBy (fun r -> r.Value)), actions))
        |> List.sortBy (fun (name, _, _) -> name)

    let buffStats (snap: AnalyticsSnapshot) (filter: MobFilter) : BuffStat list =
        filterInteractions snap filter (fun i ->
            i.InteractionType = InteractionType.Aid && i.AidType = Some AidType.Enhance)
        |> List.groupBy (fun i -> i.ActionName, i.TargetName)
        |> List.map (fun ((buff, target), rows) ->
            let times = rows |> List.sortBy (fun r -> r.TimestampMs) |> List.map (fun r -> r.TimestampMs)

            let intervals =
                if times.Length > 1 then
                    times
                    |> List.pairwise
                    |> List.map (fun (a, b) -> b - a)
                else
                    []

            let minInterval = if intervals.Length > 0 then intervals |> List.min else 0L
            let maxInterval = if intervals.Length > 0 then intervals |> List.max else 0L
            let avgInterval = if intervals.Length > 0 then intervals |> List.averageBy float |> int64 else 0L

            { Buff = buff
              Target = target
              Count = rows.Length
              MinIntervalMs = minInterval
              MaxIntervalMs = maxInterval
              AvgIntervalMs = avgInterval })

    let debuffStats (snap: AnalyticsSnapshot) (filter: MobFilter) =
        filterInteractions snap filter (fun i ->
            i.InteractionType = InteractionType.Harm && i.HarmType = Some HarmType.Enfeeble)
        |> List.groupBy (fun i -> i.ActionName, i.TargetName)
        |> List.map (fun ((debuff, target), rows) ->
            let successful = rows |> List.filter (fun r -> isHit r.Success) |> List.length
            let noEffect = rows.Length - successful
            debuff, target, rows.Length, successful, noEffect)
