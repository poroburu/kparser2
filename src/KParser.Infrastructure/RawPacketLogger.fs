namespace KParser.Infrastructure

open System
open NetMQ
open NetMQ.Sockets
open System.IO

type RawPacketLogger() =
    let mutable disposed = false
    let subscriber = new SubscriberSocket()
    let logsDir = "logs"
    let logPath = Path.Combine(logsDir, "raw_packets.log")

    member _.LogPacketsAsync() =
        task {
            Directory.CreateDirectory(logsDir) |> ignore
            use writer = new StreamWriter(logPath, true)
            printfn "Starting to listen for packets..."
            while not disposed do
                try
                    printfn "Waiting for next packet..."
                    let! (packet, _) = subscriber.ReceiveFrameBytesAsync()
                    let timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
                    let hexDump = BitConverter.ToString(packet)
                    printfn "Received packet: %s" hexDump
                    do! writer.WriteLineAsync($"{timestamp}: {hexDump}")
                    do! writer.FlushAsync()
                with ex ->
                    eprintfn "Error receiving packet: %s" ex.Message
                    eprintfn "Stack trace: %s" ex.StackTrace
        }

    member this.Start(endpoint: string) =
        printfn "Starting packet logger on %s" endpoint
        subscriber.Connect(endpoint)
        subscriber.Subscribe([||])
        printfn "Subscribed to all messages"

        use runtime = new NetMQRuntime()
        runtime.Run(this.LogPacketsAsync())

    interface IDisposable with
        member _.Dispose() =
            if not disposed then
                disposed <- true
                subscriber.Dispose()