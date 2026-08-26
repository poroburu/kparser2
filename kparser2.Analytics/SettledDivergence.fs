namespace kparser2.Analytics

open System
open kparser2.Decoders

/// Ranked settled-gap report for the parity scan. In-flight rows are omitted;
/// deferred codes do not fail `--assert-settled`. Scoped prove uses `matchesCode`.
module SettledDivergence =
    [<Literal>]
    let BucketMissing = "kparser2-missing"

    [<Literal>]
    let BucketDeferred = "deferred"

    [<Literal>]
    let BucketExtra = "kparser2-extra"

    type Issue =
        { Code: string
          Rank: int
          Deferred: bool
          Bucket: string
          Opcode: string
          CommandNo: int option
          MessageId: int option
          Kind: int option
          TimestampMs: int64 option
          Message: string }

    type Report =
        { Actionable: Issue list
          Deferred: Issue list }

    let defaultDeferredCodes =
        Set.ofList [ "unnamed_entities"; "melee_name_pairing" ]

    let matchesCode (filter: string) (code: string) =
        if String.IsNullOrWhiteSpace filter then
            false
        else
            code.Equals(filter, StringComparison.OrdinalIgnoreCase)
            || code.StartsWith(filter + "-", StringComparison.OrdinalIgnoreCase)

    let isLikelyFourcc (n: int) =
        if n <= 0x20202020 || n >= 0x7E7E7E7E then
            false
        else
            let byteOk shift =
                let b = (n >>> shift) &&& 0xFF
                b >= 0x20 && b <= 0x7E

            byteOk 0 && byteOk 8 && byteOk 16 && byteOk 24

    let isMessageClassified (messageId: int) =
        if messageId <= 0 then
            false
        else
            let resolved = ParseCodesTables.resolveAlternateMessageId messageId

            ParseCodesTables.tryInteractionType resolved |> Option.isSome
            || MsgBasicCatalog.tryClassifyAction messageId |> Option.isSome

    let private parseSpellArg (actionName: string) =
        if actionName.StartsWith("spell-", StringComparison.Ordinal) then
            match Int32.TryParse(actionName.Substring(6)) with
            | true, n -> Some n
            | _ -> None
        else
            None

    let private issue
        code
        rank
        deferred
        bucket
        opcode
        commandNo
        messageId
        kind
        timestampMs
        message
        =
        { Code = code
          Rank = rank
          Deferred = deferred
          Bucket = bucket
          Opcode = opcode
          CommandNo = commandNo
          MessageId = messageId
          Kind = kind
          TimestampMs = timestampMs
          Message = message }

    let collect (snap: AnalyticsSnapshot) (skip: Set<string>) =
        let skipped code =
            skip |> Set.exists (fun s -> matchesCode s code)

        let acc = ResizeArray<Issue>()

        let add iss =
            if not (skipped iss.Code) then
                acc.Add iss

        for i in snap.Interactions do
            if BattleMessageCatalog.isActionStartCommand i.CommandNo then
                if i.InteractionType = InteractionType.Harm then
                    add (
                        issue
                            $"start_as_harm-{i.CommandNo}"
                            1
                            false
                            BucketMissing
                            "0x28"
                            (Some i.CommandNo)
                            (Some i.MessageId)
                            None
                            (Some i.TimestampMs)
                            $"start cmd {i.CommandNo} classified as Harm"
                    )

                match parseSpellArg i.ActionName with
                | Some arg when isLikelyFourcc arg ->
                    add (
                        issue
                            $"fourcc_as_spell-{arg}"
                            1
                            false
                            BucketMissing
                            "0x28"
                            (Some i.CommandNo)
                            (Some i.MessageId)
                            None
                            (Some i.TimestampMs)
                            $"start actionName {i.ActionName} treats fourcc as a spell id"
                    )
                | _ -> ()
            elif i.CommandNo = 4 then
                match parseSpellArg i.ActionName with
                | Some arg when isLikelyFourcc arg ->
                    add (
                        issue
                            $"fourcc_as_spell-{arg}"
                            1
                            false
                            BucketMissing
                            "0x28"
                            (Some i.CommandNo)
                            (Some i.MessageId)
                            None
                            (Some i.TimestampMs)
                            $"magic finish cmd_arg looks like fourcc ({i.ActionName}); finish arg should be a spell id"
                    )
                | _ -> ()

                if i.MessageId > 0 && not (isMessageClassified i.MessageId) then
                    add (
                        issue
                            $"unclassified_message-{i.MessageId}"
                            3
                            false
                            BucketMissing
                            "0x28"
                            (Some i.CommandNo)
                            (Some i.MessageId)
                            None
                            (Some i.TimestampMs)
                            $"cmd 4 messageId {i.MessageId} not in ParseCodes or MsgBasicCatalog"
                    )

        let nameKnown = EntityRegistry.localPlayerName().IsSome

        for c in snap.ChatMessages do
            if c.Mode.StartsWith("Mode 0x", StringComparison.Ordinal) then
                add (
                    issue
                        $"unknown_kind-0x{c.ModeId:X2}"
                        2
                        false
                        BucketMissing
                        (if c.PacketId = 0xB5 then "0xB5" else "0x17")
                        None
                        None
                        (Some c.ModeId)
                        (Some c.TimestampMs)
                        $"chat Kind 0x{c.ModeId:X2} is not in ChatCommon.modeNames"
                )

            let namelessSelf =
                ChatCommon.isNamelessSelfKind c.ModeId
                || (c.PacketId = 0xB5 && ChatIngest.isPlaceholderSpeaker c.Speaker)

            if
                namelessSelf
                && ChatIngest.isPlaceholderSpeaker c.Speaker
                && nameKnown
            then
                add (
                    issue
                        "nameless_self_unnamed"
                        4
                        false
                        BucketMissing
                        (if c.PacketId = 0xB5 then "0xB5" else "0x17")
                        None
                        None
                        (Some c.ModeId)
                        (Some c.TimestampMs)
                        "nameless self-chat speaker still Unknown after local player name is known"
                )

        let unnamed =
            snap.Combatants
            |> List.filter (fun c -> c.Name.StartsWith("Entity ", StringComparison.Ordinal))

        if unnamed.Length > 0 then
            add (
                issue
                    "unnamed_entities"
                    5
                    true
                    BucketDeferred
                    "0x0E"
                    None
                    None
                    None
                    None
                    $"combatants with unresolved names: {unnamed.Length} (deferred; often missing 0x00E)"
            )

        acc
        |> Seq.distinctBy (fun i -> i.Code)
        |> Seq.sortBy (fun i -> i.Rank, i.Code)
        |> Seq.toList

    let evaluate (snap: AnalyticsSnapshot) (skip: Set<string>) =
        let issues = collect snap skip
        let deferred, actionable = issues |> List.partition (fun i -> i.Deferred)

        { Actionable = actionable
          Deferred = deferred }

    let targetedPresent (report: Report) (filter: string) =
        report.Actionable |> List.exists (fun i -> matchesCode filter i.Code)

    let printReport (report: Report) (targeted: string option) =
        match targeted with
        | Some code when targetedPresent report code ->
            printfn "settled=FAIL targeted=%s still present" code

            for i in report.Actionable do
                if matchesCode code i.Code then
                    printfn
                        "  rank=%d code=%s bucket=%s opcode=%s commandNo=%A messageId=%A kind=%A ts=%A"
                        i.Rank
                        i.Code
                        i.Bucket
                        i.Opcode
                        i.CommandNo
                        i.MessageId
                        i.Kind
                        i.TimestampMs

                    printfn "    %s" i.Message

            false
        | Some code ->
            printfn
                "settled=OK targeted=%s remaining=%d deferred=%d"
                code
                report.Actionable.Length
                report.Deferred.Length

            for i in report.Actionable do
                printfn "  leftover [%s] %s" i.Code i.Message

            true
        | None when report.Actionable.IsEmpty ->
            printfn "settled=OK actionable=0 deferred=%d" report.Deferred.Length

            for i in report.Deferred do
                printfn "  deferred [%s] %s" i.Code i.Message

            true
        | None ->
            printfn
                "settled=FAIL actionable=%d deferred=%d"
                report.Actionable.Length
                report.Deferred.Length

            for i in report.Actionable do
                printfn
                    "  rank=%d code=%s bucket=%s opcode=%s commandNo=%A messageId=%A kind=%A ts=%A"
                    i.Rank
                    i.Code
                    i.Bucket
                    i.Opcode
                    i.CommandNo
                    i.MessageId
                    i.Kind
                    i.TimestampMs

                printfn "    %s" i.Message

            for i in report.Deferred do
                printfn "  deferred [%s] %s" i.Code i.Message

            false
