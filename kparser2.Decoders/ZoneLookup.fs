namespace kparser2.Decoders

open System
open System.Collections.Generic
open System.IO
open System.Text.Json

module ZoneLookup =
    let private names = Dictionary<int, string>()

    let private defaultPath =
        Path.Combine(AppContext.BaseDirectory, "data", "zones.json")

    let mutable private loaded = false

    let load (path: string option) =
        if not loaded then
            let filePath = defaultArg path defaultPath

            if File.Exists filePath then
                let json = File.ReadAllText filePath
                use doc = JsonDocument.Parse json

                for prop in doc.RootElement.EnumerateObject() do
                    if fst (Int32.TryParse prop.Name) then
                        let id = Int32.Parse prop.Name
                        names.[id] <- prop.Value.GetString() |> Option.ofObj |> Option.defaultValue $"Zone {id}"

            loaded <- true

    let tryGetName (zoneId: int) =
        load None

        match names.TryGetValue zoneId with
        | true, name -> Some name
        | _ -> None

    let getName (zoneId: int) =
        tryGetName zoneId |> Option.defaultValue $"Zone {zoneId}"
