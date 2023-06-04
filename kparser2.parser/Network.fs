module kparser2.Parser.Network

open System
open Microsoft.FSharp.Core
open FsNetMQ
open MessagePack
open MessagePack.FSharp
open MessagePack.Resolvers
open kparser2.Parser.Formatters

type SocketManager() =

    // Define an event that will be triggered whenever a new message is received
    let messageReceived = Event<PacketFrame>() //<seq<byte array>>()

    member _.MessageReceived = messageReceived.Publish

    member _.Start() =
        Actor.create (fun shim ->
            use poller = Poller.create ()

            // Registering for the end message which will cancel the actor
            use emObserver = Poller.registerEndMessage poller shim

            // Creating sockets and adding them to the poller
            use subscription = Socket.sub ()
            Socket.connect subscription "tcp://localhost:6666"
            Socket.subscribe subscription ""

            use observer =
                Poller.addSocket poller subscription
                |> Observable.subscribe (fun sock ->
                    let bytes, more = Frame.recv sock

                    let options =
                        MessagePackSerializerOptions.Standard.WithResolver(
                            CompositeResolver.Create(
                                PacketFrameFormatterResolver.Instance,
                                FSharpResolver.Instance,
                                StandardResolver.Instance
                            )
                        )

                    let json = MessagePackSerializer.ConvertToJson(bytes)
                    Console.WriteLine(json)
                    let packetData = MessagePackSerializer.Deserialize<PacketFrame>(bytes, options)

                    let stringToHex (input: string) =
                        let bytes = System.Text.Encoding.ASCII.GetBytes(input) //Unicode.GetBytes(input)
                        let hexBytes = Array.map (fun b -> sprintf "%02X" b) bytes
                        String.Join(" ", hexBytes)

                    let stringToBytes (input: string) =
                        let bytes = Array.zeroCreate<byte> input.Length

                        for i = 0 to input.Length - 1 do
                            bytes.[i] <- byte input.[i]

                        bytes

                    { packetData with
                        data = packetData.data }
                    |> messageReceived.Trigger)

            // Signalling that the actor is ready, this will let the Actor.create function to exit
            Actor.signal shim

            Poller.run poller)
