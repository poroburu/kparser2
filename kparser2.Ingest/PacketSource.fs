namespace kparser2.Ingest

open System
open System.Threading
open System.Threading.Channels

type LivePacketSource(subEndpoint: string) =
    let subscriber = new ZmqSubscriber(subEndpoint)

    interface IPacketSource with
        member _.Packets = subscriber.Packets
        member _.WaitForCompletion() = ()

        member _.Dispose() =
            subscriber.Stop()

    member _.Diagnostics = subscriber.Diagnostics

    member _.Reconnect() = subscriber.Reconnect()

type ReplayPacketSource(path: string, ?speed: float) =
    let replayer = new NdjsonReplayer(path, ?speed = speed)

    interface IPacketSource with
        member _.Packets = replayer.Packets
        member _.WaitForCompletion() = replayer.WaitForCompletion()

        member _.Dispose() =
            (replayer :> IDisposable).Dispose()

    member _.WaitForCompletion() = replayer.WaitForCompletion()
