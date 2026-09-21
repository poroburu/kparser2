namespace kparser2.Analytics

open System
open System.Collections.Generic
open System.Globalization
open System.IO
open System.Text.Json

module SpellLookup =
    let private spells = Dictionary<int, string>()
    let private mpCosts = Dictionary<int, int>()
    let private textInfo = CultureInfo.InvariantCulture.TextInfo

    let private tryLoad () =
        let candidates =
            [ Path.Combine(AppContext.BaseDirectory, "data", "spells.json")
              Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "data", "spells.json")) ]

        candidates
        |> List.tryFind File.Exists
        |> Option.iter (fun path ->
            try
                let json = File.ReadAllText path
                use doc = JsonDocument.Parse json

                for prop in doc.RootElement.EnumerateObject() do
                    if Int32.TryParse prop.Name |> fst then
                        let id = Int32.Parse prop.Name
                        let value = prop.Value
                        let name =
                            if value.ValueKind = JsonValueKind.String then value.GetString()
                            else value.GetProperty("name").GetString()
                        spells.[id] <-
                            match name with
                            | null -> ""
                            | name -> textInfo.ToTitleCase name
                        if value.ValueKind = JsonValueKind.Object then
                            match value.TryGetProperty("mpCost") with
                            | true, cost when cost.ValueKind = JsonValueKind.Number ->
                                match cost.TryGetInt32() with
                                | true, n when n > 0 -> mpCosts.[id] <- n
                                | _ -> ()
                            | _ -> ()
            with _ ->
                ())

    do tryLoad ()

    let tryGetName (spellId: int) =
        match spells.TryGetValue spellId with
        | true, name when not (String.IsNullOrWhiteSpace name) -> Some name
        | _ -> None

    let tryGetMpCost (spellId: int) =
        match mpCosts.TryGetValue spellId with
        | true, cost -> Some cost
        | _ -> None
