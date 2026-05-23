namespace kparser2.Ingest

open System
open System.Text.Json
open NetMQ
open NetMQ.Sockets

type CommandClient(reqEndpoint: string) =
    member _.SendCommand(command: string, parameters: obj) =
        use socket = new RequestSocket()
        socket.Connect(reqEndpoint)

        let payload =
            JsonSerializer.Serialize(
                dict [
                    ("command", box command)
                    ("params", box parameters)
                ]
            )

        try
            socket.SendFrame(payload) |> ignore
            let response = socket.ReceiveFrameString()
            Some response
        with _ ->
            None

    member this.Status() = this.SendCommand("status", {| |})

    member this.Stats() = this.SendCommand("stats", {| |})

    member this.Hello() = this.SendCommand("hello", {| |})

    interface IDisposable with
        member _.Dispose() = ()

type CommandClientFactory() =
    static member Create(?reqEndpoint: string) =
        CommandClient(defaultArg reqEndpoint "tcp://localhost:5556")
