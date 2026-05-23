namespace kparser2.Core

open System
open System.Reactive.Subjects
open System.Threading
open System.Threading.Tasks
open kparser2.Abstractions
open kparser2.Ingest
open kparser2.Protocol

type PacketSession(source: IPacketSource, sourceName: string, ?maxEntries: int) =
    let maxEntries = defaultArg maxEntries 5000
    let store = PacketStore(maxEntries)
    let packetSubject = Subject<PacketRowDto>()
    let chatSubject = Subject<ChatEventDto>()
    let lootSubject = Subject<LootEventDto>()
    let combatSubject = Subject<CombatEventDto>()
    let cts = new CancellationTokenSource()
    let mutable disposed = false
    let liveSource =
        match source with
        | :? LivePacketSource as live -> Some live
        | _ -> None
    let mutable knownPluginSession: string option = None

    let monitorPluginSession () =
        task {
            try
                while not cts.Token.IsCancellationRequested do
                    do! Task.Delay(2000, cts.Token)

                    match liveSource, ConnectionProbe.sessionUuid() with
                    | Some live, Some uuid when knownPluginSession <> Some uuid ->
                        live.Reconnect()
                        knownPluginSession <- Some uuid
                    | _ -> ()
            with
            | :? OperationCanceledException -> ()
            | ex -> printfn "PacketSession plugin monitor error: %s" ex.Message
        }

    let ingestLoop () =
        task {
            try
                let mutable running = true

                while running && not cts.Token.IsCancellationRequested do
                    let! canRead = source.Packets.WaitToReadAsync(cts.Token).AsTask()

                    if not canRead then
                        running <- false
                    else
                        let mutable evt = Unchecked.defaultof<PacketEvent>

                        while source.Packets.TryRead(&evt) do
                            let row, result = store.Add(evt)
                            packetSubject.OnNext(row)

                            for chat in result.ChatEvents do
                                chatSubject.OnNext(DtoMapping.toChatEvent evt chat)

                            for loot in result.LootEvents do
                                lootSubject.OnNext(DtoMapping.toLootEvent evt loot)

                            for combat in result.CombatEvents do
                                combatSubject.OnNext(DtoMapping.toCombatEvent evt combat)
            with
            | :? OperationCanceledException -> ()
            | ex -> printfn "PacketSession ingest error: %s" ex.Message
        }

    let _ingestTask = ingestLoop()
    let _monitorTask =
        match liveSource with
        | Some _ -> monitorPluginSession() :> Task
        | None -> Task.CompletedTask

    interface IPacketSession with
        member _.Packets = packetSubject :> IObservable<_>

        member _.ChatEvents = chatSubject :> IObservable<_>

        member _.LootEvents = lootSubject :> IObservable<_>

        member _.CombatEvents = combatSubject :> IObservable<_>

        member _.GetStatsAsync() =
            task {
                let subscriberStats =
                    liveSource |> Option.map (fun live -> live.Diagnostics)

                let subPackets =
                    subscriberStats |> Option.map (fun d -> d.PacketsReceived) |> Option.toNullable

                let subParseErrors =
                    subscriberStats |> Option.map (fun d -> d.ParseErrors) |> Option.toNullable

                let subReconnects =
                    subscriberStats |> Option.map (fun d -> d.Reconnects) |> Option.toNullable

                let subLastError =
                    subscriberStats
                    |> Option.bind (fun d ->
                        if String.IsNullOrWhiteSpace d.LastError then
                            None
                        else
                            Some d.LastError)
                    |> Option.toObj

                return
                    SessionStatsDto(
                        TotalPackets = store.TotalPackets,
                        ChatEvents = store.ChatCount,
                        LootEvents = store.LootCount,
                        CombatEvents = store.CombatCount,
                        IsConnected = not disposed,
                        Source = sourceName,
                        SubscriberPackets = subPackets,
                        SubscriberParseErrors = subParseErrors,
                        SubscriberReconnects = subReconnects,
                        SubscriberLastError = subLastError
                    )
            }
            :> Task<SessionStatsDto>

        member _.GetRecentPackets(count) = store.GetRecent(count)

        member _.GetRecentChatEvents(count) = store.GetChatEvents(count)

        member _.GetRecentLootEvents(count) = store.GetLootEvents(count)

        member _.GetRecentCombatEvents(count) = store.GetCombatEvents(count)

        member _.GetSelectedPacket() =
            store.Selected |> Option.toObj

        member _.SelectPacket(packet) =
            store.Selected <- Option.ofObj packet

        member _.Dispose() =
            if not disposed then
                disposed <- true
                cts.Cancel()
                packetSubject.OnCompleted()
                chatSubject.OnCompleted()
                lootSubject.OnCompleted()
                combatSubject.OnCompleted()

                try
                    (source :> IDisposable).Dispose()
                with _ ->
                    ()

                cts.Dispose()

module PacketSessionFactory =
    let fromLive(subEndpoint: string) =
        PacketSession(LivePacketSource(subEndpoint) :> IPacketSource, $"live:{subEndpoint}")

    let fromLiveDefault() =
        fromLive "tcp://localhost:5555"

    let fromReplay(path: string, speed: float) =
        PacketSession(ReplayPacketSource(path, speed = speed) :> IPacketSource, $"replay:{path}")

    let fromReplayDefault(path: string) =
        fromReplay (path, 0.0)
