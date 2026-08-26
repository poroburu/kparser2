open System
open System.Diagnostics
open System.IO
open System.Text.Json
open System.Threading
open System.Threading.Tasks
open kparser2.Core
open kparser2.Decoders
open kparser2.Ingest
open kparser2.Protocol
open kparser2.Abstractions
open kparser2.Analytics

let private printJson value =
    printfn "%s" (JsonSerializer.Serialize(value, JsonSerializerOptions(WriteIndented = true)))

let private eventToJson (event: DecoderEvent) =
    match event with
    | DecoderEvent.Chat chat ->
        box
            {| kind = "chat"
               mode = chat.Mode
               speaker = chat.Speaker
               message = chat.Message
               isGm = chat.IsGm |}
    | DecoderEvent.Loot loot ->
        box
            {| kind = "loot"
               eventType = string loot.EventType
               itemId = loot.ItemId
               itemName = loot.ItemName
               quantity = loot.Quantity
               gil = loot.Gil
               poolSlot = loot.PoolSlot
               actorName = loot.ActorName
               detail = loot.Detail |}
    | DecoderEvent.CombatMessage message ->
        box
            {| kind = "combat_message"
               casterId = message.CasterId
               targetId = message.TargetId
               messageNum = message.MessageNum
               messageType = message.MessageType
               param1 = message.Param1
               param2 = message.Param2 |}
    | DecoderEvent.CombatAction action ->
        box
            {| kind = "combat_action"
               actorId = action.ActorId
               commandNo = action.CommandNo
               commandArg = action.CommandArg
               info = action.Info
               targets = action.Targets |}

let private runDecode (path: string) (opcodeFilter: int option) (asJson: bool) =
    let mutable count = 0

    for topic, metaJson, data in Ndjson.readAll path do
        let meta = PacketMeta.parseString metaJson
        let evt = PacketMeta.toEvent topic meta data

        let matches =
            opcodeFilter
            |> Option.map (fun op -> int evt.PacketId = op)
            |> Option.defaultValue true

        if matches then
            let decoded = DecoderRegistry.decode evt

            if not (List.isEmpty decoded.Events) then
                count <- count + 1

                if asJson then
                    printJson
                        {| packetId = evt.PacketId
                           packetName = evt.PacketName
                           timestamp = evt.Timestamp
                           events = decoded.Events |> List.map eventToJson |}
                else
                    printfn "0x%04X %s" evt.PacketId evt.PacketName

                    for event in decoded.Events do
                        match event with
                        | DecoderEvent.Chat chat ->
                            printfn "  chat [%s] %s: %s" chat.Mode chat.Speaker chat.Message
                        | DecoderEvent.Loot loot ->
                            printfn "  loot %A item=%s (%d) actor=%s" loot.EventType loot.ItemName loot.ItemId loot.ActorName
                        | DecoderEvent.CombatMessage message ->
                            printfn "  combat msg caster=%d target=%d num=0x%X" message.CasterId message.TargetId message.MessageNum
                        | DecoderEvent.CombatAction action ->
                            printfn "  combat action actor=%d cmd=%d targets=%d" action.ActorId action.CommandNo action.Targets.Length

    if not asJson then
        printfn "Decoded %d packets with structured events from %s" count path

    count

let private runReplay (path: string) (opcodeFilter: int option) (asJson: bool) =
    use session = PacketSessionFactory.fromReplayDefault path
    let packetSession = session :> IPacketSession

    let mutable count = 0
    let subscription =
        packetSession.Packets.Subscribe(
            fun row ->
                let matches =
                    opcodeFilter
                    |> Option.map (fun op -> row.PacketId = op)
                    |> Option.defaultValue true

                if matches then
                    count <- count + 1

                    if asJson then
                        printJson row
                    else
                        printfn
                            "0x%04X %s %s %s (%d bytes)"
                            row.PacketId
                            row.Direction
                            row.PacketName
                            row.Topic
                            row.Size
        )

    session.WaitForReplayComplete()
    subscription.Dispose()

    let stats = packetSession.GetStatsAsync().GetAwaiter().GetResult()

    if not asJson then
        printfn "Processed %d matching packets (total %d)" count (int stats.TotalPackets)

    stats

let private tryPluginEcho (text: string) =
    try
        use client = new CommandClient("tcp://localhost:5556")

        match client.Echo(text) with
        | Some resp ->
            printfn "plugin echo: %s" resp
            true
        | None ->
            printfn "plugin echo skipped: kpacket :5556 not reachable"
            false
    with ex ->
        printfn "plugin echo failed: %s" ex.Message
        false

let private runStats (path: string option) =
    match path with
    | Some p -> runReplay p None false |> ignore
    | None ->
        use client = CommandClientFactory.Create() :> IDisposable

        match (client :?> CommandClient).Stats() with
        | Some response -> printfn "%s" response
        | None -> printfn "Unable to reach kpacket2 command socket on tcp://localhost:5556"

