module KParser.Application.Program

open KParser.Infrastructure
open System.Threading
open System

[<EntryPoint>]
let main argv =
    printfn "KParser starting..."
    use logger = new RawPacketLogger()

    // Simple packet handler for testing
    let handlePacket (packet: RawPacket) =
        printfn "Received packet of %d bytes at %O"
            packet.Data.Length
            packet.Timestamp

    // Use 127.0.0.1 to match RawPacketLogger's defaultEndpoint
    let endpoint = "tcp://host.docker.internal:4567"

    printfn "Starting packet logger..."
    logger.Start(endpoint, handlePacket)

    0