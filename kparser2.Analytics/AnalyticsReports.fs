namespace kparser2.Analytics

open System

module private ReportHelpers =
    let safeMin minVal = if minVal = Int32.MaxValue then 0 else minVal

    let formatSection title header formatFn rows report =
        let report = ReportBuilder.appendTitle title report
        let report = ReportBuilder.appendHeader header report

        rows
        |> List.fold (fun r line -> ReportBuilder.appendFormatLine formatFn line r) report
        |> ReportBuilder.blankLine

module ItemsReport =
    let format (snap: AnalyticsSnapshot) (_filter: MobFilter) =
        let rows =
            snap.ItemUses
            |> List.groupBy (fun u -> u.ItemName)
            |> List.map (fun (item, uses) -> item, uses |> List.sumBy (fun u -> u.Quantity))
            |> List.sortByDescending snd

        let report = ReportBuilder.empty |> ReportBuilder.appendHeader ReportTemplates.Items.generalHeader

        rows
        |> List.fold
            (fun r (item, qty) ->
                r
                |> ReportBuilder.appendFormatLine "{0,-36}{1,6}" [| box item; box qty |])
            report

module PlayersReport =
    let format (snap: AnalyticsSnapshot) (_filter: MobFilter) =
        let players =
            snap.Combatants
            |> List.filter (fun c -> c.Kind = EntityKind.Player || c.Kind = EntityKind.Pet || c.Kind = EntityKind.Fellow)

        if players.IsEmpty then
            ReportBuilder.empty |> ReportBuilder.appendLine "No player information available."
        else
            players
            |> List.fold
                (fun r c ->
                    let info =
                        if String.IsNullOrWhiteSpace c.Job then
                            Option.defaultValue "" c.PlayerInfo
                        else
                            c.Job

                    r
                    |> ReportBuilder.appendLine $"{c.Name}"
                    |> ReportBuilder.appendLine $"  {info}"
                    |> ReportBuilder.blankLine)
                ReportBuilder.empty

module ChatSummaryReport =
    let format (snap: AnalyticsSnapshot) modeFilter speakerFilter =
        let modeOpt = if modeFilter = Some "All" then None else modeFilter
        let speakerOpt = if speakerFilter = Some "All" then None else speakerFilter

        let rows =
            snap.ChatMessages
            |> List.filter (fun c ->
                match modeOpt with
                | Some m -> c.Mode.Equals(m, StringComparison.OrdinalIgnoreCase)
                | None -> true)
            |> List.filter (fun c ->
                match speakerOpt with
                | Some s -> c.Speaker.Equals(s, StringComparison.OrdinalIgnoreCase)
                | None -> true)
            |> List.groupBy (fun c -> c.Mode, c.Speaker)
            |> List.map (fun ((mode, speaker), msgs) -> mode, speaker, msgs.Length)
            |> List.sortBy (fun (m, s, _) -> m, s)

        let report =
            ReportBuilder.empty
            |> ReportBuilder.appendTitle "Chat Summary"
            |> ReportBuilder.appendHeader ReportTemplates.Chat.summaryHeader

        rows
        |> List.fold
            (fun r (mode, speaker, count) ->
                r |> ReportBuilder.appendFormatLine ReportTemplates.Chat.summaryFormat [| box mode; box speaker; box count |])
            report

module ChatReport =
    let private modeColor (mode: string) =
        match mode.ToLowerInvariant() with
        | "say" -> ReportColors.gray
        | "shout" | "yell" -> ReportColors.orange
        | "tell" -> ReportColors.magenta
        | "party" -> ReportColors.blue
        | "linkshell" -> ReportColors.green
        | "emote" -> ReportColors.indigo
        | _ -> ReportColors.black

    let format (snap: AnalyticsSnapshot) modeFilter speakerFilter =
        let modeOpt = if modeFilter = Some "All" then None else modeFilter
        let speakerOpt = if speakerFilter = Some "All" then None else speakerFilter

        snap.ChatMessages
        |> List.filter (fun c ->
            match modeOpt with
            | Some m -> c.Mode.Equals(m, StringComparison.OrdinalIgnoreCase)
            | None -> true)
        |> List.filter (fun c ->
            match speakerOpt with
            | Some s -> c.Speaker.Equals(s, StringComparison.OrdinalIgnoreCase)
            | None -> true)
        |> List.sortBy (fun c -> c.TimestampMs)
        |> List.fold
            (fun report msg ->
                let time =
                    DateTimeOffset.FromUnixTimeMilliseconds(msg.TimestampMs + snap.SessionStartMs).LocalDateTime.ToString("T")

                let prefix = $"[{time}] "
                let text = if msg.IsLocalPlayer then msg.Message else msg.Message

                report
                |> ReportBuilder.appendStyled prefix false false ReportColors.purple
                |> ReportBuilder.appendStyled (text + "\n") false false (modeColor msg.Mode))
            ReportBuilder.empty