let private runHello () =
    use client = CommandClientFactory.Create() :> IDisposable

    match (client :?> CommandClient).Hello() with
    | Some response -> printfn "%s" response
    | None -> printfn "Unable to reach kpacket2 command socket on tcp://localhost:5556"

let private runProbe () =
    match ConnectionProbe.helloInfo() with
    | None -> printfn "command socket (:5556): OFFLINE"
    | Some h ->
        printfn
            "command socket (:5556): OK  session_uuid=%s version=%s"
            h.session_uuid
            h.version
        tryPluginEcho "kparser2 probe: live path OK" |> ignore

    match ConnectionProbe.playerName() with
    | Some name -> printfn "player_name=%s" name
    | None -> printfn "player_name=(unavailable)"

    match ConnectionProbe.pluginStats() with
    | None -> printfn "plugin stats: unavailable"
    | Some s ->
        printfn
            "plugin stats: packets_published=%d pub_send_errors=%d filtered=%d"
            s.packets_published
            s.pub_send_errors
            s.packets_filtered

    use source = LivePacketSource("tcp://localhost:5555") :> IPacketSource
    printfn "listening on tcp://localhost:5555 for 5s..."
    Thread.Sleep(5000)

    let diag = (source :?> LivePacketSource).Diagnostics

    printfn
        "subscriber: connected=%b zmq_messages=%d parsed=%d parse_errors=%d reconnects=%d"
        diag.Connected
        diag.RawFramesReceived
        diag.PacketsReceived
        diag.ParseErrors
        diag.Reconnects

    if not (String.IsNullOrWhiteSpace diag.LastError) then
        printfn "subscriber last error: %s" diag.LastError

    if diag.PacketsReceived = 0 && diag.RawFramesReceived = 0 then
        printfn "FAIL: no ZMQ frames received (compare with kpacket2 packet_monitor.exe)"
    elif diag.ParseErrors > 0 then
        printfn "FAIL: frames received but JSON parsing failed"
    else
        printfn "OK: live ingest path is receiving packets"

let private runWatch (durationMs: int) (intervalMs: int) (analytics: bool) =
    if analytics then
        use session = PacketSessionFactory.fromLiveDefault() :> IAnalyticsSession
        let deadline = DateTime.UtcNow.AddMilliseconds(float durationMs)

        while DateTime.UtcNow < deadline do
            let snap = session.GetSnapshot()
            let unnamed =
                snap.Combatants
                |> Seq.filter (fun c -> c.Name.StartsWith("Entity ", StringComparison.Ordinal))
                |> Seq.length

            printfn
                "[%s] fights=%d interactions=%d combatants=%d unnamed=%d zone=%s"
                (DateTime.Now.ToString("HH:mm:ss"))
                snap.Battles.Count
                snap.Interactions.Count
                snap.Combatants.Count
                unnamed
                snap.ZoneName

            Thread.Sleep(intervalMs)

        0
    else
        use source = LivePacketSource("tcp://localhost:5555") :> IPacketSource
        let live = source :?> LivePacketSource
        let deadline = DateTime.UtcNow.AddMilliseconds(float durationMs)
        let mutable lastPublished = 0L

        while DateTime.UtcNow < deadline do
            let pluginStats = ConnectionProbe.pluginStats()
            let diag = live.Diagnostics
            let mutable ingested = 0
            let mutable evt = Unchecked.defaultof<PacketEvent>

            while source.Packets.TryRead(&evt) do
                ingested <- ingested + 1

            let published =
                pluginStats |> Option.map (fun s -> s.packets_published) |> Option.defaultValue 0L

            let pubDelta = published - lastPublished
            lastPublished <- published

            printfn
                "[%s] plugin +%d (total %d) | zmq %d parsed %d parse_err %d session_ingested %d reconnects %d"
                (DateTime.Now.ToString("HH:mm:ss"))
                pubDelta
                published
                diag.RawFramesReceived
                diag.PacketsReceived
                diag.ParseErrors
                ingested
                diag.Reconnects

            Thread.Sleep(intervalMs)

        0

let private runReport (queryId: string) (path: string) (live: bool) =
    if live then
        use session = PacketSessionFactory.fromLiveDefault() :> IAnalyticsSession
        let snap = session.GetSnapshot()
        let report = AnalyticsReportService.format queryId snap (MobFilterDto())

        for span in report.Spans do
            printf "%s" span.Text

        0
    else
        use session = PacketSessionFactory.fromReplayDefault path
        session.WaitForReplayComplete()
        let snap = (session :> IAnalyticsSession).GetSnapshot()
        let report = AnalyticsReportService.format queryId snap (MobFilterDto())

        for span in report.Spans do
            printf "%s" span.Text

        0

