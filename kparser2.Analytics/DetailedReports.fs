namespace kparser2.Analytics

open System

/// Report calculations operate only on the supplied snapshot, never the live registry.
module DetailedReports =
    let private eq (a: string) (b: string) = String.Equals(a, b, StringComparison.OrdinalIgnoreCase)
    let private splitColumns (text: string) =
        text.Split([| "    " |], StringSplitOptions.None) |> Array.toList
    let private alignedLine (widths: int list) (lastColumn: int) (values: string list) =
        values
        |> List.mapi (fun index value ->
            if index = lastColumn then value
            else value.PadRight(List.item index widths))
        |> String.concat "    "
    let private section title header rows =
        let headerColumns = splitColumns header
        let rowColumns = rows |> List.map (List.map string)
        let columnCount =
            max headerColumns.Length (rowColumns |> List.map List.length |> fun lengths -> if List.isEmpty lengths then 0 else List.max lengths)
        let widths =
            [ 0 .. columnCount - 1 ]
            |> List.map (fun index ->
                let headerWidth = headerColumns |> List.tryItem index |> Option.map String.length |> Option.defaultValue 0
                let rowLengths = rowColumns |> List.choose (List.tryItem index) |> List.map String.length
                let rowWidth = if List.isEmpty rowLengths then 0 else List.max rowLengths
                max headerWidth rowWidth)
        let lastColumn = max 0 (columnCount - 1)
        let report =
            ReportBuilder.empty
            |> ReportBuilder.appendTitle title
            |> ReportBuilder.appendHeader (alignedLine widths lastColumn headerColumns)
        if List.isEmpty rows then report |> ReportBuilder.appendLine "No matching events in this capture."
        else rows |> List.fold (fun r row -> r |> ReportBuilder.appendLine (alignedLine widths lastColumn (row |> List.map string))) report
    let private unavailable message = ReportBuilder.empty |> ReportBuilder.appendLine ("Unavailable: " + message)
    let private append a b = { Spans = a.Spans @ b.Spans }
    let private percent n d = if d = 0 then "—" else sprintf "%.2f%%" (100.0 * float n / float d)
    let private average (values: int list) = if values.IsEmpty then "—" else sprintf "%.2f" (values |> List.averageBy float)
    let private hit (i: Interaction) = i.Success = "hit" || i.Success = "message" || i.Success = "critical" || i.Success = "magic-burst"
    let private damage (i: Interaction) = InteractionClassification.isHpDamage i
    let private category (i: Interaction) =
        if InteractionClassification.isSpike i then
            "Spikes"
        else
            match i.Category with
            | InteractionCategory.Melee | InteractionCategory.MeleeCrit -> "Melee"
            | InteractionCategory.Ranged | InteractionCategory.RangedCrit -> "Ranged"
            | InteractionCategory.Spell -> "Spell"
            | InteractionCategory.Weaponskill -> "Weaponskill"
            | InteractionCategory.Ability -> "Ability"
            | InteractionCategory.Skillchain -> "Skillchain"
            | _ -> "Other"
    let private containsAny (terms: string list) (value: string) =
        let value = value.ToLowerInvariant()
        terms |> List.exists value.Contains
    let private timeBuffMatches defense mode actionName =
        if mode = "All" then
            true
        else
            let offenseTerms =
                match mode with
                | "Accuracy" -> [ "focus"; "aggressor"; "sharpshot"; "souleater"; "diabolic eye"; "rng roll"; "hasso"; "yonin"; "innin"; "madrigal"; "stalwart tonic"; "stalwart gambir" ]
                | "Attack" -> [ "minuet"; "drk roll"; "berserk"; "warcry"; "last resort"; "souleater"; "hasso"; "defender"; "dia"; "footwork"; "impetus"; "stalwart tonic"; "stalwart gambir" ]
                | "CriticalRate" -> [ "blood rage"; "thf roll"; "impetus"; "innin"; "focus"; "champion tonic"; "champion gambir" ]
                | "Haste" -> [ "haste"; "march"; "hasso"; "haste samba"; "refueling"; "animating wail" ]
                | _ -> []
            let defenseTerms =
                match mode with
                | "Accuracy" -> [ "mambo"; "nin roll"; "dodge"; "yonin"; "innin"; "aggressor" ]
                | "Attack" -> [ "minne"; "defender"; "rampart"; "sentinel"; "protect" ]
                | "CriticalRate" -> []
                | _ -> []
            containsAny (if defense then defenseTerms else offenseTerms) actionName
    let private participant (snap: AnalyticsSnapshot) id =
        match snap.Combatants |> List.tryFind (fun c -> c.Id = id) with
        | Some c when c.Kind <> EntityKind.Unknown -> c.Kind = EntityKind.Player || c.Kind = EntityKind.Pet || c.Kind = EntityKind.Fellow
        | _ -> not (snap.Battles |> List.exists (fun b -> b.EnemyId = Some id))
    let private selected name (filter: MobFilter) = filter.SelectedPlayerName |> Option.forall (eq name)
    let private actionKey (i: Interaction) = i.SourcePacketId |> Option.defaultValue ($"legacy:{i.ActorId}:{i.TimestampMs}:{i.ActionName}")

    let combat defense mode (snap: AnalyticsSnapshot) (filter: MobFilter) =
        let rows =
            ReportAggregators.filterInteractions snap { filter with SelectedPlayerName = None } damage
            |> List.filter (fun i ->
                if defense then (participant snap i.TargetId || i.IsLocalPlayerTarget) && selected i.TargetName filter
                else (participant snap i.ActorId || i.IsLocalPlayerActor) && selected i.ActorName filter)
        let name (i: Interaction) = if defense then i.TargetName else i.ActorName
        let total = rows |> List.sumBy (fun i -> max 0 i.Value |> int64)
        let summary = rows |> List.groupBy name |> List.sortBy fst |> List.map (fun (n, events) ->
            let sum = events |> List.sumBy (fun i -> max 0 i.Value |> int64)
            [box n; box sum; box (if total = 0L then "—" else sprintf "%.2f%%" (100.0 * float sum / float total))])
        let details =
          rows |> List.filter (fun i -> mode = "All" || mode = "DamageTaken" || category i = mode)
          |> List.groupBy (fun i -> name i, category i, i.ActionName) |> List.sortBy fst
          |> List.map (fun ((n, cat, action), events) ->
            let hits = events |> List.filter hit
            let values = hits |> List.map (fun i -> max 0 i.Value)
            let nonzero = values |> List.filter ((<) 0)
            let crits = hits |> List.filter (fun i -> i.DamageModifier = DamageModifier.Critical)
            let bursts = hits |> List.filter (fun i -> i.DamageModifier = DamageModifier.MagicBurst)
            [box n; box cat; box action; box (values |> List.sumBy int64); box hits.Length; box (events.Length - hits.Length)
             box (percent hits.Length events.Length); box (if values.IsEmpty then "—" else sprintf "%d/%d" (List.min values) (List.max values))
             box (average values); box (average nonzero); box crits.Length; box (percent crits.Length hits.Length); box bursts.Length])
        let title = if defense then "Damage Taken" else "Damage"
        let a = section (title + " Summary") "Player    Total Dmg    Share" summary
        let b = section (title + " Details") "Player    Category    Action    Damage    Hits    Misses    Accuracy    Low/High    Avg (+0)    Avg (-0)    Crits    Crit%    Bursts" details
        let spikeDetails =
            rows
            |> List.filter InteractionClassification.isSpike
            |> List.groupBy (fun i -> name i, i.ActionName)
            |> List.sortBy fst
            |> List.map (fun ((n, action), events) ->
                let hits = events |> List.filter hit
                let values = hits |> List.map (fun i -> max 0 i.Value)
                [ box n
                  box action
                  box hits.Length
                  box (values |> List.sumBy int64)
                  box (average values) ])
        let spikes = section "Spikes" "Player    Action    Hits    Damage    Average" spikeDetails
        let withSpikes report =
            if List.isEmpty spikeDetails then report else append report spikes
        if mode = "Summary" then a
        elif mode = "All" || mode = "DamageTaken" then withSpikes (append a b)
        else withSpikes b

    let defenses mode (snap: AnalyticsSnapshot) filter =
        let rows =
            ReportAggregators.filterInteractions snap { filter with SelectedPlayerName = None } (fun _ -> true)
            |> List.filter (fun i -> (participant snap i.TargetId || i.IsLocalPlayerTarget) && selected i.TargetName filter)
        if mode = "AbilityUsage" then
            rows |> List.filter (fun i -> i.Category = InteractionCategory.Ability)
                 |> List.groupBy (fun i -> i.ActorName, i.ActionName)
                 |> List.map (fun ((n, a), es) -> [box n; box a; box (es |> List.distinctBy (fun i -> i.TimestampMs, i.ActorId, i.ActionName) |> List.length)])
                 |> section "Enemy Ability Usage" "Enemy    Ability    Observed uses"
        else
            rows |> List.filter (fun i -> damage i && (mode <> "Utsusemi" || i.Success = "shadow-absorb"))
                 |> List.groupBy (fun i -> i.TargetName, i.Success) |> List.sortBy fst
                 |> List.map (fun ((n, success), es) -> [box n; box success; box es.Length])
                 |> section (if mode = "Utsusemi" then "Shadow Absorptions" else "Defensive Outcomes") "Player    Outcome    Count"

    let frequency defense (snap: AnalyticsSnapshot) filter =
        ReportAggregators.filterInteractions snap { filter with SelectedPlayerName = None } damage
        |> List.filter (fun i ->
            if defense then (participant snap i.TargetId || i.IsLocalPlayerTarget) && selected i.TargetName filter
            else (participant snap i.ActorId || i.IsLocalPlayerActor) && selected i.ActorName filter)
        |> List.groupBy (fun i -> (if defense then i.TargetName else i.ActorName), category i, i.ActionName, i.Success, i.Value)
        |> List.sortBy fst
        |> List.map (fun ((n, cat, action, outcome, value), es) -> [box n; box cat; box action; box outcome; box value; box es.Length])
        |> section "Damage Frequency" "Player    Category    Action    Outcome    Damage    Frequency"

    let recovery mode snap filter =
        let statusCure = mode = "StatusCuring" || mode = "StatusCured"
        let eraseIds = set [83; 123; 159; 321; 341; 343]
        let rows = ReportAggregators.filterInteractions snap { filter with SelectedPlayerName = None } (fun i -> if statusCure then eraseIds.Contains i.MessageId else i.AidType = Some AidType.Recovery && i.MessageId <> 224)
        let received = mode = "Recovery" || mode = "StatusCured"
        let rows = rows |> List.filter (fun i -> selected (if received then i.TargetName else i.ActorName) filter)
        if statusCure then
            rows |> List.groupBy (fun i -> i.ActorName, i.TargetName, i.ActionName, i.Value) |> List.sortBy fst
            |> List.map (fun ((caster, target, action, effect), es) -> [box caster; box target; box action; box effect; box es.Length])
            |> section "Status Cures" "Caster    Recipient    Action    Status ID    Removals"
        else
            rows |> List.groupBy (fun i -> (if received then i.TargetName else i.ActorName), i.ActionName) |> List.sortBy fst
            |> List.map (fun ((n, action), es) ->
                let values = es |> List.filter hit |> List.map (fun i -> max 0 i.Value)
                [box n; box action; box (es |> List.distinctBy actionKey |> List.length)
                 box (values |> List.sumBy int64); box (average values)])
            |> section (if received then "Recovery Received" else "Curing") "Player    Action    Casts    HP restored    Average per target"

    let buffs mode snap filter =
        ReportAggregators.filterInteractions snap { filter with SelectedPlayerName = None } (fun i -> i.AidType = Some AidType.Enhance && not ((set [83; 123; 159; 204; 206; 231; 321; 341; 343; 571]).Contains(i.MessageId)))
        |> List.filter (fun i -> selected (if mode = "Received" then i.TargetName else i.ActorName) filter)
        |> List.groupBy (fun i -> i.ActorName, i.TargetName, i.ActionName, i.Success) |> List.sortBy fst
        |> List.map (fun ((actor, target, action, outcome), es) ->
            let times = es |> List.map (fun i -> i.TimestampMs) |> List.distinct |> List.sort
            let intervals = times |> List.pairwise |> List.map (fun (a, b) -> float (b - a) / 1000.0)
            [box actor; box target; box action; box outcome; box times.Length; box (if intervals.IsEmpty then "—" else sprintf "%.2f" (List.average intervals))])
        |> section (if mode = "Received" then "Buffs Received" else "Buffs Used") "Caster    Recipient    Buff    Outcome    Applications    Mean interval (s)"

    let debuffs mode snap filter =
        ReportAggregators.filterInteractions snap filter (fun i -> i.HarmType = Some HarmType.Enfeeble)
        |> List.filter (fun i -> if mode = "Players" then participant snap i.TargetId else not (participant snap i.TargetId))
        |> List.groupBy (fun i -> i.ActorName, i.TargetName, i.ActionName) |> List.sortBy fst
        |> List.map (fun ((a, t, action), es) ->
            let successes = es |> List.filter hit |> List.length
            [box a; box t; box action; box es.Length; box successes; box (es.Length - successes); box (percent successes es.Length)])
        |> section "Debuff Outcomes" "Caster    Target    Action    Attempts    Successful    Failed    Success%"

    let additionalEffects snap filter =
        let meleeRanged (i: Interaction) = category i = "Melee" || category i = "Ranged"
        let scoped = ReportAggregators.filterInteractions snap { filter with SelectedPlayerName = None }
        let attacks = scoped meleeRanged |> List.filter (fun i -> selected i.ActorName filter)
        let procs =
            attacks
            |> List.filter (fun i -> i.IsProc)
            |> List.groupBy (fun i -> i.ActorName, i.ActionName)
            |> List.sortBy fst
            |> List.map (fun ((n, a), es) ->
                let eligible = attacks |> List.filter (fun i -> i.ActorName = n && i.ActionName = a && hit i) |> List.length
                [ box n
                  box a
                  box es.Length
                  box (es |> List.sumBy (fun i -> int64 i.ProcValue))
                  box (percent es.Length eligible) ])
        let incomingLanded (actor: string) =
            scoped meleeRanged
            |> List.filter (fun i -> hit i && eq i.TargetName actor)
            |> List.length
        let spikes =
            scoped InteractionClassification.isSpike
            |> List.filter (fun i -> (participant snap i.ActorId || i.IsLocalPlayerActor) && selected i.ActorName filter)
            |> List.groupBy (fun i -> i.ActorName, i.ActionName)
            |> List.sortBy fst
            |> List.map (fun ((n, a), es) ->
                [ box n
                  box a
                  box es.Length
                  box (es |> List.sumBy (fun i -> int64 (max 0 i.Value)))
                  box (percent es.Length (incomingLanded n)) ])
        let procReport =
            if List.isEmpty procs then ReportBuilder.empty
            else section "Additional Effects" "Player    Attack    Procs    Proc value total    Proc / landed attack" procs
        let spikeReport =
            if List.isEmpty spikes then ReportBuilder.empty
            else section "Spikes" "Player    Action    Hits    Damage    Rate vs landed melee taken" spikes
        match List.isEmpty procs, List.isEmpty spikes with
        | true, true ->
            section "Additional Effects" "Player    Attack    Procs    Proc value total    Proc / landed attack" []
        | _ -> append procReport spikeReport

    let wsRates snap filter =
        let rows =
            ReportAggregators.filterInteractions snap filter (fun i -> i.Category = InteractionCategory.Weaponskill)
            |> List.distinctBy actionKey
        rows |> List.groupBy (fun i -> i.ActorName, i.ActionName) |> List.sortBy fst
        |> List.map (fun ((n, a), es) ->
            let intervals =
                es |> List.sortBy (fun i -> i.TimestampMs) |> List.pairwise
                |> List.filter (fun (a, b) -> a.BattleId.IsSome && a.BattleId = b.BattleId)
                |> List.map (fun (a, b) -> b.TimestampMs - a.TimestampMs)
            [box n; box a; box es.Length; box intervals.Length; box (if intervals.IsEmpty then "—" else sprintf "%.2f" (List.averageBy float intervals / 1000.0))])
        |> section "Weaponskill Rates" "Player    Weaponskill    Uses    Within-fight intervals    Mean interval (s)"
        |> fun r -> append r (unavailable "TP at weaponskill use is not retained; no TP estimate is substituted.")

    let extraAttacks baseAttacks snap filter =
        let rows = ReportAggregators.filterInteractions snap filter (fun i -> category i = "Melee" && i.CommandNo = 1)
        let evidenced = rows |> List.filter (fun i -> i.SourcePacketId.IsSome)
        let report =
            evidenced |> List.groupBy (fun i -> i.ActorName, actionKey i)
            |> List.groupBy (fun ((n, _), _) -> n)
            |> List.map (fun (n, rounds) ->
                let swings = rounds |> List.sumBy (fun (_, es) -> es.Length)
                let extra = rounds |> List.sumBy (fun (_, es) -> max 0 (es.Length - baseAttacks))
                let extraRounds = rounds |> List.filter (fun (_, es) -> es.Length > baseAttacks) |> List.length
                [box n; box swings; box rounds.Length; box (sprintf "%.2f" (float swings / float rounds.Length)); box extra; box (percent extraRounds rounds.Length)])
            |> section "Extra Attacks" "Player    Swings    Packet rounds    Attacks/round    Attacks above selected base    Rounds above base%"
        if evidenced.Length <> rows.Length then append report (unavailable "some imported events lack packet identity and are excluded from round statistics.") else report

    let corsair snap filter =
        // LSB corsair.applyRoll returns the total for main-target messages.
        // Secondary targets repeat the effect and must not multiply the roll count.
        let ids = set [420; 422; 424; 425; 426]
        let rolls =
            ReportAggregators.filterInteractions snap filter (fun i -> ids.Contains i.MessageId)
            |> List.distinctBy actionKey
        let summary =
            rolls |> List.groupBy (fun i -> i.ActorName, i.ActionName) |> List.sortBy fst
            |> List.map (fun ((n, a), es) ->
                let initial = es |> List.filter (fun i -> i.MessageId = 420 || i.MessageId = 422) |> List.length
                let busts = es |> List.filter (fun i -> i.MessageId = 426) |> List.length
                let totals = es |> List.filter (fun i -> i.MessageId <> 426 && i.Value >= 1 && i.Value <= 11) |> List.map (fun i -> i.Value)
                [box n; box a; box initial; box (es.Length - initial); box busts; box (average totals)])
            |> section "Corsair Rolls" "Player    Roll    Initial rolls    Double-ups    Busts    Average observed total"
        let distribution =
            rolls |> List.groupBy (fun i -> i.ActorName, i.ActionName, if i.MessageId = 426 then "Bust" else string i.Value)
            |> List.sortBy fst |> List.map (fun ((n, a, total), es) -> [box n; box a; box total; box es.Length])
            |> section "Roll Distribution" "Player    Roll    Observed total    Frequency"
        append summary distribution

    let thief snap filter =
        ReportAggregators.filterInteractions snap filter (fun i -> BattleMessageCatalog.isThiefAction i.ActionName)
        |> List.distinctBy actionKey
        |> List.groupBy (fun i -> i.ActorName, i.ActionName) |> List.sortBy fst
        |> List.map (fun ((n, a), es) -> [box n; box a; box es.Length])
        |> section "Thief Abilities" "Player    Ability    Uses"
        |> fun r -> append r (unavailable "SA/TA consumption is not retained; subsequent damage cannot be attributed reliably to these abilities.")

    type private LootDisplayRow =
        { EventType: string
          ItemName: string
          Recipient: string
          Source: string
          Quantity: int
          Gil: int }

    let private displayActor actor =
        if String.IsNullOrWhiteSpace actor
           || actor.Equals("Unknown", StringComparison.OrdinalIgnoreCase)
           || actor.Equals("Entity 0", StringComparison.OrdinalIgnoreCase) then
            "—"
        else
            actor

    let private sameSource (left: string option) (right: string option) =
        match left, right with
        | Some left, Some right -> eq left right
        | _ -> true

    let private consolidateLoot (rows: LootResolution.ResolvedLoot list) =
        let pending = ResizeArray<LootResolution.ResolvedLoot>()
        let result = ResizeArray<LootDisplayRow>()

        let addStandalone (row: LootResolution.ResolvedLoot) =
            let loot = row.Loot
            let distribution = LootResolution.isDistribution loot

            result.Add
                { EventType = loot.EventType
                  ItemName = row.ItemName
                  Recipient = if distribution then displayActor loot.ActorName else "—"
                  Source =
                    if distribution then
                        row.SourceName |> Option.map displayActor |> Option.defaultValue "—"
                    else
                        row.SourceName |> Option.map displayActor |> Option.defaultValue (displayActor loot.ActorName)
                  Quantity = loot.Quantity
                  Gil = loot.Gil }

        for row in rows do
            if LootResolution.isMeaningful row then
                let loot = row.Loot

                if not (LootResolution.isDistribution loot) then
                    if loot.EventType.Equals("Found", StringComparison.OrdinalIgnoreCase) && loot.ItemId > 0 then
                        pending.Add row
                    elif loot.ItemId > 0 || loot.Gil > 0 then
                        addStandalone row
                elif not (loot.EventType.Equals("Lot", StringComparison.OrdinalIgnoreCase)) then
                    let matchIndex =
                        [ 0 .. pending.Count - 1 ]
                        |> List.tryFind (fun index ->
                            let found = pending.[index]
                            found.ItemName = row.ItemName && sameSource found.SourceName row.SourceName)

                    let source, quantity, gil =
                        match matchIndex with
                        | Some index ->
                            let found = pending.[index]
                            pending.RemoveAt index
                            found.SourceName, found.Loot.Quantity, found.Loot.Gil
                        | None -> row.SourceName, loot.Quantity, loot.Gil

                    result.Add
                        { EventType = loot.EventType
                          ItemName = row.ItemName
                          Recipient = displayActor loot.ActorName
                          Source = source |> Option.map displayActor |> Option.defaultValue "—"
                          Quantity = quantity
                          Gil = gil }

        for row in pending do
            addStandalone row

        List.ofSeq result

    let loot mode excludeCrystals (snap: AnalyticsSnapshot) filter =
        let rows =
            LootResolution.resolve snap.LootRecords
            |> List.filter LootResolution.isMeaningful
            |> consolidateLoot
            |> List.filter (fun row ->
                (filter.SelectedPlayerName
                 |> Option.forall (fun name -> eq name row.Recipient || eq name row.Source))
                && (not excludeCrystals || not (row.ItemName.Contains("crystal", StringComparison.OrdinalIgnoreCase))))

        if mode <> "All" && mode <> "Summary" && mode <> "Details" then
            unavailable "this loot mode requires source/kill or gathering attribution absent from the current loot records."
        else
            rows |> List.groupBy (fun row -> row.EventType, row.ItemName, row.Recipient, row.Source) |> List.sortBy fst
            |> List.map (fun ((kind, item, recipient, source), es) ->
                [ box kind
                  box item
                  box recipient
                  box source
                  box (es |> List.sumBy (fun row -> row.Quantity))
                  box (es |> List.sumBy (fun row -> int64 row.Gil)) ])
            |> section "Loot Events" "Event    Item    Recipient    Source    Quantity    Gil"

    let items mode (snap: AnalyticsSnapshot) filter =
        let rows = snap.ItemUses |> List.filter (fun i -> selected i.ActorName filter)
        if mode = "Details" then
            rows |> List.map (fun i -> [box i.TimestampMs; box i.ActorName; box i.ItemName; box i.Quantity])
            |> section "Item Use Details" "Time (ms)    Player    Item    Quantity"
        else
            rows |> List.groupBy (fun i -> i.ActorName, i.ItemName) |> List.sortBy fst
            |> List.map (fun ((n, item), es) -> [box n; box item; box (es |> List.sumBy (fun i -> i.Quantity))])
            |> section "Items Used" "Player    Item    Quantity"

    type private StatusInterval = { Start: Interaction; Finish: int64 }
    let private statusIntervals (snap: AnalyticsSnapshot) =
        let starts = set [203; 205; 230; 236; 237; 242; 243; 266; 267; 278; 319; 320]
        let ends = set [83; 123; 159; 204; 206; 321; 341; 343]
        let active = Collections.Generic.Dictionary<uint32 * int, Interaction>()
        let complete = ResizeArray<StatusInterval>()
        for i in snap.Interactions |> List.sortBy (fun i -> i.TimestampMs, i.Id) do
            let key = i.TargetId, i.Value
            if starts.Contains i.MessageId && hit i then
                // A refresh closes the observed interval; it does not prove an earlier unobserved application.
                match active.TryGetValue key with
                | true, first when i.TimestampMs > first.TimestampMs -> complete.Add({ Start = first; Finish = i.TimestampMs })
                | _ -> ()
                active.[key] <- i
            elif ends.Contains i.MessageId then
                match active.TryGetValue key with
                | true, first when i.TimestampMs >= first.TimestampMs ->
                    complete.Add({ Start = first; Finish = i.TimestampMs })
                    active.Remove key |> ignore
                | _ -> ()
        List.ofSeq complete, active.Count

    let durations snap filter =
        let intervals, openCount = statusIntervals snap
        let selectedIds = ReportAggregators.filterBattles snap filter |> List.map (fun b -> b.Id) |> Set.ofList
        let restricted = filter.SelectedBattleId.IsSome || filter.SelectedMobName.IsSome || filter.ExcludeZeroXp
        intervals |> List.filter (fun interval -> interval.Start.HarmType = Some HarmType.Enfeeble && selected interval.Start.ActorName filter && (not restricted || interval.Start.BattleId |> Option.exists selectedIds.Contains))
        |> List.groupBy (fun interval -> interval.Start.ActionName, interval.Start.TargetName) |> List.sortBy fst
        |> List.map (fun ((action, target), intervals) ->
            let times = intervals |> List.map (fun i -> i.Finish - i.Start.TimestampMs)
            [box action; box target; box times.Length; box (sprintf "%.2f" (List.sum times |> float |> fun v -> v / 1000.0)); box (sprintf "%.2f" (List.averageBy float times / 1000.0))])
        |> section "Enfeebling Durations" "Effect    Target    Completed intervals    Total seconds    Mean seconds"
        |> ReportBuilder.appendLine ($"{openCount} status applications have no observed end; excluded from duration averages.")

    let timeline defense mode snap filter =
        let intervals, openCount = statusIntervals snap
        let rows = ReportAggregators.filterInteractions snap { filter with SelectedPlayerName = None } damage
        let report =
            intervals |> List.filter (fun i -> i.Start.AidType = Some AidType.Enhance && selected i.Start.TargetName filter && timeBuffMatches defense mode i.Start.ActionName)
            |> List.groupBy (fun i -> i.Start.TargetId, i.Start.TargetName, i.Start.ActionName) |> List.sortBy fst
            |> List.collect (fun ((id, name, effect), spans) ->
                let actorRows = rows |> List.filter (fun i -> (if defense then i.TargetId else i.ActorId) = id && (category i = "Melee" || category i = "Ranged"))
                actorRows |> List.groupBy (fun i -> spans |> List.exists (fun span -> i.TimestampMs >= span.Start.TimestampMs && i.TimestampMs < span.Finish))
                |> List.map (fun (inside, es) ->
                    let hits = es |> List.filter hit
                    let crits = hits |> List.filter (fun i -> i.DamageModifier = DamageModifier.Critical) |> List.length
                    [box name; box effect; box (if inside then "Within observed intervals" else "Outside observed intervals")
                     box es.Length; box (percent hits.Length es.Length); box (average (hits |> List.map (fun i -> i.Value))); box (percent crits hits.Length)]))
            |> section (if defense then "Defense by Time" else "Buffs by Time") "Player    Buff    Sample    Attempts    Accuracy    Mean hit damage    Critical rate"
        let report = report |> ReportBuilder.appendLine ($"{openCount} unclosed applications excluded. Outside an observed interval does not establish absence of a buff.")
        if mode = "Haste" then append report (unavailable "effective haste requires weapon delay and complete action timing; attack counts alone cannot establish haste.") else report

    let format queryId mode details excludeCrystals baseAttacks snap filter =
        match queryId with
        | "offense" -> combat false mode snap filter
        | "defense" when mode = "Defenses" || mode = "Utsusemi" || mode = "AbilityUsage" -> defenses mode snap filter
        | "defense" -> combat true mode snap filter
        | "offense-detail" -> frequency false snap filter
        | "defense-detail" -> frequency true snap filter
        | "recovery" -> recovery mode snap filter
        | "buffs" -> buffs mode snap filter
        | "debuffs" -> debuffs mode snap filter
        | "enfeebling" when mode = "Paralyze" ->
            ReportAggregators.filterInteractions snap filter (fun i -> i.MessageId = 29 || i.MessageId = 84)
            |> List.groupBy (fun i -> i.ActorName) |> List.map (fun (n, es) -> [box n; box es.Length])
            |> section "Observed Paralysis Interruptions" "Actor    Interruptions"
        | "enfeebling" when mode = "TpMoves" -> defenses "AbilityUsage" snap filter
        | "enfeebling" -> durations snap filter
        | "buffs-by-time" -> timeline false mode snap filter
        | "def-by-time" -> timeline true mode snap filter
        | "extra-attacks" -> extraAttacks baseAttacks snap filter
        | "add-effect" -> additionalEffects snap filter
        | "ws-rates" -> wsRates snap filter
        | "corsair" -> corsair snap filter
        | "thief" -> thief snap filter
        | "loot" -> loot mode excludeCrystals snap filter
        | "items" -> items (if details then "Details" else mode) snap filter
        | "abyssea" -> LegacyReportStubs.abyssea snap filter
        | _ -> AnalyticsReports.format queryId snap filter