module FightsReport =
    let format (snap: AnalyticsSnapshot) (filter: MobFilter) =
        let rows = ReportAggregators.buildFightRows snap filter

        if rows.IsEmpty then
            ReportBuilder.empty
        else
            let report = ReportBuilder.empty |> ReportBuilder.appendHeader ReportTemplates.Fights.fightHeader

            rows
            |> List.fold
                (fun r row ->
                    r
                    |> ReportBuilder.appendFormatLine
                        ReportTemplates.Fights.fightFormat
                        [| box row.Number
                           box row.Enemy
                           box row.Killed
                           box row.Killer
                           box row.StartTime
                           box row.EndTime
                           box row.Length
                           box row.Exp
                           box row.Chain |])
                report

module DeathsReport =
    let format (snap: AnalyticsSnapshot) (filter: MobFilter) =
        let deaths =
            ReportAggregators.filterInteractions snap filter (fun i -> i.InteractionType = InteractionType.Death)

        if deaths.IsEmpty then
            ReportBuilder.empty |> ReportBuilder.appendTitle ReportTemplates.Death.title
        else
            let summary =
                deaths
                |> List.groupBy (fun d -> d.ActorName)
                |> List.map (fun (name, rows) -> name, rows.Length)
                |> List.sortBy fst

            let report =
                ReportBuilder.empty
                |> ReportBuilder.appendTitle ReportTemplates.Death.title
                |> ReportBuilder.appendTitle ReportTemplates.Death.summaryTitle
                |> ReportBuilder.appendHeader ReportTemplates.Death.summaryHeader

            let report =
                summary
                |> List.fold
                    (fun r (name, count) ->
                        r |> ReportBuilder.appendFormatLine ReportTemplates.Death.summaryFormat [| box name; box count |])
                    report
                |> ReportBuilder.blankLine
                |> ReportBuilder.appendTitle ReportTemplates.Death.detailsTitle
                |> ReportBuilder.appendHeader ReportTemplates.Death.detailsHeader

            deaths
            |> List.sortBy (fun d -> d.TimestampMs)
            |> List.fold
                (fun r d ->
                    let time =
                        DateTimeOffset.FromUnixTimeMilliseconds(d.TimestampMs + snap.SessionStartMs).LocalDateTime.ToString("G")

                    r
                    |> ReportBuilder.appendFormatLine
                        ReportTemplates.Death.detailsFormat
                        [| box d.ActorName; box time; box d.TargetName |])
                report

module OffenseReport =
    let private formatSummary (offensePlayers: ReportAggregators.PlayerOffenseStats list) report =
        let totalAll = offensePlayers |> List.sumBy (fun p -> ReportAggregators.totalCategoryDamage p.Categories)

        let report =
            report
            |> ReportBuilder.appendTitle ReportTemplates.Offense.titleSummary
            |> ReportBuilder.appendHeader ReportTemplates.Offense.headerSummary

        let report =
            offensePlayers
            |> List.fold
                (fun r (p: ReportAggregators.PlayerOffenseStats) ->
                    let c = p.Categories
                    let total = ReportAggregators.totalCategoryDamage c
                    let pct = if totalAll > 0 then float total / float totalAll else 0.0

                    r
                    |> ReportBuilder.appendFormatLine
                        ReportTemplates.Offense.formatSummary
                        [| box p.Name
                           box total
                           box pct
                           box (ReportAggregators.meleeTotal c)
                           box (ReportAggregators.rangedTotal c)
                           box c.Ability
                           box c.Weaponskill
                           box c.Spell
                           box (ReportAggregators.otherTotal c)
                           box 0 |])
                report

        if offensePlayers.Length > 1 then
            let c =
                offensePlayers
                |> List.fold
                    (fun (acc: ReportAggregators.CategoryDamage) (p: ReportAggregators.PlayerOffenseStats) ->
                        { acc with
                            Melee = acc.Melee + p.Categories.Melee + p.Categories.MeleeCrit
                            Ranged = acc.Ranged + p.Categories.Ranged + p.Categories.RangedCrit
                            Ability = acc.Ability + p.Categories.Ability
                            Weaponskill = acc.Weaponskill + p.Categories.Weaponskill
                            Spell = acc.Spell + p.Categories.Spell
                            OtherPhysical = acc.OtherPhysical + p.Categories.OtherPhysical
                            OtherMagical = acc.OtherMagical + p.Categories.OtherMagical
                            Other = acc.Other + p.Categories.Other })
                    ReportAggregators.emptyCategoryDamage

            let total = ReportAggregators.totalCategoryDamage c

            report
            |> ReportBuilder.appendFormatLine
                ReportTemplates.Offense.formatSummary
                [| box ReportTemplates.Public.total
                   box total
                   box 1.0
                   box (c.Melee + c.MeleeCrit)
                   box (c.Ranged + c.RangedCrit)
                   box c.Ability
                   box c.Weaponskill
                   box c.Spell
                   box (c.OtherPhysical + c.OtherMagical + c.Other)
                   box 0 |]
        else
            report

    let private formatMeleeSection (offensePlayers: ReportAggregators.PlayerOffenseStats list) report =
        let report =
            report
            |> ReportBuilder.appendTitle ReportTemplates.Offense.titleMelee
            |> ReportBuilder.appendHeader ReportTemplates.Offense.headerMelee

        offensePlayers
        |> List.filter (fun (p: ReportAggregators.PlayerOffenseStats) -> p.Melee.Hits + p.Melee.Misses > 0)
        |> List.fold
            (fun r (p: ReportAggregators.PlayerOffenseStats) ->
                let c = p.Categories
                let meleeDmg = c.Melee
                let total = ReportAggregators.totalCategoryDamage c
                let pct = if total > 0 then float meleeDmg / float total else 0.0
                let s = p.Melee

                r
                |> ReportBuilder.appendFormatLine
                    ReportTemplates.Offense.formatMelee
                    [| box p.Name
                       box meleeDmg
                       box 0
                       box meleeDmg
                       box pct
                       box (ReportAggregators.hitMissString s)
                       box (ReportAggregators.hitRate s)
                       box (ReportAggregators.hitRate s)
                       box (ReportAggregators.lowHighString (ReportHelpers.safeMin s.MinDmg) s.MaxDmg)
                       box (ReportAggregators.avgNonCrit s)
                       box (ReportAggregators.avgNonCrit s) |])
            report
        |> ReportBuilder.blankLine

    let format (snap: AnalyticsSnapshot) (filter: MobFilter) =
        let offensePlayers = ReportAggregators.buildPlayerOffense snap filter

        if offensePlayers.IsEmpty then
            ReportBuilder.empty
        else
            ReportBuilder.empty
            |> formatSummary offensePlayers
            |> ReportBuilder.blankLine
            |> formatMeleeSection offensePlayers

