namespace kparser2.Core

open System
open System.IO
open System.Text.Json
open System.Threading
open System.Threading.Channels
open System.Threading.Tasks
open kparser2.Ingest
open kparser2.Protocol

/// A snapshot source has no producer and can never acquire live packets.
type SnapshotPacketSource() =
    let channel = Channel.CreateUnbounded<PacketEvent>()
    do channel.Writer.TryComplete() |> ignore
    interface IPacketSource with
        member _.Packets = channel.Reader
        member _.WaitForCompletion() = ()
        member _.Dispose() = ()

/// Subscribe before replaying history. A separate pump journals the continuation
/// while the replay worker feeds history, then drains it in receive order.
type DesktopPacketSource(
    live: IPacketSource,
    path: string,
    resume: bool,
    uuid: string,
    afterMessageId: uint64,
    enforceBoundary: bool,
    boundaryMode: string,
    boundaryQuality: string,
    boundaryReason: string) =
    let output = Channel.CreateUnbounded<PacketEvent>()
    let pending = Channel.CreateUnbounded<PacketEvent>()
    let cts = new CancellationTokenSource()
    let mutable error = ""
    let mutable recovered = 0L
    let mutable restoring = resume
    let mutable ended = false
    let mutable changedSession = ""
    let mutable received = 0L
    let mutable lastMessageId = 0UL
    // A stable read handle limits recovery to the pre-launch prefix.
    let history = if resume && File.Exists path then Some(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) else None
    let historyLength = history |> Option.map (fun s -> s.Length) |> Option.defaultValue 0L

    let encode (evt: PacketEvent) =
        let meta = JsonSerializer.Serialize({| timestamp = evt.Timestamp; direction = PacketEvent.directionToString evt.Direction;
                                              packet_type = evt.PacketType; packet_id = evt.PacketId; packet_name = evt.PacketName;
                                              size = evt.Size; session_uuid = evt.SessionUuid; message_id = evt.MessageId; version = evt.Version;
                                              metadata = {| injected = evt.Injected; blocked = evt.Blocked; chunk_size = evt.Size; session_id = evt.SessionUuid; sync_count = 0 |} |})
        Ndjson.encode evt.Topic meta evt.Data

    let pump = Task.Run(Func<Task>(fun () -> (task {
        let mutable writer: StreamWriter option = None
        try
            try
                Directory.CreateDirectory(Path.GetDirectoryName(path)) |> ignore
                // Keep interrupted tails as evidence; a newline prevents corrupting the next record.
                writer <- Some(new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)))
                writer.Value.AutoFlush <- true
                if historyLength > 0L then writer.Value.WriteLine()
                else
                    Ndjson.writeSessionHeaderWithBoundary
                        writer.Value
                        None
                        (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
                        uuid
                        afterMessageId
                        boundaryMode
                        boundaryQuality
                        boundaryReason
            with ex -> error <- "Capture could not be saved: " + ex.Message
            let mutable running = true
            while running && not cts.IsCancellationRequested do
                let! ready = live.Packets.WaitToReadAsync(cts.Token).AsTask()
                if not ready then running <- false
                else
                    let mutable evt = Unchecked.defaultof<PacketEvent>
                    while live.Packets.TryRead(&evt) do
                        if evt.SessionUuid <> uuid then
                            if evt.SessionUuid <> "" then changedSession <- evt.SessionUuid
                        elif not ended && (not enforceBoundary || evt.MessageId > afterMessageId) then
                            received <- received + 1L
                            lastMessageId <- evt.MessageId
                            match writer with
                            | Some w ->
                                try w.WriteLine(encode evt)
                                with ex ->
                                    error <- "Recording stopped: " + ex.Message
                                    w.Dispose()
                                    writer <- None
                            | None -> ()
                            pending.Writer.TryWrite(evt) |> ignore
                            if RecordWatch.tryLogoutStop evt.PacketId evt.Direction evt.Data |> Option.isSome then ended <- true
        with
        | :? OperationCanceledException -> ()
        | ex -> error <- ex.Message
        writer |> Option.iter (fun w -> w.Dispose())
        pending.Writer.TryComplete() |> ignore
    } :> Task)))

    let replay = Task.Run(Func<Task>(fun () -> (task {
        try
            match history with
            | Some stream ->
                // Read only the initial length, including an interrupted tail but never new records.
                use reader = new BinaryReader(stream)
                use line = new MemoryStream()
                let mutable position = 0L
                while position < historyLength && not cts.IsCancellationRequested do
                    let b = reader.ReadByte()
                    position <- position + 1L
                    if b = 10uy then
                        let text = System.Text.Encoding.UTF8.GetString(line.ToArray())
                        line.SetLength(0L)
                        if not (String.IsNullOrWhiteSpace text) then
                            try
                                use doc = JsonDocument.Parse(text)
                                let mutable header = Unchecked.defaultof<JsonElement>
                                if not (doc.RootElement.TryGetProperty("type", &header)) then
                                    let topic, meta, data = Ndjson.decode text
                                    let evt = PacketMeta.toEvent topic (PacketMeta.parseString meta) data
                                    output.Writer.TryWrite(evt) |> ignore
                                    recovered <- recovered + 1L
                            with ex -> error <- "Recovery skipped a damaged record: " + ex.Message
                    else line.WriteByte(b)
                if line.Length > 0L then error <- "Recovery ignored an interrupted final record."
            | None -> ()
            restoring <- false
            let mutable running = true
            while running && not cts.IsCancellationRequested do
                let! ready = pending.Reader.WaitToReadAsync(cts.Token).AsTask()
                if not ready then running <- false
                else
                    let mutable evt = Unchecked.defaultof<PacketEvent>
                    while pending.Reader.TryRead(&evt) do output.Writer.TryWrite(evt) |> ignore
        with
        | :? OperationCanceledException -> ()
        | ex -> error <- "Recovery failed: " + ex.Message
        restoring <- false
        history |> Option.iter (fun s -> s.Dispose())
        output.Writer.TryComplete() |> ignore
    } :> Task)))

    member _.Error = error
    member _.IsRestoring = restoring
    member _.RecoveredPackets = recovered
    member _.ReceivedPackets = received
    member _.Ended = ended
    member _.ChangedSession = changedSession
    member _.LastMessageId = lastMessageId
    interface IPacketSource with
        member _.Packets = output.Reader
        member _.WaitForCompletion() = replay.GetAwaiter().GetResult()
        member _.Dispose() =
            live.Dispose()
            // Drain packets already accepted by the subscriber to the journal.
            pump.GetAwaiter().GetResult()
            cts.Cancel()
            Task.WhenAll(pump, replay).GetAwaiter().GetResult()
            cts.Dispose()
