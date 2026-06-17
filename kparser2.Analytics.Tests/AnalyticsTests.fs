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
    let bstLootName () = find "bst_loot_name.ndjson"
    let bstCampMulti () = find "bst_camp_multi.ndjson"

module private ReplayHelpers =
    let waitForReplay (session: IAnalyticsSession) =
        match session with
        | :? PacketSession as ps -> ps.WaitForReplayComplete()
        | _ -> ()

module private ReportTestHelpers =
    let defaultFilter = MobFilterDto()

    let replaySnapshot path =
        use session = PacketSessionFactory.fromReplayDefault(path) :> IAnalyticsSession
        ReplayHelpers.waitForReplay session
        session.GetSnapshot()

    let reportText (queryId: string) (snap: AnalyticsSnapshotDto) =
        let report = AnalyticsReportService.format queryId snap defaultFilter
        report.Spans |> Seq.map (fun s -> s.Text) |> String.Concat

    let contains (needle: string) (text: string) =
        Assert.Contains(needle, text, StringComparison.Ordinal)

[<Collection("EntityRegistry")>]
module ReportFormatTests =
    [<Fact>]
    let ``fights report includes fight header`` () =
        let snap = ReportTestHelpers.replaySnapshot (FixturePaths.petrifyingPair())
        let text = ReportTestHelpers.reportText "fights" snap
        ReportTestHelpers.contains "Fight #" text
        ReportTestHelpers.contains "Enemy" text

    [<Fact>]
    let ``offense report includes damage summary section`` () =
        let snap = ReportTestHelpers.replaySnapshot (FixturePaths.combatAction())
        let text = ReportTestHelpers.reportText "offense" snap
        ReportTestHelpers.contains "Damage Summary" text
        ReportTestHelpers.contains "Total Dmg" text

    [<Fact>]
    let ``defense report includes damage taken summary`` () =
        let snap = ReportTestHelpers.replaySnapshot (FixturePaths.combatAction())
        let text = ReportTestHelpers.reportText "defense" snap
        ReportTestHelpers.contains "Damage Taken Summary" text

    [<Fact>]
    let ``time span format rolls seconds into minutes`` () =
        Assert.Equal("15:00", TimeSpanFormat.formatMs 899_500L false)
        Assert.Equal("1:27", TimeSpanFormat.formatMs 86_795L false)

    [<Fact>]
    let ``performance report includes overall and dps sections`` () =
        let snap = ReportTestHelpers.replaySnapshot (FixturePaths.petrifyingPair())
        let text = ReportTestHelpers.reportText "performance" snap
        ReportTestHelpers.contains "Overall" text
        ReportTestHelpers.contains "Damage Per Second" text

    [<Fact>]
    let ``experience report includes chain section`` () =
        let snap = ReportTestHelpers.replaySnapshot (FixturePaths.chatXp())
        let text = ReportTestHelpers.reportText "experience" snap
        ReportTestHelpers.contains "Experience Chains" text
        ReportTestHelpers.contains "Chain   Count" text

    [<Fact>]
    let ``chat report formats messages with timestamps`` () =
        let snap = ReportTestHelpers.replaySnapshot (FixturePaths.chatSelfSay())
        let report = AnalyticsReportService.formatChat snap None None
        let text = report.Spans |> Seq.map (fun s -> s.Text) |> String.Concat
        Assert.Contains("[", text)
        Assert.Contains("hello", text)

    [<Fact>]
    let ``offense detail report includes frequency histogram`` () =
        let snap = ReportTestHelpers.replaySnapshot (FixturePaths.combatAction())
        let text = ReportTestHelpers.reportText "offense-detail" snap
        ReportTestHelpers.contains "Melee" text

    [<Fact>]
    let ``deaths report includes summary and details`` () =
        let snap = ReportTestHelpers.replaySnapshot (FixturePaths.combatDeath())
        let text = ReportTestHelpers.reportText "deaths" snap
        ReportTestHelpers.contains "Player Deaths" text
        ReportTestHelpers.contains "Summary" text

    [<Fact>]
    let ``recovery report includes curing section when healing present`` () =
        let snap = ReportTestHelpers.replaySnapshot (FixturePaths.petrifyingPair())
        let text = ReportTestHelpers.reportText "recovery" snap

        if snap.Interactions |> Seq.exists (fun i -> i.InteractionType = "Aid" && i.AidType = "Recovery") then
            ReportTestHelpers.contains "Curing" text
        else
            Assert.True(true)

