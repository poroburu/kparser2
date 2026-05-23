namespace kparser2.Ingest

open System
open System.Threading
open System.Threading.Channels
open kparser2.Protocol
open NetMQ
open NetMQ.Sockets

type ZmqSubscriberDiagnostics =
    { Connected: bool
      PacketsReceived: int
      RawFramesReceived: int
      ParseErrors: int
      Reconnects: int
      LastError: string
      LastSessionUuid: string }

type ZmqSubscriber(subEndpoint: string) =
    let channel = Channel.CreateUnbounded<PacketEvent>()
    let cts = new CancellationTokenSource()
    let reconnectSignal = new AutoResetEvent(false)
    let socketLock = obj()
    let mutable activeSocket: SubscriberSocket option = None
    let mutable packetsReceived = 0
    let mutable rawFramesReceived = 0
    let mutable parseErrors = 0
    let mutable reconnects = 0
    let mutable lastError = ""
    let mutable connected = false
    let mutable lastSessionUuid = ""

    let disposeActiveSocket () =
        lock socketLock (fun () ->
            match activeSocket with
            | Some socket ->
                activeSocket <- None

                try
                    socket.Dispose()
                with _ ->
                    ()
            | None -> ())

    let handleMessage (msg: NetMQMessage) =
        rawFramesReceived <- rawFramesReceived + 1

        if msg.FrameCount >= 2 then
            let topic = msg.[0].ConvertToString()
            let metaJson = msg.[1].ConvertToString()

            let data =
                if msg.FrameCount >= 3 then
                    msg.[2].ToByteArray()
                else
                    Array.empty

            try
                let meta = PacketMeta.parseString metaJson
                let evt = PacketMeta.toEvent topic meta data
                lastSessionUuid <- evt.SessionUuid
                channel.Writer.TryWrite(evt) |> ignore
                packetsReceived <- packetsReceived + 1
            with ex ->
                parseErrors <- parseErrors + 1
                lastError <- ex.Message
        else
            lastError <- $"Unexpected frame count: {msg.FrameCount}"

    let runSocket () =
        use socket = new SubscriberSocket()
        lock socketLock (fun () -> activeSocket <- Some socket)

        socket.Options.ReceiveHighWatermark <- 5000
        socket.Subscribe("") |> ignore
        socket.Connect(subEndpoint)

        // ZMQ slow-joiner: allow PUB to be ready before first recv.
        Thread.Sleep(500)
        connected <- true

        try
            while not cts.IsCancellationRequested && reconnectSignal.WaitOne(0) = false do
                try
                    let msg = socket.ReceiveMultipartMessage()
                    handleMessage msg
                with
                | :? TerminatingException -> ()
                | :? ObjectDisposedException -> ()
                | _ when cts.IsCancellationRequested -> ()
                | ex ->
                    lastError <- ex.Message
        finally
            connected <- false
            lock socketLock (fun () ->
                if activeSocket = Some socket then
                    activeSocket <- None)

    let worker =
        Thread(
            fun () ->
                try
                    while not cts.IsCancellationRequested do
                        runSocket()

                        if cts.IsCancellationRequested then
                            ()
                        else
                            reconnects <- reconnects + 1
                            reconnectSignal.Reset() |> ignore
                            Thread.Sleep(250)
                finally
                    channel.Writer.TryComplete() |> ignore
        )

    do
        worker.IsBackground <- true
        worker.Start()

    member _.Packets = channel.Reader

    member _.Diagnostics =
        { Connected = connected
          PacketsReceived = packetsReceived
          RawFramesReceived = rawFramesReceived
          ParseErrors = parseErrors
          Reconnects = reconnects
          LastError = lastError
          LastSessionUuid = lastSessionUuid }

    member _.Reconnect() =
        reconnectSignal.Set() |> ignore
        disposeActiveSocket()

    member _.Stop() =
        reconnectSignal.Set() |> ignore
        cts.Cancel()
        disposeActiveSocket()
        channel.Writer.TryComplete() |> ignore

        if worker.IsAlive then
            worker.Join(TimeSpan.FromSeconds(2)) |> ignore

        cts.Dispose()
        reconnectSignal.Dispose()
