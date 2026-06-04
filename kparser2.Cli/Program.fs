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

let private runWatch (durationMs: int) (intervalMs: int) =
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

let private runRecord (output: string) (durationMs: int) =
    use source = LivePacketSource("tcp://localhost:5555") :> IPacketSource
    use writer = new StreamWriter(output)

    let deadline = DateTime.UtcNow.AddMilliseconds(float durationMs)
    let mutable count = 0

    while DateTime.UtcNow < deadline do
        let mutable evt = Unchecked.defaultof<PacketEvent>

        if source.Packets.WaitToReadAsync().AsTask().Wait(100) && source.Packets.TryRead(&evt) then
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

    printfn "Recorded %d packets to %s" count output

    printfn "Recorded %d packets to %s" count output

let private runAnalyticsSnapshot (path: string) (asJson: bool) =
    use session = PacketSessionFactory.fromReplayDefault path
    session.WaitForReplayComplete()
    let snap = (session :> IAnalyticsSession).GetSnapshot()

    if asJson then
        printJson snap
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

let private runImportValidate (path: string) =
    EntityRegistry.reset()
    InteractionBuilder.reset()
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

    0

[<EntryPoint>]
let main argv =
    if argv.Length = 0 then
        printfn "Usage:"
        printfn "  kparser2.cli replay <file.ndjson> [--filter 0x17] [--json]"
        printfn "  kparser2.cli stats [--replay <file.ndjson>]"
        printfn "  kparser2.cli hello"
        printfn "  kparser2.cli probe"
        printfn "  kparser2.cli watch [--duration-ms 30000] [--interval-ms 2000]"
        printfn "  kparser2.cli record <file.ndjson> [--duration-ms 5000]"
        printfn "  kparser2.cli decode <file.ndjson> [--filter 0x17] [--json]"
        printfn "  kparser2.cli export-items [--sql <path>] [--output <path>]"
        printfn "  kparser2.cli export-actions [--sql <path>] [--output <path>]"
        printfn "  kparser2.cli analytics snapshot <file.ndjson> [--json]"
        printfn "  kparser2.cli export report <file.ndjson> -o <file.kparse2.json>"
        printfn "  kparser2.cli import report <file.kparse2.json> [--validate]"
        printfn "  kparser2.cli import packetviewer [--full path.log | --incoming in.log [--outgoing out.log]] -o capture.ndjson [--session-id name]"
        printfn "  kparser2.cli import packetviewer --validate capture.ndjson"
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
            | "probe" ->
                runProbe ()
                0
            | "watch" ->
                let mutable duration = 30000
                let mutable interval = 2000
                let mutable i = 1

                while i < argv.Length do
                    match argv.[i] with
                    | "--duration-ms" when i + 1 < argv.Length ->
                        duration <- Int32.Parse argv.[i + 1]
                        i <- i + 2
                    | "--interval-ms" when i + 1 < argv.Length ->
                        interval <- Int32.Parse argv.[i + 1]
                        i <- i + 2
                    | _ -> i <- i + 1

                runWatch duration interval
            | "record" when argv.Length >= 2 ->
                let output = argv.[1]
                let mutable duration = 5000
                let mutable i = 2

                while i < argv.Length do
                    match argv.[i] with
                    | "--duration-ms" when i + 1 < argv.Length ->
                        duration <- Int32.Parse argv.[i + 1]
                        i <- i + 2
                    | _ -> i <- i + 1

                runRecord output duration
                0
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
                let mutable i = 3

                while i < argv.Length do
                    match argv.[i] with
                    | "--json" ->
                        asJson <- true
                        i <- i + 1
                    | _ -> i <- i + 1

                runAnalyticsSnapshot path asJson |> ignore
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
                    runImportValidate output
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