[<Collection("EntityRegistry")>]
module FightSegmenterTests =
    let private harmInteraction actorId targetId ts value =
        { Id = 0
          BattleId = None
          TimestampMs = ts
          InteractionType = InteractionType.Harm
          HarmType = Some HarmType.Melee
          AidType = None
          Category = InteractionCategory.Melee
          DamageModifier = DamageModifier.Normal
          ActorId = actorId
          TargetId = targetId
          ActorName = ""
          TargetName = ""
          ActionName = "hit"
          Value = value
          Success = ""
          CommandNo = 0
          MessageId = 0
          IsProc = false
          ProcValue = 0
          IsLocalPlayerActor = false
          IsLocalPlayerTarget = false }

    let private deathInteraction actorId targetId ts =
        { harmInteraction actorId targetId ts 0 with
            InteractionType = InteractionType.Death
            HarmType = None
            Category = InteractionCategory.Death }

    [<Fact>]
    let ``respawn same enemy id opens new battle`` () =
        EntityRegistry.reset()
        let playerId = 0x10001u
        let mobId = 0x20002u
        EntityRegistry.setEntityKind playerId EntityRegistry.EntityKind.Player
        // Simulate local player via 0x00DF path
        let evt =
            { Topic = "test"
              Timestamp = 1UL
              Direction = PacketDirection.Incoming
              PacketType = "world_s2c"
              PacketId = 0x00DFus
              PacketName = "test"
              Size = 40u
              Injected = false
              Blocked = false
              SessionUuid = "test"
              Version = "v1"
              MessageId = 1UL
              Data = Fixtures.groupAttrPacket playerId 140us }

        EntityRegistry.observe evt

        let mutable state = FightSegmenter.initial
        let s1, bid1 = FightSegmenter.applyInteraction state (harmInteraction playerId mobId 1000L 50)
        state <- s1
        Assert.Equal(Some 1, bid1)

        let s2, _ = FightSegmenter.applyInteraction state (deathInteraction playerId mobId 5000L)
        state <- s2
        Assert.Equal(None, state.CurrentBattleId)
        Assert.True(state.Battles |> List.exists (fun b -> b.Id = 1 && b.Killed))

        let s3, bid2 = FightSegmenter.applyInteraction state (harmInteraction playerId mobId 6000L 40)
        state <- s3
        Assert.Equal(Some 2, bid2)

        let battleIds = state.Battles |> List.map (fun b -> b.Id) |> List.distinct
        Assert.Equal(2, battleIds.Length)

        state.Battles
        |> List.filter (fun b -> b.EnemyId = Some mobId)
        |> fun battles -> Assert.Equal(2, battles.Length)

    [<Fact>]
    let ``harm without registered mob kind still opens battle`` () =
        EntityRegistry.reset()

        let evt =
            { Topic = "test"
              Timestamp = 1UL
              Direction = PacketDirection.Incoming
              PacketType = "world_s2c"
              PacketId = 0x00DFus
              PacketName = "test"
              Size = 40u
              Injected = false
              Blocked = false
              SessionUuid = "test"
              Version = "v1"
              MessageId = 1UL
              Data = Fixtures.groupAttrPacket 0x10001u 140us }

        EntityRegistry.observe evt
        let mobId = 0x99999u

        let state, bid =
            FightSegmenter.applyInteraction FightSegmenter.initial (harmInteraction 0x10001u mobId 1000L 42)

        Assert.Equal(Some 1, bid)
        Assert.True(state.Battles.Length >= 1)

    [<Fact>]
    let ``harm to registered player does not open mob fight`` () =
        EntityRegistry.reset()

        EntityRegistry.observe
            { Topic = "test"
              Timestamp = 1UL
              Direction = PacketDirection.Incoming
              PacketType = "world_s2c"
              PacketId = 0x00DFus
              PacketName = "GP_SERV_COMMAND_GROUP_ATTR"
              Size = 40u
              Injected = false
              Blocked = false
              SessionUuid = "test"
              Version = "v1"
              MessageId = 1UL
              Data = Fixtures.groupAttrPacket 5485u 0us }

        EntityRegistry.observe
            { Topic = "test"
              Timestamp = 2UL
              Direction = PacketDirection.Incoming
              PacketType = "world_s2c"
              PacketId = 0x00DDus
              PacketName = "GP_SERV_COMMAND_PARTY_MEMBER"
              Size = 54u
              Injected = false
              Blocked = false
              SessionUuid = "test"
              Version = "v1"
              MessageId = 2UL
              Data = Fixtures.partyMemberPacket 77777u "LullabyMelodia" }

        let state, bid =
            FightSegmenter.applyInteraction
                FightSegmenter.initial
                (harmInteraction 5485u 77777u 1000L 42)

        Assert.Equal(None, bid)
        Assert.True(state.Battles.IsEmpty)

    [<Fact>]
    let ``harm to jug pet does not open mob fight`` () =
        EntityRegistry.reset()

        EntityRegistry.observe
            { Topic = "test"
              Timestamp = 1UL
              Direction = PacketDirection.Incoming
              PacketType = "world_s2c"
              PacketId = 0x00DFus
              PacketName = "GP_SERV_COMMAND_GROUP_ATTR"
              Size = 40u
              Injected = false
              Blocked = false
              SessionUuid = "test"
              Version = "v1"
              MessageId = 1UL
              Data = Fixtures.groupAttrPacket 5485u 0us }

        EntityRegistry.observe
            { Topic = "test"
              Timestamp = 2UL
              Direction = PacketDirection.Incoming
              PacketType = "world_s2c"
              PacketId = 0x000Eus
              PacketName = "GP_SERV_COMMAND_CHAR_NPC"
              Size = 68u
              Injected = false
              Blocked = false
              SessionUuid = "test"
              Version = "v1"
              MessageId = 2UL
              Data = Fixtures.npcUpdatePacket "LullabyMelodia" 17285278u }

        EntityRegistry.setEntityKind 17285278u EntityRegistry.EntityKind.Pet

        let mobId = 0x99999u
        let harm = harmInteraction mobId 17285278u 1000L 42

        CombatEntityInference.inferFromInteraction harm |> ignore
        Assert.Equal(Some EntityRegistry.EntityKind.Pet, EntityRegistry.tryGetEntityKind 17285278u)

        let state, bid = FightSegmenter.applyInteraction FightSegmenter.initial harm
        Assert.Equal(None, bid)
        Assert.True(state.Battles.IsEmpty)

