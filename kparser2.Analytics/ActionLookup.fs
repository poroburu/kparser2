namespace kparser2.Analytics

open System
open System.Collections.Generic
open System.IO
open System.Text.Json

module ActionLookup =
    // Verified against paired BST ChatLines and server/sql/{weapon_skills,mob_skills}.sql.
    // These IDs belong to separate namespaces from job abilities in actions.json.
    let tryWeaponSkillName id =
        match id with | 69 -> Some "Rampage" | _ -> None

    let tryMonsterSkillName id =
        match id with
        | 260 | 3857 -> Some "Lamb Chop"
        | 340 -> Some "Rhino Attack"
        | 341 -> Some "Rhino Guard"
        | 792 -> Some "Sandstorm"
        | 795 -> Some "Sand Trap"
        | _ -> None

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
