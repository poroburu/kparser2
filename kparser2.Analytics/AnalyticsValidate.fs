namespace kparser2.Analytics

open System
open kparser2.Decoders

module AnalyticsValidate =
    type ValidationIssue =
        { Code: string
          Message: string }

    type ValidationReport =
        { Ok: bool
          Issues: ValidationIssue list }

    let private issue code message = { Code = code; Message = message }

    let validateCombat (snap: AnalyticsSnapshot) =
        let mutable issues = []

        if snap.Interactions.Length > 0 && snap.Battles.Length = 0 then
            issues <- issue "no_battles" "interactions > 0 but battles = 0 (fight segmentation)" :: issues

        let harmCount =
            snap.Interactions
            |> List.filter (fun i -> i.InteractionType = InteractionType.Harm)
            |> List.length

        if harmCount > 0 && snap.Battles.Length = 0 then
            issues <- issue "harm_without_fights" "harm interactions present but no battles segmented" :: issues

        let unnamedCombatants =
            snap.Combatants
            |> List.filter (fun c -> c.Name.StartsWith("Entity ", StringComparison.Ordinal))

        if unnamedCombatants.Length > 0 then
            issues <-
                issue
                    "unnamed_entities"
                    $"combatants with unresolved names: {unnamedCombatants.Length} (e.g. {unnamedCombatants.Head.Name})"
                :: issues

        let killed = snap.Battles |> List.filter (fun b -> b.Killed)

        if snap.Battles.Length >= 2 then
            let ids = snap.Battles |> List.map (fun b -> b.Id) |> List.sort

            if ids <> List.sort (List.distinct ids) then
                issues <- issue "duplicate_battle_ids" "battle ids are not unique" :: issues

            let outOfOrder = List.pairwise ids |> List.exists (fun (a, b) -> b <= a)

            if outOfOrder then
                issues <- issue "battle_id_order" "battle ids are not monotonically increasing" :: issues

        if killed.Length > 0 && snap.ExperienceRecords.Length = 0 then
            issues <-
                issue
                    "kills_without_xp"
                    $"killed {killed.Length} battle(s) but no XP records (may be normal for some mobs)"
                :: issues

        let localId = EntityRegistry.tryLocalPlayerId()

        let localHarm =
            match localId with
            | Some id ->
                snap.Interactions
                |> List.exists (fun i ->
                    i.InteractionType = InteractionType.Harm
                    && (i.ActorId = id || i.TargetId = id))
            | None -> false

        if localId.IsSome && localHarm && EntityRegistry.localPlayerName().IsNone then
            issues <-
                issue
                    "local_player_unnamed"
                    $"local player id {localId.Value} has combat activity but no resolved name"
                :: issues

        let hardIssues =
            issues
            |> List.filter (fun i ->
                i.Code <> "unnamed_entities"
                && i.Code <> "kills_without_xp"
                && i.Code <> "local_player_unnamed")

        { Ok = List.isEmpty hardIssues
          Issues = List.rev issues }

    let validateMultiFight (snap: AnalyticsSnapshot) (minBattles: int) =
        let baseReport = validateCombat snap

        if snap.Battles.Length < minBattles then
            { Ok = false
              Issues =
                issue "insufficient_battles" $"expected at least {minBattles} battles, got {snap.Battles.Length}"
                :: baseReport.Issues }
        else
            baseReport

    let validateNames (snap: AnalyticsSnapshot) =
        let baseReport = validateCombat snap

        match EntityRegistry.tryLocalPlayerId(), EntityRegistry.localPlayerName() with
        | Some _, None ->
            { Ok = false
              Issues =
                baseReport.Issues
                |> List.filter (fun i -> i.Code <> "local_player_unnamed")
                |> fun issues ->
                    issue
                        "local_player_unnamed"
                        "local player id is known but local_player_name is missing"
                    :: issues }
        | _ -> baseReport

    let validateChat (snap: AnalyticsSnapshot) (minChat: int) =
        let incoming =
            snap.ChatMessages
            |> List.filter (fun c ->
                String.IsNullOrWhiteSpace c.Direction
                || c.Direction.Equals("incoming", StringComparison.OrdinalIgnoreCase))

        if incoming.Length < minChat then
            { Ok = false
              Issues =
                [ issue
                      "insufficient_chat"
                      $"expected at least {minChat} incoming chat row(s), got {incoming.Length}" ] }
        else
            { Ok = true
              Issues = [] }

    let printReport (report: ValidationReport) =
        if report.Ok then
            printfn "validate=OK"
        else
            printfn "validate=FAIL"

            for i in report.Issues do
                printfn "  [%s] %s" i.Code i.Message

        report.Ok
