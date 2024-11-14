namespace KParser.Infrastructure

open System
open NetMQ
open NetMQ.Sockets

type RawPacket = {
    Timestamp: DateTimeOffset
    Data: byte[]
}

type RawPacketLogger() =
    let mutable disposed = false
    let subscriber = new SubscriberSocket()
    let defaultEndpoint = "tcp://host.docker.internal:4567"
    let poller = new NetMQPoller()
    let mutable packetHandler = fun (packet: RawPacket) -> ()

    let handleReceive (args: NetMQSocketEventArgs) =
        try
            let frame = args.Socket.ReceiveFrameBytes()
            let packet = {
                Timestamp = DateTimeOffset.UtcNow
                Data = frame
            }
            packetHandler packet
        with ex ->
            eprintfn "Error in receive handler: %s" ex.Message

    member this.Start(?endpoint: string, ?onPacket: RawPacket -> unit) =
        let endpoint = defaultArg endpoint defaultEndpoint
        packetHandler <- defaultArg onPacket (fun _ -> ())

        try
            subscriber.Connect(endpoint)
            subscriber.Subscribe("")
            subscriber.ReceiveReady.Add(handleReceive)
            poller.Add(subscriber)
            poller.Run()
        with ex ->
            eprintfn "Fatal error: %s" ex.Message

    interface IDisposable with
        member _.Dispose() =
            if not disposed then
                disposed <- true
                if poller.IsRunning then
                    poller.Stop()
                poller.Dispose()
                subscriber.Dispose()