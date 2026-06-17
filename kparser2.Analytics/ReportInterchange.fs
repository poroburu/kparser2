namespace kparser2.Analytics

open System
open System.IO
open System.Reflection
open System.Text.Json
open System.Text.Json.Serialization
open FSharp.SystemTextJson

module ReportInterchange =
    let SchemaVersion = 1

    let productVersion =
        match Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>() with
        | null -> "unknown"
        | attr -> attr.InformationalVersion

    type ReportMeta =
        { [<JsonPropertyName("schema_version")>] SchemaVersion: int
          [<JsonPropertyName("title")>] Title: string
          [<JsonPropertyName("zone")>] Zone: string
          [<JsonPropertyName("recorded_at")>] RecordedAt: string
          [<JsonPropertyName("kparser2_version")>] Kparser2Version: string }

    type ReportBundle =
        { Meta: ReportMeta
          Combatants: Combatant list
          Fights: Battle list
          Events: Interaction list
          Chat: ChatMessageRecord list
          Loot: LootRecord list
          ItemUses: ItemUseRecord list
          [<JsonPropertyName("experience")>] Experience: ExperienceRecord list option
          Summaries: Map<string, Map<string, int>> }

    let private jsonOptions =
        let opts = JsonSerializerOptions(WriteIndented = true)
        opts.Converters.Add(JsonFSharpConverter())
        opts

    let private summaryMap rows =
        rows |> List.map (fun r -> r.Label, r.Total) |> Map.ofList

    let fromSnapshot (title: string) (snap: AnalyticsSnapshot) =
        let filter = MobFilter.defaultFilter

        let offense = AnalyticsQueries.offenseSummary snap filter |> summaryMap
        let defense = AnalyticsQueries.defenseSummary snap filter |> summaryMap
        let recovery = AnalyticsQueries.recovery snap filter |> summaryMap
        let chatByMode = AnalyticsQueries.chatSummary snap None None |> List.groupBy (fun r -> r.Value) |> List.map (fun (mode, rows) -> mode, rows |> List.sumBy (fun r -> r.Count)) |> Map.ofList
        let chatBySpeaker = AnalyticsQueries.chatSummary snap None None |> summaryMap
        let experience = AnalyticsQueries.experience snap |> summaryMap
        let loot = AnalyticsQueries.lootSummary snap |> summaryMap

        { Meta =
            { SchemaVersion = SchemaVersion
              Title = title
              Zone = snap.ZoneName
              RecordedAt = DateTimeOffset.UtcNow.ToString("O")
              Kparser2Version = productVersion }
          Combatants = snap.Combatants
          Fights = snap.Battles
          Events = snap.Interactions
          Chat = snap.ChatMessages
          Loot = snap.LootRecords
          ItemUses = snap.ItemUses
          Experience = Some snap.ExperienceRecords
          Summaries =
              Map
                  [ "offense_by_category", offense
                    "defense_by_category", defense
                    "recovery_by_action", recovery
                    "chat_by_mode", chatByMode
                    "chat_by_speaker", chatBySpeaker
                    "experience_by_actor", experience
                    "loot_by_item", loot ] }

    let toSnapshot (bundle: ReportBundle) =
        { SessionStartMs = 0L
          ZoneName = bundle.Meta.Zone
          Combatants = bundle.Combatants
          Battles = bundle.Fights
          Interactions = bundle.Events
          ChatMessages = bundle.Chat
          LootRecords = bundle.Loot
          ItemUses = bundle.ItemUses
          ExperienceRecords = bundle.Experience |> Option.defaultValue [] }

    let exportToFile (path: string) (bundle: ReportBundle) =
        let json = JsonSerializer.Serialize(bundle, jsonOptions)
        File.WriteAllText(path, json)

    let importFromFile (path: string) =
        let json = File.ReadAllText path
        let bundle = JsonSerializer.Deserialize<ReportBundle>(json, jsonOptions)

        if bundle.Meta.SchemaVersion <> SchemaVersion then
            failwith $"Unsupported schema version {bundle.Meta.SchemaVersion}"

        bundle

    let validateRoundTrip (snap: AnalyticsSnapshot) =
        let bundle = fromSnapshot "roundtrip" snap
        let restored = toSnapshot bundle
        snap.Interactions.Length = restored.Interactions.Length
        && snap.Battles.Length = restored.Battles.Length
        && snap.ExperienceRecords.Length = restored.ExperienceRecords.Length
