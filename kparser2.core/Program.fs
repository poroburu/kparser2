module kparser2.Core.Program



open Serilog
open Serilog.Extensions.Logging
open Elmish
open Elmish.WPF
open kparser2.Network.Formatters
open kparser2.Network.Network

type Model =
    { Packet: PacketFrame
      PacketPrint: string
      PacketLog: string list }

type Msg =
    | Reset
    | SocketMsg of PacketFrame

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

    { id = 0; size = 0; data = "" }

let init =
    { Packet = initPacket
      PacketPrint = ""
      PacketLog = List.empty}

let canReset = (<>) init

let update msg m =
    match msg with

    | Reset -> init
    | SocketMsg bytes ->
        // Handle the socket message here
        // For example, you could update ethe model with the new data
        let packetString = Utils.hexformatFile (bytes.data, bytes.size)
        { m with
            Packet = bytes
            PacketPrint = packetString
            PacketLog = [packetString] |> List.append m.PacketLog  }

let bindings () : Binding<Model, Msg> list =
    [ "Reset" |> Binding.cmdIf (Reset, canReset)
      "PacketPrint" |> Binding.oneWay (fun m -> m.PacketPrint)
      "PacketLog" |> Binding.oneWay (fun m -> m.PacketLog)
       ]

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
