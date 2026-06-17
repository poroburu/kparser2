namespace kparser2.Analytics

open System
open System.Collections.Generic
open System.IO
open System.Text.Json

module MobXpLookup =
    let private mobXp = Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)

    let private tryLoad () =
        let candidates =
            [ Path.Combine(AppContext.BaseDirectory, "data", "mob_xp.json")
              Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "data", "mob_xp.json")) ]

        candidates
        |> List.tryFind File.Exists
        |> Option.iter (fun path ->
            try
                let json = File.ReadAllText path
                use doc = JsonDocument.Parse json

                for prop in doc.RootElement.EnumerateObject() do
                    if prop.Value.ValueKind = JsonValueKind.Number then
                        mobXp.[prop.Name] <- prop.Value.GetInt32()
            with _ ->
                ())

    do tryLoad ()

    let tryGetXp (mobName: string) =
        match mobXp.TryGetValue mobName with
        | true, xp -> Some xp
        | _ -> None

    let hasXp (mobName: string) =
        match tryGetXp mobName with
        | Some xp -> xp > 0
        | None -> true
