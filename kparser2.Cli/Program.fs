open System
open System.IO
open System.Text.Json
open System.Threading
open System.Threading.Tasks
open kparser2.Core
open kparser2.Ingest
open kparser2.Protocol

let private printJson value =
    printfn "%s" (JsonSerializer.Serialize(value, JsonSerializerOptions(WriteIndented = true)))

let private runReplay (path: string) (opcodeFilter: int option) (asJson: bool) =
    use session = PacketSessionFactory.fromReplayDefault(path) :> kparser2.Abstractions.IPacketSession

    let mutable count = 0
    let subscription =
        session.Packets.Subscribe(
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

    Task.Delay(500).Wait()
    subscription.Dispose()

    let stats = session.GetStatsAsync().GetAwaiter().GetResult()

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
            | _ ->
                printfn "Unknown command: %s" argv.[0]
                1
        with ex ->
            eprintfn "Error: %s" ex.Message
            1