let private runRecord (output: string) (durationMs: int) (prompt: string option) (idleMs: int) (checkpointMs: int) =
    ConnectionProbe.tryBootstrapLocalPlayerName ()
    use source = LivePacketSource("tcp://localhost:5555") :> IPacketSource
    use writer = new StreamWriter(output)
    writer.AutoFlush <- true

    let recordStartMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
    let playerName = ConnectionProbe.playerName()

    Ndjson.writeSessionHeader writer playerName recordStartMs

    let secs = max 1 (durationMs / 1000)
    tryPluginEcho $"Recording started ({secs}s)" |> ignore

    match prompt with
    | Some p when not (String.IsNullOrWhiteSpace p) -> tryPluginEcho p |> ignore
    | _ -> ()

    let deadline = DateTime.UtcNow.AddMilliseconds(float durationMs)
    let mutable count = 0
    let mutable initialUuid = ConnectionProbe.sessionUuid () |> Option.defaultValue ""
    let mutable helloMisses = 0
    let mutable lastPublished = ConnectionProbe.publishedCount ()
    let mutable lastProgressUtc = DateTime.UtcNow
    let mutable poll = 0
    let mutable stopReason: RecordWatch.StopReason option = None
    let mutable lastCheckpointUtc = DateTime.UtcNow

    let considerUuid candidate =
        if String.IsNullOrWhiteSpace candidate then
            ()
        elif String.IsNullOrWhiteSpace initialUuid then
            initialUuid <- candidate
        else
            match RecordWatch.trySessionStop initialUuid candidate with
            | Some reason -> stopReason <- Some reason
            | None -> ()

    while DateTime.UtcNow < deadline && stopReason.IsNone do
        poll <- poll + 1

        if poll % 10 = 0 then
            match ConnectionProbe.helloInfo () with
            | None ->
                helloMisses <- helloMisses + 1

                if helloMisses >= 3 then
                    stopReason <- Some RecordWatch.StopReason.PluginOffline
            | Some hello ->
                helloMisses <- 0
                considerUuid hello.session_uuid

            if stopReason.IsNone then
                let published = ConnectionProbe.publishedCount ()

                match
                    RecordWatch.tryStallStop idleMs count published lastPublished lastProgressUtc DateTime.UtcNow
                with
                | Some reason -> stopReason <- Some reason
                | None ->
                    if published <> lastPublished then
                        lastPublished <- published
                        lastProgressUtc <- DateTime.UtcNow

        if stopReason.IsNone then
            let mutable evt = Unchecked.defaultof<PacketEvent>

            if source.Packets.WaitToReadAsync().AsTask().Wait(100) && source.Packets.TryRead(&evt) then
                considerUuid evt.SessionUuid

                if stopReason.IsNone then
                    let meta =
                        JsonSerializer.Serialize(
                            {| timestamp = evt.Timestamp
                               direction = PacketEvent.directionToString evt.Direction
                               packet_type = evt.PacketType
                               packet_id = evt.PacketId
                               packet_name = evt.PacketName
                               size = evt.Size
                               metadata =
                                {| injected = evt.Injected
                                   blocked = evt.Blocked
                                   chunk_size = 0
                                   session_id = evt.SessionUuid
                                   sync_count = 0 |}
                               version = evt.Version
                               session_uuid = evt.SessionUuid
                               message_id = evt.MessageId |}
                        )

                    Ndjson.writeLine writer evt.Topic meta evt.Data
                    count <- count + 1
                    lastProgressUtc <- DateTime.UtcNow

                    if RecordWatch.isLogoutPacket evt.PacketId evt.Direction then
                        stopReason <- Some RecordWatch.StopReason.Logout

        if
            checkpointMs > 0
            && stopReason.IsNone
            && (DateTime.UtcNow - lastCheckpointUtc).TotalMilliseconds >= float checkpointMs
        then
            printfn "record checkpoint: packets=%d" count
            lastCheckpointUtc <- DateTime.UtcNow

    writer.Flush ()

    match stopReason with
    | Some reason -> printfn "recording stopped: %s" (RecordWatch.label reason)
    | None -> ()

    printfn "Recorded %d packets to %s" count output

    if ConnectionProbe.isPluginReachable () then
        tryPluginEcho $"Recording complete ({count} packets)" |> ignore

let private jsonOptions = JsonSerializerOptions(WriteIndented = true)

let private chatParityRows (snap: AnalyticsSnapshotDto) =
    snap.ChatMessages
    |> Seq.filter (fun m ->
        String.IsNullOrWhiteSpace m.Direction
        || m.Direction.Equals("incoming", StringComparison.OrdinalIgnoreCase))
    |> Seq.map (fun m ->
        let speaker =
            if String.IsNullOrWhiteSpace m.Speaker then
                "System"
            else
                m.Speaker

        {| speaker = speaker
           mode = m.Mode
           message = m.Message |})
    |> Seq.toList