module DefenseReport =
    let format (snap: AnalyticsSnapshot) (filter: MobFilter) =
        let players = ReportAggregators.buildPlayerDefense snap filter

        if players.IsEmpty then
            ReportBuilder.empty
        else
            let totalAll = players |> List.sumBy (fun p -> ReportAggregators.totalCategoryDamage p.Categories)

            let report =
                ReportBuilder.empty
                |> ReportBuilder.appendTitle ReportTemplates.Defense.titleSummary
                |> ReportBuilder.appendHeader ReportTemplates.Defense.headerSummary

            players
            |> List.fold
                (fun r p ->
                    let c = p.Categories
                    let total = ReportAggregators.totalCategoryDamage c
                    let pct = if totalAll > 0 then float total / float totalAll else 0.0

                    r
                    |> ReportBuilder.appendFormatLine
                        ReportTemplates.Defense.formatSummary
                        [| box p.Name
                           box total
                           box pct
                           box (ReportAggregators.meleeTotal c)
                           box (ReportAggregators.rangedTotal c)
                           box c.Ability
                           box c.Weaponskill
                           box c.Spell
                           box (ReportAggregators.otherTotal c) |])
                report

module PerformanceReport =
    let format (snap: AnalyticsSnapshot) (filter: MobFilter) =
        let battles = ReportAggregators.filterBattles snap filter
        let totalFightMs = battles |> List.sumBy (fun b -> let e = b.EndMs |> Option.defaultValue b.StartMs in max 0L (e - b.StartMs))
        let players = ReportAggregators.buildPerformance snap filter

        if players.IsEmpty then
            ReportBuilder.empty
        else
            let report =
                ReportBuilder.empty
                |> ReportBuilder.appendTitle ReportTemplates.Performance.overallTitle
                |> ReportBuilder.appendHeader ReportTemplates.Performance.overallHeader
                |> ReportBuilder.appendFormatLine
                    ReportTemplates.Performance.overallFormat
                    [| box battles.Length; box (TimeSpanFormat.formatMs totalFightMs false) |]
                |> ReportBuilder.blankLine
                |> ReportBuilder.appendTitle ReportTemplates.Performance.participateTitle
                |> ReportBuilder.appendHeader ReportTemplates.Performance.participateFightsHeader

            let report =
                players
                |> List.fold
                    (fun r p ->
                        r
                        |> ReportBuilder.appendFormatLine
                            ReportTemplates.Performance.participateFightsFormat
                            [| box p.Name; box p.FightCount; box p.FightParticipation |])
                    report
                |> ReportBuilder.blankLine
                |> ReportBuilder.appendHeader ReportTemplates.Performance.participateTimeHeader

            let report =
                players
                |> List.fold
                    (fun r p ->
                        r
                        |> ReportBuilder.appendFormatLine
                            ReportTemplates.Performance.participateTimeFormat
                            [| box p.Name
                               box (TimeSpanFormat.formatMs p.TimeFightingMs false)
                               box (TimeSpanFormat.formatMs p.TotalFightLengthMs false)
                               box (TimeSpanFormat.formatMs (int64 (p.AvgTimePerFightSec * 1000.0)) false)
                               box p.FightTimePercent
                               box p.OverallTimePercent |])
                    report
                |> ReportBuilder.blankLine
                |> ReportBuilder.appendTitle ReportTemplates.Performance.dpsTitle
                |> ReportBuilder.appendHeader ReportTemplates.Performance.dpsHeader

            players
            |> List.fold
                (fun r p ->
                    r
                    |> ReportBuilder.appendFormatLine
                        ReportTemplates.Performance.dpsFormat
                        [| box p.Name
                           box p.MeleeDps
                           box p.RangedDps
                           box p.WsDps
                           box p.MagicDps
                           box p.OtherDps
                           box p.TotalDps |])
                report

