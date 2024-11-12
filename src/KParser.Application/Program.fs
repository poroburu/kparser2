module KParser.Application.Program

open KParser.Infrastructure

[<EntryPoint>]
let main argv =
    let logger = new RawPacketLogger()

    // Default FFXI packet forwarder endpoint
    let endpoint = "tcp://127.0.0.1:5555"

    printfn "Starting packet logger..."
    logger.Start(endpoint)

    // Keep the program running until user input
    printfn "Press any key to exit..."
    System.Console.ReadKey() |> ignore

    0