let private runAnalyticsSnapshot
    (path: string)
    (asJson: bool)
    (parityChat: bool)
    (output: string option)
    (assertCombat: bool)
    (minBattles: int option)
    (assertNames: bool)
    (assertChat: bool)
    (minChat: int option)
    (assertSettled: bool)
    (settledCode: string option)
    (skipCodes: Set<string>)
    =
    use session = PacketSessionFactory.fromReplayDefault path
    session.WaitForReplayComplete()
    let snap = (session :> IAnalyticsSession).GetSnapshot()

    let jsonText =
        if parityChat then
            JsonSerializer.Serialize(chatParityRows snap, jsonOptions)
        else
            JsonSerializer.Serialize(snap, jsonOptions)

    match output with
    | Some outPath -> File.WriteAllText(outPath, jsonText)
    | None -> ()

    if parityChat || asJson then
        printfn "%s" jsonText
    else
        let fSnap = AnalyticsDtoMapping.fromSnapshotDto snap
        let offense = AnalyticsQueries.offenseSummary fSnap MobFilter.defaultFilter

        printfn
            "interactions=%d battles=%d combatants=%d chat=%d loot=%d items=%d experience=%d"
            snap.Interactions.Count
            snap.Battles.Count
            snap.Combatants.Count
            snap.ChatMessages.Count
            snap.LootRecords.Count
            snap.ItemUses.Count
            snap.ExperienceRecords.Count

        if snap.ChatMessages.Count > 0 then
            printfn "chat speakers:"

            for msg in snap.ChatMessages |> Seq.truncate 8 do
                printfn "  [%s] %s: %s" msg.Mode msg.Speaker msg.Message

        if offense.Length > 0 then
            printfn "offense by category:"

            for row in offense do
                printfn "  %s: %d (%d hits)" row.Label row.Total row.Count

    let fSnap = AnalyticsDtoMapping.fromSnapshotDto snap
    let mutable ok = true

    if assertChat || minChat.IsSome then
        let report = AnalyticsValidate.validateChat fSnap (defaultArg minChat 1)

        if not (AnalyticsValidate.printReport report) then
            ok <- false

    if assertCombat || minBattles.IsSome || assertNames then
        let report =
            if assertNames then
                AnalyticsValidate.validateNames fSnap
            elif minBattles.IsSome then
                AnalyticsValidate.validateMultiFight fSnap minBattles.Value
            else
                AnalyticsValidate.validateCombat fSnap

        if not (AnalyticsValidate.printReport report) then
            ok <- false

    if assertSettled || settledCode.IsSome then
        let report = SettledDivergence.evaluate fSnap skipCodes

        if not (SettledDivergence.printReport report settledCode) then
            ok <- false

    if not ok then
        failwith "Snapshot validation failed"

    snap

let private runExportReport (path: string) (output: string) =
    use session = PacketSessionFactory.fromReplayDefault path
    session.WaitForReplayComplete()
    let exporter = FileReportExporter() :> IReportExporter

    exporter.ExportAsync(output, (session :> IAnalyticsSession).GetSnapshot(), Path.GetFileNameWithoutExtension path)
        .GetAwaiter()
        .GetResult()

    printfn "Exported report to %s" output

let private runImportReport (path: string) (validateOnly: bool) =
    let importer = FileReportImporter() :> IReportImporter

    if validateOnly then
        let ok = importer.ValidateAsync(path).GetAwaiter().GetResult()
        printfn "validate=%b" ok
    else
        let snap = importer.ImportAsync(path).GetAwaiter().GetResult()

        printfn
            "imported interactions=%d battles=%d zone=%s"
            snap.Interactions.Count
            snap.Battles.Count
            snap.ZoneName

let private resolveConverterScript () =
    let candidates =
        [ Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "scripts", "convert-packetviewer-to-ndjson.ps1"))
          Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "scripts", "convert-packetviewer-to-ndjson.ps1")) ]

    candidates |> List.tryFind File.Exists

