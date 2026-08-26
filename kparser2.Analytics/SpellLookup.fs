namespace kparser2.Analytics

open System
open System.Collections.Generic
open System.Globalization
open System.IO
open System.Text.Json

module SpellLookup =
    let private spells = Dictionary<int, string>()
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
                        spells.[Int32.Parse prop.Name] <-
                            match prop.Value.GetString() with
                            | null -> ""
                            | name -> textInfo.ToTitleCase name
            with _ ->
                ())

    do tryLoad ()

    let tryGetName (spellId: int) =
        match spells.TryGetValue spellId with
        | true, name when not (String.IsNullOrWhiteSpace name) -> Some name
        | _ -> None