module RecoveryReport =
    let format (snap: AnalyticsSnapshot) (filter: MobFilter) =
        let rows = ReportAggregators.recoveryByPlayer snap filter

        if rows.IsEmpty then
            ReportBuilder.empty
        else
            let report =
                ReportBuilder.empty
                |> ReportBuilder.appendTitle ReportTemplates.Recovery.titleCuring
                |> ReportBuilder.appendHeader ReportTemplates.Recovery.headerCuring

            rows
            |> List.fold
                (fun r (name, total, actions) ->
                    let spellCure = actions |> List.filter (fun (a, _, _) -> a.Contains("Cure", StringComparison.OrdinalIgnoreCase)) |> List.sumBy (fun (_, _, v) -> v)
                    let abCure = total - spellCure

                    r
                    |> ReportBuilder.appendFormatLine
                        ReportTemplates.Recovery.formatCuring
                        [| box name; box spellCure; box abCure; box 0; box 0; box 0; box 0; box 0; box 0; box 0; box 0; box 0; box 0; box 0 |])
                report

module BuffsReport =
    let format (snap: AnalyticsSnapshot) (filter: MobFilter) =
        let rows = ReportAggregators.buffStats snap filter

        if rows.IsEmpty then
            ReportBuilder.empty
        else
            let report =
                ReportBuilder.empty
                |> ReportBuilder.appendTitle "Buffs Used"
                |> ReportBuilder.appendHeader ReportTemplates.Buff.usedHeader

            rows
            |> List.sortBy (fun r -> r.Buff, r.Target)
            |> List.fold
                (fun r row ->
                    r
                    |> ReportBuilder.appendFormatLine
                        "{0,-20}{1,-20}"
                        [| box row.Buff; box row.Target |]
                    |> ReportBuilder.appendFormatLine ReportTemplates.Buff.numTimesFormat [| box row.Count |]
                    |> ReportBuilder.appendFormatLine
                        ReportTemplates.Buff.intervalsFormat
                        [| box (TimeSpanFormat.formatMs row.MinIntervalMs false)
                           box (TimeSpanFormat.formatMs row.MaxIntervalMs false)
                           box (TimeSpanFormat.formatMs row.AvgIntervalMs false) |])
                report

module DebuffsReport =
    let format (snap: AnalyticsSnapshot) (filter: MobFilter) =
        let rows = ReportAggregators.debuffStats snap filter

        if rows.IsEmpty then
            ReportBuilder.empty
        else
            let report =
                ReportBuilder.empty
                |> ReportBuilder.appendTitle "Debuffs on Mobs"
                |> ReportBuilder.appendHeader ReportTemplates.Debuff.debuffWithTargetsHeader

            rows
            |> List.sortBy (fun (d, t, _, _, _) -> d, t)
            |> List.fold
                (fun r (debuff, target, times, successful, noEffect) ->
                    let pct = if times > 0 then float successful / float times else 0.0

                    r
                    |> ReportBuilder.appendFormatLine
                        "{0,-20}{1,-20}"
                        [| box debuff; box target |]
                    |> ReportBuilder.appendFormatLine
                        ReportTemplates.Debuff.mobDebuffFormat
                        [| box times; box successful; box noEffect; box pct |])
                report

