namespace kparser2.Ingest

open System
open System.Threading
open System.Threading.Channels
open kparser2.Protocol

type NdjsonReplayer(path: string, ?speed: float) =
    let speed = defaultArg speed 0.0
    let channel = Channel.CreateUnbounded<PacketEvent>()

    let worker =
        Thread(
            fun () ->
                try
                    for topic, metaJson, data in Ndjson.readAll path do
                        let meta = PacketMeta.parseString metaJson
                        let evt = PacketMeta.toEvent topic meta data
                        channel.Writer.TryWrite(evt) |> ignore

                        if speed > 0.0 then
                            Thread.Sleep(int (1000.0 / speed))
                finally
                    channel.Writer.TryComplete() |> ignore
        )

    do
        worker.IsBackground <- true
        worker.Start()

    member _.Packets = channel.Reader

    member _.WaitForCompletion() =
        worker.Join()

    interface IDisposable with
        member _.Dispose() =
            if worker.IsAlive then
                worker.Join(TimeSpan.FromSeconds(1)) |> ignore
