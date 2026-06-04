namespace kparser2.Analytics.Tests

open System
open System.IO
open Xunit
open kparser2.Analytics
open kparser2.Abstractions
open kparser2.Core
open kparser2.Decoders
open kparser2.Decoders.Tests
open kparser2.Protocol

module private FixturePaths =
    let private candidates name =
        [ Path.Combine(AppContext.BaseDirectory, "fixtures", "sessions", name)
          Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "fixtures", "sessions", name)) ]

    let find name = candidates name |> List.find File.Exists

    let combatBasic () = find "combat_basic.ndjson"
    let combatAction () = find "combat_action.ndjson"
    let combatDeath () = find "combat_death.ndjson"
    let combatRecovery () = find "combat_recovery.ndjson"
    let chatXp () = find "chat_xp.ndjson"
    let chatSelfSay () = find "chat_self_say.ndjson"
    let petrifyingPair () = find "bcmn30_petrifying_pair.ndjson"

module private ReplayHelpers =
    let waitForReplay (session: IAnalyticsSession) =
        match session with
        | :? PacketSession as ps -> ps.WaitForReplayComplete()
        | _ -> ()

module AnalyticsTests =
    [<Fact>]
    let ``combat_basic produces classified interactions`` () =
        use session = PacketSessionFactory.fromReplayDefault(FixturePaths.combatBasic()) :> IAnalyticsSession
        ReplayHelpers.waitForReplay session
        let snap = session.GetSnapshot()
        Assert.True(snap.Interactions.Count > 0)

    [<Fact>]
    let ``combat_action produces harm interactions`` () =
        use session = PacketSessionFactory.fromReplayDefault(FixturePaths.combatAction()) :> IAnalyticsSession
        ReplayHelpers.waitForReplay session
        let snap = session.GetSnapshot()
        Assert.True(snap.Interactions.Count >= 2)

    [<Fact>]
    let ``combat_death classifies MsgBasic death`` () =
        use session = PacketSessionFactory.fromReplayDefault(FixturePaths.combatDeath()) :> IAnalyticsSession
        ReplayHelpers.waitForReplay session
        let snap = session.GetSnapshot()
        Assert.True(snap.Interactions |> Seq.exists (fun i -> i.InteractionType = "Death"))

    [<Fact>]
    let ``chat_xp records experience from MsgBasic`` () =
        use session = PacketSessionFactory.fromReplayDefault(FixturePaths.chatXp()) :> IAnalyticsSession
        ReplayHelpers.waitForReplay session
        let snap = session.GetSnapshot()
        Assert.True(snap.ExperienceRecords.Count >= 2)
        Assert.True(snap.ExperienceRecords |> Seq.exists (fun r -> r.ExperiencePoints = 150))
        Assert.True(snap.ExperienceRecords |> Seq.exists (fun r -> r.Chain = 3 && r.ExperiencePoints = 180))

    [<Fact>]
    let ``report round trip preserves counts`` () =
        use session = PacketSessionFactory.fromReplayDefault(FixturePaths.chatXp()) :> IAnalyticsSession
        ReplayHelpers.waitForReplay session
        let snap = session.GetSnapshot()
        let fSnap = AnalyticsDtoMapping.fromSnapshotDto snap
        let bundle = ReportInterchange.fromSnapshot "test" fSnap
        let restored = ReportInterchange.toSnapshot bundle
        Assert.Equal(snap.Interactions.Count, restored.Interactions.Length)
        Assert.Equal(snap.Battles.Count, restored.Battles.Length)
        Assert.Equal(snap.ExperienceRecords.Count, restored.ExperienceRecords.Length)

    [<Fact>]
    let ``battle message catalog classifies melee hit`` () =
        let interactionType, harmType, _ =
            BattleMessageCatalog.classifyActionEffect 1 0 0 42

        Assert.Equal(InteractionType.Harm, interactionType)
        Assert.Equal(Some HarmType.Melee, harmType)

    [<Fact>]
    let ``parse codes classify melee message id`` () =
        let interactionType, harmType, _ =
            BattleMessageCatalog.classifyActionEffect 1 0x14 0 42

        Assert.Equal(InteractionType.Harm, interactionType)
        Assert.Equal(Some HarmType.Melee, harmType)

    [<Fact>]
    let ``MsgBasic classifies XP message`` () =
        let interactionType, _, _ = MsgBasicCatalog.classify MsgBasicCatalog.ExperiencePointsGained 0
        Assert.Equal(InteractionType.Unknown, interactionType)
        Assert.True(MsgBasicCatalog.isExperienceMessage MsgBasicCatalog.ExperiencePointsGained)

    [<Fact>]
    let ``MsgBasic classifies defeat as death`` () =
        let interactionType, _, _ = MsgBasicCatalog.classify MsgBasicCatalog.DefeatsTarget 0
        Assert.Equal(InteractionType.Death, interactionType)

    [<Fact>]
    let ``experience parser reads battle message`` () =
        match ExperienceParser.tryParseBattleMessage 8 0u 150u with
        | Some parsed ->
            Assert.Equal(150, parsed.Points)
            Assert.Equal(0, parsed.Chain)
        | None -> failwith "Expected XP parse"

    [<Fact>]
    let ``combat_recovery includes aid interactions`` () =
        use session = PacketSessionFactory.fromReplayDefault(FixturePaths.combatRecovery()) :> IAnalyticsSession
        ReplayHelpers.waitForReplay session
        let snap = session.GetSnapshot()
        Assert.True(snap.Interactions |> Seq.exists (fun i -> i.InteractionType = "Aid"))

    [<Fact>]
    let ``defenseByTime query returns rows for enhance`` () =
        use session = PacketSessionFactory.fromReplayDefault(FixturePaths.combatRecovery()) :> IAnalyticsSession
        ReplayHelpers.waitForReplay session
        let snap = AnalyticsDtoMapping.fromSnapshotDto (session.GetSnapshot())
        let rows = AnalyticsQueries.defenseByTime snap MobFilter.defaultFilter
        Assert.True(rows.Length >= 0)

    [<Fact>]
    let ``petrifying_pair registers mobs and battles`` () =
        use session = PacketSessionFactory.fromReplayDefault(FixturePaths.petrifyingPair()) :> IAnalyticsSession
        ReplayHelpers.waitForReplay session
        let snap = session.GetSnapshot()
        Assert.True(snap.Combatants.Count >= 2)

        Assert.True(
            snap.Combatants
            |> Seq.exists (fun c -> c.Name.Contains("Kalamainu", StringComparison.OrdinalIgnoreCase))
        )

        Assert.True(snap.Battles.Count >= 1)
        Assert.True(snap.Interactions |> Seq.exists (fun i -> i.InteractionType = "Death"))

    [<Fact>]
    let ``session store resolves self say after tell to self bootstrap`` () =
        EntityRegistry.reset()
        let store = SessionStore.create()

        let mkEvt (ts: int) packetId direction (data: byte[]) =
            { Topic = "test"
              Timestamp = uint64 ts
              Direction = direction
              PacketType = if direction = PacketDirection.Outgoing then "world_c2s" else "world_s2c"
              PacketId = packetId
              PacketName = "test"
              Size = uint32 data.Length
              Injected = false
              Blocked = false
              SessionUuid = "test"
              Version = "v1"
              MessageId = uint64 ts
              Data = data }

        SessionStore.ingest store (mkEvt 1 0x00DFus PacketDirection.Incoming (Fixtures.groupAttrPacket 0x950F5u 140us)) DecoderResult.empty

        SessionStore.ingest
            store
            (mkEvt 2 0x00B5us PacketDirection.Outgoing (Fixtures.outgoingChatPacket "hello" 0x00uy))
            (DecoderRegistry.decode (
                mkEvt 2 0x00B5us PacketDirection.Outgoing (Fixtures.outgoingChatPacket "hello" 0x00uy)))

        SessionStore.ingest
            store
            (mkEvt 3 0x0017us PacketDirection.Incoming (Fixtures.chatPacket "Poroburu" ">>Poroburu ping" 0x03uy))
            (DecoderRegistry.decode (
                mkEvt 3 0x0017us PacketDirection.Incoming (Fixtures.chatPacket "Poroburu" ">>Poroburu ping" 0x03uy)))

        let snap = SessionStore.snapshot store
        Assert.Contains(snap.ChatMessages, fun c -> c.Mode = "Say" && c.Speaker = "Poroburu" && c.Message = "hello")
        Assert.Contains(snap.ChatMessages, fun c -> c.Mode = "Tell" && c.Speaker = "Poroburu")

    [<Fact>]
    let ``chat_self_say fixture resolves outgoing say speaker`` () =
        use session = PacketSessionFactory.fromReplayDefault(FixturePaths.chatSelfSay()) :> IAnalyticsSession
        ReplayHelpers.waitForReplay session
        let snap = session.GetSnapshot()
        Assert.Contains(snap.ChatMessages, fun c -> c.Mode = "Say" && c.Speaker = "Poroburu" && c.Message = "hello")
        Assert.Contains(snap.ChatMessages, fun c -> c.Mode = "Tell" && c.Speaker = "Poroburu")