module SkillchainReport =
    let format (snap: AnalyticsSnapshot) (filter: MobFilter) =
        let players = ReportAggregators.buildPlayerOffense snap filter

        let scRows =
            players
            |> List.collect (fun p ->
                p.Skillchains
                |> Map.toList
                |> List.map (fun (name, stats) -> p.Name, name, stats))

        if scRows.IsEmpty then
            ReportBuilder.empty
        else
            let report =
                ReportBuilder.empty
                |> ReportBuilder.appendTitle ReportTemplates.Offense.titleSkillchain
                |> ReportBuilder.appendHeader ReportTemplates.Offense.headerSkillchain

            scRows
            |> List.sortBy (fun (_, name, _) -> name)
            |> List.fold
                (fun r (player, scName, stats) ->
                    let label = $"{scName} ({player})"

                    r
                    |> ReportBuilder.appendFormatLine
                        ReportTemplates.Offense.formatSkillchain
                        [| box label
                           box stats.TotalDmg
                           box 0
                           box stats.TotalDmg
                           box stats.Hits
                           box (ReportAggregators.lowHighString (ReportHelpers.safeMin stats.MinDmg) stats.MaxDmg)
                           box (if stats.Hits > 0 then float stats.TotalDmg / float stats.Hits else 0.0) |])
                report

module FrequencyReport =
    let formatOffense (snap: AnalyticsSnapshot) (filter: MobFilter) =
        let interactions = ReportAggregators.offenseInteractions snap filter

        let categories =
            [ "Melee", (fun i -> i.Category = InteractionCategory.Melee || i.Category = InteractionCategory.MeleeCrit)
              "Ranged", (fun i -> i.Category = InteractionCategory.Ranged || i.Category = InteractionCategory.RangedCrit)
              "Spells", (fun i -> i.Category = InteractionCategory.Spell)
              "Ability", (fun i -> i.Category = InteractionCategory.Ability)
              "Weaponskill", (fun i -> i.Category = InteractionCategory.Weaponskill) ]

        categories
        |> List.fold
            (fun report (label, pred) ->
                let subset = interactions |> List.filter pred
                let buckets = ReportAggregators.frequencyBuckets subset

                if buckets.IsEmpty then
                    report
                else
                    let report =
                        report
                        |> ReportBuilder.appendTitle label
                        |> ReportBuilder.blankLine

                    buckets
                    |> List.fold
                        (fun r (amount, count) ->
                            r |> ReportBuilder.appendFormatLine "   {0,6}: {1,4}" [| box amount; box count |])
                        report
                    |> ReportBuilder.blankLine)
            ReportBuilder.empty

    let formatDefense (snap: AnalyticsSnapshot) (filter: MobFilter) =
        let interactions = ReportAggregators.defenseInteractions snap filter

        let categories =
            [ "Melee", (fun i -> i.Category = InteractionCategory.Melee || i.Category = InteractionCategory.MeleeCrit)
              "Ranged", (fun i -> i.Category = InteractionCategory.Ranged || i.Category = InteractionCategory.RangedCrit)
              "Spells", (fun i -> i.Category = InteractionCategory.Spell)
              "Ability", (fun i -> i.Category = InteractionCategory.Ability)
              "Weaponskill", (fun i -> i.Category = InteractionCategory.Weaponskill) ]

        categories
        |> List.fold
            (fun report (label, pred) ->
                let subset = interactions |> List.filter pred
                let buckets = ReportAggregators.frequencyBuckets subset

                if buckets.IsEmpty then
                    report
                else
                    let report =
                        report
                        |> ReportBuilder.appendTitle label
                        |> ReportBuilder.blankLine

                    buckets
                    |> List.fold
                        (fun r (amount, count) ->
                            r |> ReportBuilder.appendFormatLine "   {0,6}: {1,4}" [| box amount; box count |])
                        report
                    |> ReportBuilder.blankLine)
            ReportBuilder.empty

module LootReport =
    let format (snap: AnalyticsSnapshot) (_filter: MobFilter) =
        let kills = snap.Battles |> List.filter (fun b -> b.Killed) |> List.length

        let items =
            snap.LootRecords
            |> List.groupBy (fun l -> l.ItemName)
            |> List.map (fun (item, rows) ->
                let qty = rows |> List.sumBy (fun r -> r.Quantity)
                let maxQty = rows |> List.maxBy (fun r -> r.Quantity) |> fun r -> r.Quantity
                item, qty, maxQty)
            |> List.sortByDescending (fun (_, qty, _) -> qty)

        let totalDrops = items |> List.sumBy (fun (_, qty, _) -> qty)

        if items.IsEmpty then
            ReportBuilder.empty
        else
            let report =
                ReportBuilder.empty
                |> ReportBuilder.appendTitle "Item Drops"
                |> ReportBuilder.blankLine

            items
            |> List.fold
                (fun r (item, qty, maxQty) ->
                    let itemsPerKill = if kills > 0 then float qty / float kills else 0.0
                    let dropRate = if kills > 0 then float qty / float kills else 0.0
                    let pctDrops = if totalDrops > 0 then float qty / float totalDrops else 0.0

                    r
                    |> ReportBuilder.appendFormatLine
                        ReportTemplates.Treasure.dropItemFormat
                        [| box qty; box item; box maxQty; box itemsPerKill; box dropRate; box pctDrops |])
                report

