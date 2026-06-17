namespace kparser2.Analytics

open System
open System.Collections.Generic
open System.IO
open System.Text.Json

module ActionLookup =
    let private actions = Dictionary<int, string>()

    let private tryLoad () =
        let candidates =
            [ Path.Combine(AppContext.BaseDirectory, "data", "actions.json")
              Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "data", "actions.json")) ]

        candidates
        |> List.tryFind File.Exists
        |> Option.iter (fun path ->
            try
                let json = File.ReadAllText path
                use doc = JsonDocument.Parse json

                for prop in doc.RootElement.EnumerateObject() do
                    if Int32.TryParse prop.Name |> fst then
                        actions.[Int32.Parse prop.Name] <-
                            match prop.Value.GetString() with
                            | null -> ""
                            | name -> name
            with _ ->
                ())

    do tryLoad ()

    let tryGetName (actionId: int) =
        match actions.TryGetValue actionId with
        | true, name when not (String.IsNullOrWhiteSpace name) -> Some name
        | _ -> None