let private runImportPacketViewer (fullLog: string option) (incomingLog: string option) (outgoingLog: string option) (output: string) (sessionId: string option) =
    match resolveConverterScript () with
    | None ->
        eprintfn "convert-packetviewer-to-ndjson.ps1 not found"
        1
    | Some scriptPath ->
        let argParts = ResizeArray<string>()
        argParts.Add("-NoProfile")
        argParts.Add("-ExecutionPolicy")
        argParts.Add("Bypass")
        argParts.Add("-File")
        argParts.Add($"\"{scriptPath}\"")
        argParts.Add("-OutputNdjson")
        argParts.Add($"\"{output}\"")

        match fullLog with
        | Some path ->
            argParts.Add("-FullLog")
            argParts.Add($"\"{path}\"")
        | None -> ()

        match incomingLog with
        | Some path ->
            argParts.Add("-IncomingLog")
            argParts.Add($"\"{path}\"")
        | None -> ()

        match outgoingLog with
        | Some path ->
            argParts.Add("-OutgoingLog")
            argParts.Add($"\"{path}\"")
        | None -> ()

        match sessionId with
        | Some id ->
            argParts.Add("-SessionId")
            argParts.Add($"\"{id}\"")
        | None -> ()

        let psi =
            ProcessStartInfo(
                FileName = "powershell",
                Arguments = String.concat " " (argParts |> Seq.toList),
                UseShellExecute = false
            )

        use proc = Process.Start(psi)
        proc.WaitForExit()

        if proc.ExitCode = 0 then
            printfn "Imported PacketViewer logs to %s" output
            0
        else
            eprintfn "PacketViewer import failed with exit code %d" proc.ExitCode
            1

let private runImportValidate (path: string) (assertCombat: bool) (minBattles: int option) (assertNames: bool) =
    EntityRegistry.reset()
    InteractionBuilder.reset()

    match Ndjson.tryPlayerName path with
    | Some name -> EntityRegistry.registerLocalPlayerName name
    | None -> ()

    ConnectionProbe.tryBootstrapLocalPlayerName ()

    let store = SessionStore.create()
    let opCounts = System.Collections.Generic.Dictionary<int, int>()
    let mutable total = 0

    for topic, metaJson, data in Ndjson.readAll path do
        total <- total + 1
        let meta = PacketMeta.parseString metaJson
        let evt = PacketMeta.toEvent topic meta data
        let op = int evt.PacketId

        if opCounts.ContainsKey op then
            opCounts.[op] <- opCounts.[op] + 1
        else
            opCounts.[op] <- 1

        let decoded = DecoderRegistry.decode evt
        SessionStore.ingest store evt decoded

    let snap = SessionStore.snapshot store

    printfn "capture=%s" (Path.GetFileName path)
    printfn "total_packets=%d" total
    printfn "entities=%d" (EntityRegistry.allEntityIds().Length)
    printfn "local_player=%A" (EntityRegistry.tryLocalPlayerId())
    printfn "local_player_name=%A" (EntityRegistry.localPlayerName())
    printfn "zone_id=%A" (EntityRegistry.tryGetZoneId())
    printfn "zone_name=%s" snap.ZoneName
    printfn "interactions=%d battles=%d combatants=%d chat=%d loot=%d xp=%d" snap.Interactions.Length snap.Battles.Length snap.Combatants.Length snap.ChatMessages.Length snap.LootRecords.Length snap.ExperienceRecords.Length

    printfn "opcode_top:"
    opCounts
    |> Seq.sortByDescending (fun kv -> kv.Value)
    |> Seq.truncate 10
    |> Seq.iter (fun kv -> printfn "  0x%04X=%d" kv.Key kv.Value)

    printfn "combatants:"
    snap.Combatants
    |> List.sortBy (fun c -> c.Name)
    |> List.truncate 12
    |> List.iter (fun c -> printfn "  %s (%A) id=%d" c.Name c.Kind c.Id)

    if assertCombat || minBattles.IsSome || assertNames then
        let report =
            if assertNames then
                AnalyticsValidate.validateNames snap
            elif minBattles.IsSome then
                AnalyticsValidate.validateMultiFight snap minBattles.Value
            else
                AnalyticsValidate.validateCombat snap

        if AnalyticsValidate.printReport report then
            0
        else
            1
    else
        0