module ExperienceReport =
    let format (snap: AnalyticsSnapshot) (_filter: MobFilter) =
        if snap.ExperienceRecords.IsEmpty then
            ReportBuilder.empty
        else
            let totalXp = snap.ExperienceRecords |> List.sumBy (fun r -> r.ExperiencePoints)

            let chains =
                snap.ExperienceRecords
                |> List.groupBy (fun r -> r.Chain)
                |> List.map (fun (chain, rows) ->
                    let xp = rows |> List.sumBy (fun r -> r.ExperiencePoints)
                    chain, rows.Length, xp, if rows.Length > 0 then float xp / float rows.Length else 0.0)
                |> List.sortBy (fun (chain, _, _, _) -> chain)

            let report =
                ReportBuilder.empty
                |> ReportBuilder.appendTitle ReportTemplates.Experience.experienceRates
                |> ReportBuilder.appendFormatLine ReportTemplates.Experience.xpListFormatNum [| box "Total Experience"; box totalXp |]
                |> ReportBuilder.blankLine
                |> ReportBuilder.appendTitle ReportTemplates.Experience.experienceChains
                |> ReportBuilder.appendHeader ReportTemplates.Experience.chainHeader

            let report =
                chains
                |> List.fold
                    (fun r (chain, count, xp, avg) ->
                        r |> ReportBuilder.appendFormatLine ReportTemplates.Experience.chainFormat [| box chain; box count; box xp; box avg |])
                    report
                |> ReportBuilder.blankLine
                |> ReportBuilder.appendTitle ReportTemplates.Experience.mobListing
                |> ReportBuilder.appendHeader ReportTemplates.Experience.mobListingHeader

            snap.Battles
            |> List.filter (fun b -> b.Killed)
            |> List.groupBy (fun b -> b.EnemyName)
            |> List.map (fun (name, rows) ->
                let baseXp = MobXpLookup.tryGetXp name |> Option.defaultValue 0
                let avgFightMs =
                    if rows.Length > 0 then
                        rows
                        |> List.averageBy (fun b ->
                            let e = b.EndMs |> Option.defaultValue b.StartMs
                            float (max 0L (e - b.StartMs)))
                    else
                        0.0

                name, baseXp, rows.Length, avgFightMs)
            |> List.sortByDescending (fun (_, _, count, _) -> count)
            |> List.fold
                (fun r (name, baseXp, count, avgMs) ->
                    r
                    |> ReportBuilder.appendFormatLine
                        "{0,-26}{1,9}{2,9}{3,16}"
                        [| box name; box baseXp; box count; box (TimeSpanFormat.formatMs (int64 avgMs) false) |])
                report

module EnfeeblingReport =
    let format (snap: AnalyticsSnapshot) (filter: MobFilter) =
        let rows = ReportAggregators.debuffStats snap filter

        if rows.IsEmpty then
            ReportBuilder.empty
        else
            let report =
                ReportBuilder.empty
                |> ReportBuilder.appendTitle ReportTemplates.Enfeeble.titleDurations
                |> ReportBuilder.appendHeader ReportTemplates.Enfeeble.headerDurations

            rows
            |> List.groupBy (fun (debuff, _, _, _, _) -> debuff)
            |> List.map (fun (debuff, entries) ->
                let successful = entries |> List.sumBy (fun (_, _, _, s, _) -> s)
                debuff, successful, 0, 0)
            |> List.sortBy (fun (debuff, _, _, _) -> debuff)
            |> List.fold
                (fun r (debuff, successful, totalDur, avgDur) ->
                    r
                    |> ReportBuilder.appendFormatLine
                        "{0,-20}{1,12}{2,19}{3,17}"
                        [| box debuff; box successful; box (TimeSpanFormat.formatMs (int64 totalDur) true); box (TimeSpanFormat.formatMs (int64 avgDur) true) |])
                report

