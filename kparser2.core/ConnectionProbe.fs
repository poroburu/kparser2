namespace kparser2.Core

open System
open System.Text.Json
open kparser2.Decoders
open kparser2.Ingest

type PluginHello =
    { version: string
      session_uuid: string
      capabilities: JsonElement }

type PluginStats =
    { packets_published: int64
      packets_filtered: int64
      commands_processed: int64
      pub_send_errors: int64 }

module ConnectionProbe =
    let private jsonOptions = JsonSerializerOptions(PropertyNameCaseInsensitive = true)

    let hello () =
        use client = new CommandClient("tcp://localhost:5556")
        client.Hello()

    let statsRaw () =
        use client = new CommandClient("tcp://localhost:5556")
        client.Stats()

    let tryParseHello (response: string) =
        try
            Some(JsonSerializer.Deserialize<PluginHello>(response, jsonOptions))
        with _ ->
            None

    let tryParseStats (response: string) =
        try
            let doc = JsonDocument.Parse(response)
            let mutable statsElement = Unchecked.defaultof<JsonElement>

            let element =
                if doc.RootElement.TryGetProperty("stats", &statsElement) then
                    statsElement
                else
                    doc.RootElement

            Some(JsonSerializer.Deserialize<PluginStats>(element.GetRawText(), jsonOptions))
        with _ ->
            None

    let helloInfo () =
        hello() |> Option.bind tryParseHello

    let pluginStats () =
        statsRaw() |> Option.bind tryParseStats

    let isPluginReachable () =
        hello().IsSome

    let sessionUuid () =
        helloInfo() |> Option.map (fun h -> h.session_uuid)

    let publishedCount () =
        pluginStats() |> Option.map (fun s -> s.packets_published) |> Option.defaultValue 0L

    let tryPlayerNameFromRaw (response: string) =
        try
            let doc = JsonDocument.Parse(response)
            let mutable prop = Unchecked.defaultof<JsonElement>

            if doc.RootElement.TryGetProperty("player_name", &prop) then
                let name = prop.GetString()

                if String.IsNullOrWhiteSpace name then
                    None
                else
                    Some name
            else
                None
        with _ ->
            None

    let playerName () =
        hello() |> Option.bind tryPlayerNameFromRaw

    let tryBootstrapLocalPlayerName () =
        playerName() |> Option.iter EntityRegistry.registerLocalPlayerName