[<EntryPoint>]
let main argv =
    if argv.Length = 0 then
        printfn "Usage:"
        printfn "  kparser2.cli replay <file.ndjson> [--filter 0x17] [--json]"
        printfn "  kparser2.cli stats [--replay <file.ndjson>]"
        printfn "  kparser2.cli hello"
        printfn "  kparser2.cli probe"
        printfn "  kparser2.cli echo <text>"
        printfn "  kparser2.cli watch [--duration-ms 30000] [--interval-ms 2000] [--analytics]"
        printfn "  kparser2.cli record <file.ndjson> [--duration-ms 5000] [--idle-ms 180000] [--checkpoint-ms 0] [--prompt text]"
        printfn "  kparser2.cli decode <file.ndjson> [--filter 0x17] [--json]"
        printfn "  kparser2.cli report <queryId> <file.ndjson> [--live]"
        printfn "  kparser2.cli export-items [--sql <path>] [--output <path>]"
        printfn "  kparser2.cli export-actions [--sql <path>] [--output <path>]"
        printfn "  kparser2.cli export-spells [--sql <path>] [--output <path>]"
        printfn "  kparser2.cli analytics snapshot <file.ndjson> [--json] [--parity-chat] [-o|--output out.json] [--assert-combat] [--assert-chat] [--assert-names] [--min-battles N] [--min-chat N] [--assert-settled] [--assert-settled-code CODE] [--skip-code CODE]"
        printfn "  kparser2.cli export report <file.ndjson> -o <file.kparse2.json>"
        printfn "  kparser2.cli import report <file.kparse2.json> [--validate]"
        printfn "  kparser2.cli import packetviewer [--full path.log | --incoming in.log [--outgoing out.log]] -o capture.ndjson [--session-id name]"
        printfn "  kparser2.cli import packetviewer --validate capture.ndjson [--assert-combat] [--assert-names] [--min-battles N]"
        printfn "  kparser2.cli export-zones [--sql <path>] [--output <path>]"
        1
    else
        try
            match argv.[0].ToLowerInvariant() with
            | "replay" when argv.Length >= 2 ->
                let path = argv.[1]
                let mutable filter = None
                let mutable asJson = false
                let mutable i = 2

                while i < argv.Length do
                    match argv.[i] with
                    | "--filter" when i + 1 < argv.Length ->
                        let token = argv.[i + 1].Replace("0x", "").Replace("0X", "")
                        filter <- Some(Convert.ToInt32(token, 16))
                        i <- i + 2
                    | "--json" ->
                        asJson <- true
                        i <- i + 1
                    | _ -> i <- i + 1

                runReplay path filter asJson |> ignore
                0
            | "stats" ->
                let mutable path = None
                let mutable i = 1

                while i < argv.Length do
                    match argv.[i] with
                    | "--replay" when i + 1 < argv.Length ->
                        path <- Some argv.[i + 1]
                        i <- i + 2
                    | _ -> i <- i + 1

                runStats path
                0
            | "hello" ->
                runHello ()
                0
            | "echo" when argv.Length >= 2 ->
                let text = String.Join(" ", argv.[1..])

                if tryPluginEcho text then 0 else 1
            | "probe" ->
                runProbe ()
                0
            | "watch" ->
                let mutable duration = 30000
                let mutable interval = 2000
                let mutable analytics = false
                let mutable i = 1

                while i < argv.Length do
                    match argv.[i] with
                    | "--duration-ms" when i + 1 < argv.Length ->
                        duration <- Int32.Parse argv.[i + 1]
                        i <- i + 2
                    | "--interval-ms" when i + 1 < argv.Length ->
                        interval <- Int32.Parse argv.[i + 1]
                        i <- i + 2
                    | "--analytics" ->
                        analytics <- true
                        i <- i + 1
                    | _ -> i <- i + 1

                runWatch duration interval analytics
            | "record" when argv.Length >= 2 ->
                let output = argv.[1]
                let mutable duration = 5000
                let mutable idleMs = 180_000
                let mutable checkpointMs = 0
                let mutable prompt = None
                let mutable i = 2

                while i < argv.Length do
                    match argv.[i] with
                    | "--duration-ms" when i + 1 < argv.Length ->
                        duration <- Int32.Parse argv.[i + 1]
                        i <- i + 2
                    | "--idle-ms" when i + 1 < argv.Length ->
                        idleMs <- Int32.Parse argv.[i + 1]
                        i <- i + 2
                    | "--checkpoint-ms" when i + 1 < argv.Length ->
                        checkpointMs <- Int32.Parse argv.[i + 1]
                        i <- i + 2
                    | "--prompt" when i + 1 < argv.Length ->
                        prompt <- Some argv.[i + 1]
                        i <- i + 2
                    | _ -> i <- i + 1

                runRecord output duration prompt idleMs checkpointMs
                0
            | "report" when argv.Length >= 3 ->
                let queryId = argv.[1]
                let mutable live = false
                let mutable path = argv.[2]
                let mutable i = 3

                while i < argv.Length do
                    match argv.[i] with
                    | "--live" ->
                        live <- true
                        i <- i + 1
                    | _ -> i <- i + 1

                runReport queryId path live
            | "decode" when argv.Length >= 2 ->
                let path = argv.[1]
                let mutable filter = None
                let mutable asJson = false
                let mutable i = 2

                while i < argv.Length do
                    match argv.[i] with
                    | "--filter" when i + 1 < argv.Length ->
                        let token = argv.[i + 1].Replace("0x", "").Replace("0X", "")
                        filter <- Some(Convert.ToInt32(token, 16))
                        i <- i + 2
                    | "--json" ->
                        asJson <- true
                        i <- i + 1
                    | _ -> i <- i + 1

                runDecode path filter asJson |> ignore
                0
            | "analytics" when argv.Length >= 3 && argv.[1].ToLowerInvariant() = "snapshot" ->
                let path = argv.[2]
                let mutable asJson = false
                let mutable parityChat = false
                let mutable output = None
                let mutable assertCombat = false
                let mutable assertNames = false
                let mutable assertChat = false
                let mutable minBattles = None
                let mutable minChat = None
                let mutable assertSettled = false
                let mutable settledCode = None
                let mutable skipCodes = Set.empty
                let mutable i = 3

                while i < argv.Length do
                    match argv.[i] with
                    | "--json" ->
                        asJson <- true
                        i <- i + 1
                    | "--parity-chat" ->
                        parityChat <- true
                        i <- i + 1
                    | arg when (arg = "-o" || arg = "--output") && i + 1 < argv.Length ->
                        output <- Some argv.[i + 1]
                        i <- i + 2
                    | "--assert-combat" ->
                        assertCombat <- true
                        i <- i + 1
                    | "--assert-chat" ->
                        assertChat <- true
                        i <- i + 1
                    | "--assert-names" ->
                        assertNames <- true
                        i <- i + 1
                    | "--min-battles" when i + 1 < argv.Length ->
                        minBattles <- Some(Int32.Parse argv.[i + 1])
                        assertCombat <- true
                        i <- i + 2
                    | "--min-chat" when i + 1 < argv.Length ->
                        minChat <- Some(Int32.Parse argv.[i + 1])
                        assertChat <- true
                        i <- i + 2
                    | "--assert-settled" ->
                        assertSettled <- true
                        i <- i + 1
                    | "--assert-settled-code" when i + 1 < argv.Length ->
                        settledCode <- Some argv.[i + 1]
                        i <- i + 2
                    | "--skip-code" when i + 1 < argv.Length ->
                        skipCodes <- skipCodes.Add argv.[i + 1]
                        i <- i + 2
                    | _ -> i <- i + 1

                runAnalyticsSnapshot
                    path
                    asJson
                    parityChat
                    output
                    assertCombat
                    minBattles
                    assertNames
                    assertChat
                    minChat
                    assertSettled
                    settledCode
                    skipCodes
                |> ignore

                0
            | "export" when argv.Length >= 4 && argv.[1].ToLowerInvariant() = "report" ->
                let path = argv.[2]
                let mutable output = "report.kparse2.json"
                let mutable i = 3

                while i < argv.Length do
                    match argv.[i] with
                    | "-o" when i + 1 < argv.Length ->
                        output <- argv.[i + 1]
                        i <- i + 2
                    | _ -> i <- i + 1

                runExportReport path output
                0
            | "import" when argv.Length >= 3 && argv.[1].ToLowerInvariant() = "report" ->
                let path = argv.[2]
                let mutable validateOnly = false
                let mutable i = 3

                while i < argv.Length do
                    match argv.[i] with
                    | "--validate" ->
                        validateOnly <- true
                        i <- i + 1
                    | _ -> i <- i + 1

                runImportReport path validateOnly
                0
            | "import" when argv.Length >= 3 && argv.[1].ToLowerInvariant() = "packetviewer" ->
                let mutable validateOnly = false
                let mutable assertCombat = false
                let mutable assertNames = false
                let mutable minBattles = None
                let mutable fullLog = None
                let mutable incomingLog = None
                let mutable outgoingLog = None
                let mutable output = "capture.ndjson"
                let mutable sessionId = None
                let mutable i = 2

                while i < argv.Length do
                    match argv.[i] with
                    | "--validate" when i + 1 < argv.Length ->
                        validateOnly <- true
                        output <- argv.[i + 1]
                        i <- i + 2
                    | "--assert-combat" ->
                        assertCombat <- true
                        i <- i + 1
                    | "--assert-names" ->
                        assertNames <- true
                        i <- i + 1
                    | "--min-battles" when i + 1 < argv.Length ->
                        minBattles <- Some(Int32.Parse argv.[i + 1])
                        assertCombat <- true
                        i <- i + 2
                    | "--full" when i + 1 < argv.Length ->
                        fullLog <- Some argv.[i + 1]
                        i <- i + 2
                    | "--incoming" when i + 1 < argv.Length ->
                        incomingLog <- Some argv.[i + 1]
                        i <- i + 2
                    | "--outgoing" when i + 1 < argv.Length ->
                        outgoingLog <- Some argv.[i + 1]
                        i <- i + 2
                    | "-o" when i + 1 < argv.Length ->
                        output <- argv.[i + 1]
                        i <- i + 2
                    | "--session-id" when i + 1 < argv.Length ->
                        sessionId <- Some argv.[i + 1]
                        i <- i + 2
                    | _ -> i <- i + 1

                if validateOnly then
                    runImportValidate output assertCombat minBattles assertNames
                else
                    runImportPacketViewer fullLog incomingLog outgoingLog output sessionId
            | "export-items" ->
                let mutable sqlPath =
                    Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "server", "sql", "item_basic.sql"))

                let mutable outputPath =
                    Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "data", "items.json"))

                let mutable i = 1

                while i < argv.Length do
                    match argv.[i] with
                    | "--sql" when i + 1 < argv.Length ->
                        sqlPath <- argv.[i + 1]
                        i <- i + 2
                    | "--output" when i + 1 < argv.Length ->
                        outputPath <- argv.[i + 1]
                        i <- i + 2
                    | _ -> i <- i + 1

                let scriptPath =
                    Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "scripts", "export-items.ps1"))

                if not (File.Exists scriptPath) then
                    eprintfn "export-items script not found: %s" scriptPath
                    1
                else
                    let psi =
                        ProcessStartInfo(
                            FileName = "powershell",
                            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" -SqlPath \"{sqlPath}\" -OutputPath \"{outputPath}\"",
                            UseShellExecute = false
                        )

                    use proc = Process.Start(psi)
                    proc.WaitForExit()

                    if proc.ExitCode = 0 then
                        printfn "Exported items to %s" outputPath
                        0
                    else
                        eprintfn "export-items failed with exit code %d" proc.ExitCode
                        1
            | "export-actions" ->
                let mutable sqlPath =
                    Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "server", "sql", "abilities.sql"))

                let mutable outputPath =
                    Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "data", "actions.json"))

                let mutable i = 1

                while i < argv.Length do
                    match argv.[i] with
                    | "--sql" when i + 1 < argv.Length ->
                        sqlPath <- argv.[i + 1]
                        i <- i + 2
                    | "--output" when i + 1 < argv.Length ->
                        outputPath <- argv.[i + 1]
                        i <- i + 2
                    | _ -> i <- i + 1

                let scriptPath =
                    Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "scripts", "export-actions.ps1"))

                if not (File.Exists scriptPath) then
                    eprintfn "export-actions script not found: %s" scriptPath
                    1
                else
                    let psi =
                        ProcessStartInfo(
                            FileName = "powershell",
                            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" -SqlPath \"{sqlPath}\" -OutputPath \"{outputPath}\"",
                            UseShellExecute = false
                        )

                    use proc = Process.Start(psi)
                    proc.WaitForExit()

                    if proc.ExitCode = 0 then
                        printfn "Exported actions to %s" outputPath
                        0
                    else
                        eprintfn "export-actions failed with exit code %d" proc.ExitCode
                        1
            | "export-spells" ->
                let mutable sqlPath =
                    Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "server", "sql", "spell_list.sql"))

                let mutable outputPath =
                    Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "data", "spells.json"))

                let mutable i = 1

                while i < argv.Length do
                    match argv.[i] with
                    | "--sql" when i + 1 < argv.Length ->
                        sqlPath <- argv.[i + 1]
                        i <- i + 2
                    | "--output" when i + 1 < argv.Length ->
                        outputPath <- argv.[i + 1]
                        i <- i + 2
                    | _ -> i <- i + 1

                let scriptPath =
                    Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "scripts", "export-spells.ps1"))

                if not (File.Exists scriptPath) then
                    eprintfn "export-spells script not found: %s" scriptPath
                    1
                else
                    let psi =
                        ProcessStartInfo(
                            FileName = "powershell",
                            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" -SqlPath \"{sqlPath}\" -OutputPath \"{outputPath}\"",
                            UseShellExecute = false
                        )

                    use proc = Process.Start(psi)
                    proc.WaitForExit()

                    if proc.ExitCode = 0 then
                        printfn "Exported spells to %s" outputPath
                        0
                    else
                        eprintfn "export-spells failed with exit code %d" proc.ExitCode
                        1
            | "export-zones" ->
                let mutable sqlPath =
                    Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "server", "sql", "zone_settings.sql"))

                let mutable outputPath =
                    Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "data", "zones.json"))

                let mutable i = 1

                while i < argv.Length do
                    match argv.[i] with
                    | "--sql" when i + 1 < argv.Length ->
                        sqlPath <- argv.[i + 1]
                        i <- i + 2
                    | "--output" when i + 1 < argv.Length ->
                        outputPath <- argv.[i + 1]
                        i <- i + 2
                    | _ -> i <- i + 1

                let scriptPath =
                    Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "scripts", "export-zones.ps1"))

                if not (File.Exists scriptPath) then
                    eprintfn "export-zones script not found: %s" scriptPath
                    1
                else
                    let psi =
                        ProcessStartInfo(
                            FileName = "powershell",
                            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" -SqlPath \"{sqlPath}\" -OutputPath \"{outputPath}\"",
                            UseShellExecute = false
                        )

                    use proc = Process.Start(psi)
                    proc.WaitForExit()

                    if proc.ExitCode = 0 then
                        printfn "Exported zones to %s" outputPath
                        0
                    else
                        eprintfn "export-zones failed with exit code %d" proc.ExitCode
                        1
            | _ ->
                printfn "Unknown command: %s" argv.[0]
                1
        with ex ->
            eprintfn "Error: %s" ex.Message
            1