module TimelineBuffsReport =
    let formatBuffsByTime (snap: AnalyticsSnapshot) (filter: MobFilter) =
        let rows = ReportAggregators.buffStats snap filter

        if rows.IsEmpty then
            ReportBuilder.empty
        else
            let report =
                ReportBuilder.empty
                |> ReportBuilder.appendTitle "Buffs by Time"
                |> ReportBuilder.appendHeader ReportTemplates.Buff.usedHeader

            rows
            |> List.sortBy (fun r -> r.Buff)
            |> List.fold
                (fun r row ->
                    r
                    |> ReportBuilder.appendFormatLine "{0,-20}{1,-20}" [| box row.Buff; box row.Target |]
                    |> ReportBuilder.appendFormatLine ReportTemplates.Buff.numTimesFormat [| box row.Count |]
                    |> ReportBuilder.appendFormatLine
                        ReportTemplates.Buff.intervalsFormat
                        [| box (TimeSpanFormat.formatMs row.MinIntervalMs false)
                           box (TimeSpanFormat.formatMs row.MaxIntervalMs false)
                           box (TimeSpanFormat.formatMs row.AvgIntervalMs false) |])
                report

    let formatDefByTime (snap: AnalyticsSnapshot) (filter: MobFilter) =
        let rows =
            ReportAggregators.filterInteractions snap filter (fun i ->
                i.AidType = Some AidType.Enhance
                && BattleMessageCatalog.isDefensiveBuff i.ActionName)
            |> List.sortBy (fun i -> i.TimestampMs)

        if rows.IsEmpty then
            ReportBuilder.empty
        else
            let report =
                ReportBuilder.empty
                |> ReportBuilder.appendTitle "Def. by Time"
                |> ReportBuilder.appendHeader "Buff                Time"

            rows
            |> List.fold
                (fun r i ->
                    let time =
                        DateTimeOffset.FromUnixTimeMilliseconds(i.TimestampMs + snap.SessionStartMs).LocalDateTime.ToString("T")

                    r |> ReportBuilder.appendFormatLine "{0,-20}{1,12}" [| box i.ActionName; box time |])
                report

module ExtraAttacksReport =
    let format (snap: AnalyticsSnapshot) (filter: MobFilter) =
        let players = ReportAggregators.buildPlayerOffense snap filter

        if players.IsEmpty then
            ReportBuilder.empty
        else
            let report =
                ReportBuilder.empty
                |> ReportBuilder.appendTitle ReportTemplates.ExtraAttacks.mainSectionTitle
                |> ReportBuilder.appendHeader ReportTemplates.ExtraAttacks.headerMain1

            players
            |> List.filter (fun p -> p.Melee.Hits + p.Melee.Misses > 0)
            |> List.fold
                (fun r p ->
                    let attacks = p.Melee.Hits + p.Melee.Misses
                    let rounds = max 1 p.Melee.Hits
                    let extra = max 0 (attacks - rounds)

                    r
                    |> ReportBuilder.appendFormatLine
                        ReportTemplates.ExtraAttacks.formatMain1
                        [| box p.Name; box attacks; box rounds; box (float attacks / float rounds); box extra |])
                report

module AddEffectReport =
    let format (snap: AnalyticsSnapshot) (filter: MobFilter) =
        let rows =
            ReportAggregators.filterInteractions snap filter (fun i -> i.IsProc && i.ProcValue > 0)
            |> List.groupBy (fun i -> i.ActorName, i.ActionName)

        if rows.IsEmpty then
            ReportBuilder.empty
        else
            let report =
                ReportBuilder.empty
                |> ReportBuilder.appendTitle "Additional Effect Status Inflictions"
                |> ReportBuilder.appendHeader "Player               Effect               # Procs   Proc Dmg"

            rows
            |> List.sortBy (fun ((actor, action), _) -> actor, action)
            |> List.fold
                (fun r ((actor, action), entries) ->
                    let procs = entries.Length
                    let dmg = entries |> List.sumBy (fun e -> e.ProcValue)

                    r
                    |> ReportBuilder.appendFormatLine
                        "{0,-20}{1,-20}{2,10}{3,10}"
                        [| box actor; box action; box procs; box dmg |])
                report

module WsRatesReport =
    let format (snap: AnalyticsSnapshot) (filter: MobFilter) =
        let players = ReportAggregators.buildPlayerOffense snap filter

        if players.IsEmpty then
            ReportBuilder.empty
        else
            let report =
                ReportBuilder.empty
                |> ReportBuilder.appendTitle ReportTemplates.WSRate.title
                |> ReportBuilder.appendHeader ReportTemplates.WSRate.mainHeader

            let report =
                players
                |> List.filter (fun p -> p.Melee.Hits > 0 || p.Weaponskill.Hits > 0)
                |> List.fold
                    (fun r p ->
                        let meleeHits = p.Melee.Hits
                        let wsHits = p.Weaponskill.Hits

                        r
                        |> ReportBuilder.appendFormatLine
                            ReportTemplates.WSRate.mainFormat
                            [| box p.Name
                               box meleeHits
                               box 0
                               box wsHits
                               box 0
                               box 0
                               box 0.0
                               box 0.0
                               box 0
                               box 0 |])
                    report
                |> ReportBuilder.blankLine
                |> ReportBuilder.appendTitle ReportTemplates.WSRate.detailsTitle
                |> ReportBuilder.appendHeader ReportTemplates.WSRate.wsHeader

            players
            |> List.filter (fun p -> p.Weaponskill.Hits > 0)
            |> List.fold
                (fun r p ->
                    r
                    |> ReportBuilder.appendFormatLine
                        ReportTemplates.WSRate.wsFormat
                        [| box p.Name; box p.Weaponskill.Hits; box "-" |])
                report