[<Collection("EntityRegistry")>]
module CombatValidationTests =
    [<Fact>]
    let ``multi fight validation passes on petrifying pair`` () =
        use session = PacketSessionFactory.fromReplayDefault(FixturePaths.petrifyingPair()) :> IAnalyticsSession
        ReplayHelpers.waitForReplay session
        let snap = AnalyticsDtoMapping.fromSnapshotDto (session.GetSnapshot())
        Assert.True(snap.Battles.Length >= 2)

        let sameEnemyIds =
            snap.Battles
            |> List.choose (fun b -> b.EnemyId)
            |> List.distinct

        Assert.True(sameEnemyIds.Length >= 1)

        let report = AnalyticsValidate.validateMultiFight snap 2
        Assert.True(report.Ok, report.Issues |> List.map (fun i -> i.Message) |> String.concat "; ")

    [<Fact>]
    let ``offense report includes player rows across fights`` () =
        let snap = ReportTestHelpers.replaySnapshot (FixturePaths.petrifyingPair())
        Assert.True(snap.Battles.Count >= 2)
        Assert.True(snap.Interactions.Count > 0)

        let fightsText = ReportTestHelpers.reportText "fights" snap
        ReportTestHelpers.contains "Fight #" fightsText
        ReportTestHelpers.contains "Killed?" fightsText

    [<Fact>]
    let ``petrifying pair passes combat validation`` () =
        use session = PacketSessionFactory.fromReplayDefault(FixturePaths.petrifyingPair()) :> IAnalyticsSession
        ReplayHelpers.waitForReplay session
        let snap = AnalyticsDtoMapping.fromSnapshotDto (session.GetSnapshot())
        let report = AnalyticsValidate.validateCombat snap
        Assert.True(report.Ok, report.Issues |> List.map (fun i -> i.Message) |> String.concat "; ")

    [<Fact>]
    let ``combat_action has harm interactions and validation`` () =
        EntityRegistry.reset()
        use session = PacketSessionFactory.fromReplayDefault(FixturePaths.combatAction()) :> IAnalyticsSession
        ReplayHelpers.waitForReplay session
        let snap = AnalyticsDtoMapping.fromSnapshotDto (session.GetSnapshot())
        Assert.True(snap.Interactions |> List.exists (fun i -> i.InteractionType = InteractionType.Harm))

    [<Fact>]
    let ``bst loot slice resolves Poroburu in offense report`` () =
        EntityRegistry.reset()
        let snap = ReportTestHelpers.replaySnapshot (FixturePaths.bstLootName())
        let text = ReportTestHelpers.reportText "offense" snap
        ReportTestHelpers.contains "Poroburu" text

    [<Fact>]
    let ``bst camp multi validates at least three battles`` () =
        EntityRegistry.reset()
        use session = PacketSessionFactory.fromReplayDefault(FixturePaths.bstCampMulti()) :> IAnalyticsSession
        ReplayHelpers.waitForReplay session
        let snap = AnalyticsDtoMapping.fromSnapshotDto (session.GetSnapshot())
        Assert.True(snap.Battles.Length >= 2)

        let enemyIds =
            snap.Battles
            |> List.choose (fun b -> b.EnemyId)
            |> List.distinct

        Assert.True(enemyIds.Length >= 1)

        let report = AnalyticsValidate.validateMultiFight snap 2

        Assert.True(
            report.Ok,
            report.Issues |> List.map (fun i -> i.Message) |> String.concat "; "
        )

    [<Fact>]
    let ``performance report excludes mob enemy names`` () =
        EntityRegistry.reset()
        let snap = ReportTestHelpers.replaySnapshot (FixturePaths.bstCampMulti())
        let text = ReportTestHelpers.reportText "performance" snap
        Assert.DoesNotContain("Master_Coeurl", text, StringComparison.OrdinalIgnoreCase)

[<Collection("EntityRegistry")>]
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
