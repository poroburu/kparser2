#r "nuget: NetMQ"

open NetMQ
open NetMQ.Sockets
open System
open System.Threading

let testPublisher() =
    printfn "Creating publisher..."
    use publisher = new PublisherSocket()

    printfn "Binding publisher to endpoint..."
    publisher.Bind("tcp://127.0.0.1:5555")  // Change back to Bind

    // Give the subscriber time to connect
    printfn "Waiting for subscriber to connect..."
    Thread.Sleep(2000)

    // Send 5 test packets
    for i in 1..5 do
        let testPacket = [| byte i; 0xFFuy; 0xAAuy |] // Simple test packet
        publisher.SendFrame(testPacket)
        printfn $"Sent test packet {i}: {BitConverter.ToString(testPacket)}"
        Thread.Sleep(1000) // Increased delay between packets

printfn "Starting publisher test..."
testPublisher()
printfn "Publisher test complete. Press any key to exit..."
Console.ReadKey() |> ignore