module ThiefReport =
    let format (snap: AnalyticsSnapshot) (filter: MobFilter) =
        let rows =
            ReportAggregators.filterInteractions snap filter (fun i ->
                i.IsLocalPlayerActor
                && (BattleMessageCatalog.isThiefAction i.ActionName
                    || i.Category = InteractionCategory.Weaponskill))

        if rows.IsEmpty then
            ReportBuilder.empty
        else
            let grouped = rows |> List.groupBy (fun i -> i.ActionName)

            let report =
                ReportBuilder.empty
                |> ReportBuilder.appendTitle "Thief Actions"

            grouped
            |> List.sortBy fst
            |> List.fold
                (fun r (action, entries) ->
                    let count = entries.Length
                    let dmg = entries |> List.sumBy (fun e -> e.Value)

                    r
                    |> ReportBuilder.appendFormatLine
                        ReportTemplates.Thief.formatSummary1
                        [| box action; box count; box dmg |])
                report

module CorsairReport =
    let format (snap: AnalyticsSnapshot) (filter: MobFilter) =
        let rows =
            ReportAggregators.filterInteractions snap filter (fun i ->
                i.IsLocalPlayerActor && BattleMessageCatalog.isCorsairRoll i.ActionName)

        if rows.IsEmpty then
            ReportBuilder.empty
        else
            let grouped = rows |> List.groupBy (fun i -> i.ActionName)

            let report =
                ReportBuilder.empty
                |> ReportBuilder.appendTitle ReportTemplates.Corsair.rollFrequency
                |> ReportBuilder.blankLine

            grouped
            |> List.sortBy fst
            |> List.fold
                (fun r (roll, entries) ->
                    r
                    |> ReportBuilder.appendLine roll
                    |> ReportBuilder.appendFormatLine
                        ReportTemplates.Corsair.shortFormat
                        [| box "Count:"; box (entries.Length); box 0; box 0; box 0; box 0; box 0 |]
                    |> ReportBuilder.blankLine)
                report

module AbysseaReport =
    let format (snap: AnalyticsSnapshot) (_filter: MobFilter) =
        let killed = snap.Battles |> List.filter (fun b -> b.Killed)

        if killed.IsEmpty then
            ReportBuilder.empty
        else
            let totalXp = killed |> List.sumBy (fun b -> b.ExperiencePoints)

            ReportBuilder.empty
            |> ReportBuilder.appendTitle "Abyssea"
            |> ReportBuilder.appendFormatLine ReportTemplates.Experience.xpListFormatNum [| box "Total Cruor/XP"; box totalXp |]
            |> ReportBuilder.blankLine
            |> ReportBuilder.appendTitle "Kills"
            |> ReportBuilder.appendFormatLine
                ReportTemplates.Treasure.timesKilledFormat
                [| box (killed.Length.ToString() + " mobs"); box killed.Length |]

module AnalyticsReports =
    let format (queryId: string) (snap: AnalyticsSnapshot) (filter: MobFilter) =
        match queryId with
        | "items" -> ItemsReport.format snap filter
        | "players" -> PlayersReport.format snap filter
        | "chat-summary" -> ChatSummaryReport.format snap None None
        | "chat" -> ChatReport.format snap None None
        | "fights" -> FightsReport.format snap filter
        | "deaths" -> DeathsReport.format snap filter
        | "offense" -> OffenseReport.format snap filter
        | "defense" -> DefenseReport.format snap filter
        | "performance" -> PerformanceReport.format snap filter
        | "recovery" -> RecoveryReport.format snap filter
        | "buffs" -> BuffsReport.format snap filter
        | "debuffs" -> DebuffsReport.format snap filter
        | "skillchain" -> SkillchainReport.format snap filter
        | "offense-detail" -> FrequencyReport.formatOffense snap filter
        | "defense-detail" -> FrequencyReport.formatDefense snap filter
        | "loot" -> LootReport.format snap filter
        | "experience" -> ExperienceReport.format snap filter
        | "enfeebling" -> EnfeeblingReport.format snap filter
        | "buffs-by-time" -> TimelineBuffsReport.formatBuffsByTime snap filter
        | "def-by-time" -> TimelineBuffsReport.formatDefByTime snap filter
        | "extra-attacks" -> ExtraAttacksReport.format snap filter
        | "add-effect" -> AddEffectReport.format snap filter
        | "ws-rates" -> WsRatesReport.format snap filter
        | "thief" -> ThiefReport.format snap filter
        | "corsair" -> CorsairReport.format snap filter
        | "abyssea" -> AbysseaReport.format snap filter
        | _ -> ReportBuilder.empty

    let formatChat (snap: AnalyticsSnapshot) (modeFilter: string option) (speakerFilter: string option) =
        ChatReport.format snap modeFilter speakerFilter

    let formatChatSummary (snap: AnalyticsSnapshot) (modeFilter: string option) (speakerFilter: string option) =
        ChatSummaryReport.format snap modeFilter speakerFilter
