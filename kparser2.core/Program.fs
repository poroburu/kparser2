module kparser2.UI.Program



open Serilog
open Serilog.Extensions.Logging
open Elmish
open Elmish.WPF
open kparser2.Parser.Network

type Model =
    { Packet: PacketFrame //seq<byte array>
      PacketLog: seq<seq<byte array>> }

type Msg =
    | Reset
    | SocketMsg of PacketFrame//seq<byte array>

let subscriptions (model: Model) : Sub<Msg> =
    let socketSubscription: Subscribe<Msg> =
        fun dispatch ->
            let socketManager = SocketManager()
            // Subscribe to the MessageReceived event
            socketManager.MessageReceived.Add(fun bytes -> dispatch <| SocketMsg bytes)
            socketManager.Start()

    // Start the async loop to receive packets
    [ [ nameof socketSubscription ], socketSubscription ]

let initPacket =
    
    {
        id= 0.0 
        size= 0.0 
        data= ""

    }

let init =
    { Packet = initPacket
      PacketLog = [| [||] |] }

let canReset = (<>) init

let update msg m =
    match msg with

    | Reset -> init
    | SocketMsg bytes ->
        // Handle the socket message here
        // For example, you could update ethe model with the new data

        { m with
            Packet = bytes
            //PacketLog = m.PacketLog |> Seq.append (seq { bytes })
            }

let bindings () : Binding<Model, Msg> list =
    [ "Reset" |> Binding.cmdIf (Reset, canReset) ]

let designVm = ViewModel.designInstance init (bindings ())

let main window =

    let logger =
        LoggerConfiguration()
            .MinimumLevel.Override("Elmish.WPF.Update", Events.LogEventLevel.Verbose)
            .MinimumLevel.Override("Elmish.WPF.Bindings", Events.LogEventLevel.Verbose)
            .MinimumLevel.Override("Elmish.WPF.Performance", Events.LogEventLevel.Verbose)
            .WriteTo.Console()
            .CreateLogger()

    WpfProgram.mkSimple (fun () -> init) update bindings
    |> WpfProgram.withLogger (new SerilogLoggerFactory(logger))
    |> WpfProgram.withSubscription subscriptions
    |> WpfProgram.startElmishLoop window
