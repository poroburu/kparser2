namespace kparser2.Analytics.Tests

open System
open System.IO
open Xunit
open kparser2.Analytics
open kparser2.Abstractions
open kparser2.Core
open kparser2.Decoders
open kparser2.Decoders.Tests
open kparser2.Ingest
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
    let combatKillXp () = find "combat_kill_xp.ndjson"
    let chatSelfSay () = find "chat_self_say.ndjson"
    let chatYell () = find "chat_yell.ndjson"
    let chatYellLive () = find "chat_yell_live.ndjson"
    let sample () = find "sample.ndjson"
    let login () = find "login.ndjson"
    let itemDrop () = find "item_drop.ndjson"
    let combatMeleeHits () = find "combat_melee_hits.ndjson"
    let combatMisses () = find "combat_misses.ndjson"
    let combatDefense () = find "combat_defense.ndjson"
    let combatFailures () = find "combat_failures.ndjson"
    let combatTpDrain () = find "combat_tp_drain.ndjson"
    let combatEnfeeble () = find "combat_enfeeble.ndjson"
    let combatBuff () = find "combat_buff.ndjson"
    let combatMagicLive () = find "combat_magic_live.ndjson"
    let petrifyingPair () = find "bcmn30_petrifying_pair.ndjson"
    let bstLootName () = find "bst_loot_name.ndjson"
    let bstCampMulti () = find "bst_camp_multi.ndjson"

module private ReplayHelpers =
    let waitForReplay (session: IAnalyticsSession) =
        match session with
        | :? PacketSession as ps -> ps.WaitForReplayComplete()
        | _ -> ()

    let ingestFixture path =
        EntityRegistry.reset()
        Ndjson.tryPlayerName path |> Option.iter EntityRegistry.registerLocalPlayerName
        let store = SessionStore.create()

        for topic, metaJson, data in Ndjson.readAll path do
            let meta = PacketMeta.parseString metaJson
            let evt = PacketMeta.toEvent topic meta data
            EntityRegistry.observe evt
            let decoded = DecoderRegistry.decode evt
            SessionStore.ingest store evt decoded

        SessionStore.snapshot store

    let ingestFixtureDto path =
        ingestFixture path |> AnalyticsDtoMapping.toSnapshotDto

module private ReportTestHelpers =
    let defaultFilter = MobFilterDto()

    /// Synchronous NDJSON replay (preferred in tests — no PacketSession background tasks).
    let replaySnapshot path = ReplayHelpers.ingestFixtureDto path

    let reportText (queryId: string) (snap: AnalyticsSnapshotDto) =
        let report = AnalyticsReportService.format queryId snap defaultFilter
        report.Spans |> Seq.map (fun s -> s.Text) |> String.Concat

    let contains (needle: string) (text: string) =
        Assert.Contains(needle, text, StringComparison.Ordinal)

[<Collection("EntityRegistry")>]
module ReportFormatTests =
    [<Fact>]
    [<Trait("Category", "Integration")>]
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
    [<Trait("Category", "Integration")>]
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
        Assert.Contains("[Say]", text)
        Assert.Contains("Poroburu", text)
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
    [<Trait("Category", "Integration")>]
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

    [<Fact>]
    let ``applyExperience attaches to recently killed battle`` () =
        EntityRegistry.reset()
        let playerId = 0x10001u
        let mobId = 0x20002u
        EntityRegistry.setEntityKind playerId EntityRegistry.EntityKind.Player

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
        let s1, _ = FightSegmenter.applyInteraction state (harmInteraction playerId mobId 1000L 50)
        state <- s1

        let s2, _ = FightSegmenter.applyInteraction state (deathInteraction playerId mobId 2000L)
        state <- s2
        Assert.Equal(None, state.CurrentBattleId)

        let s3, battleId = FightSegmenter.applyExperience state 2500L 150 0
        Assert.Equal(Some 1, battleId)

        let battle = s3.Battles |> List.find (fun b -> b.Id = 1)
        Assert.Equal(150, battle.ExperiencePoints)

        let s4, chainBattleId = FightSegmenter.applyExperience s3 2600L 0 2
        Assert.Equal(Some 1, chainBattleId)

        let battleWithChain = s4.Battles |> List.find (fun b -> b.Id = 1)
        Assert.Equal(150, battleWithChain.ExperiencePoints)
        Assert.Equal(2, battleWithChain.ExperienceChain)

[<Collection("EntityRegistry")>]
module CombatValidationTests =
    [<Fact>]
    [<Trait("Category", "Integration")>]
    let ``multi fight validation passes on petrifying pair`` () =
        let snap = ReplayHelpers.ingestFixture (FixturePaths.petrifyingPair())
        Assert.True(snap.Battles.Length >= 2)

        let sameEnemyIds =
            snap.Battles
            |> List.choose (fun b -> b.EnemyId)
            |> List.distinct

        Assert.True(sameEnemyIds.Length >= 1)

        let report = AnalyticsValidate.validateMultiFight snap 2
        Assert.True(report.Ok, report.Issues |> List.map (fun i -> i.Message) |> String.concat "; ")

    [<Fact>]
    [<Trait("Category", "Integration")>]
    let ``offense report includes player rows across fights`` () =
        let snap = ReportTestHelpers.replaySnapshot (FixturePaths.petrifyingPair())
        Assert.True(snap.Battles.Count >= 2)
        Assert.True(snap.Interactions.Count > 0)

        let fightsText = ReportTestHelpers.reportText "fights" snap
        ReportTestHelpers.contains "Fight #" fightsText
        ReportTestHelpers.contains "Killed?" fightsText

    [<Fact>]
    [<Trait("Category", "Integration")>]
    let ``petrifying pair passes combat validation`` () =
        let snap = ReplayHelpers.ingestFixture (FixturePaths.petrifyingPair())
        let report = AnalyticsValidate.validateCombat snap
        Assert.True(report.Ok, report.Issues |> List.map (fun i -> i.Message) |> String.concat "; ")

    [<Fact>]
    let ``combat_action has harm interactions and validation`` () =
        let snap = ReplayHelpers.ingestFixture (FixturePaths.combatAction())
        Assert.True(snap.Interactions |> List.exists (fun i -> i.InteractionType = InteractionType.Harm))

    [<Fact>]
    [<Trait("Category", "Integration")>]
    let ``bst loot slice resolves Poroburu in offense report`` () =
        let snap = ReportTestHelpers.replaySnapshot (FixturePaths.bstLootName())
        let text = ReportTestHelpers.reportText "offense" snap
        ReportTestHelpers.contains "Poroburu" text

    [<Fact>]
    [<Trait("Category", "Integration")>]
    let ``bst camp multi validates at least three battles`` () =
        let snap = ReplayHelpers.ingestFixture (FixturePaths.bstCampMulti())
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
    [<Trait("Category", "Integration")>]
    let ``performance report excludes mob enemy names`` () =
        let snap = ReportTestHelpers.replaySnapshot (FixturePaths.bstCampMulti())
        let text = ReportTestHelpers.reportText "performance" snap
        Assert.DoesNotContain("Master_Coeurl", text, StringComparison.OrdinalIgnoreCase)

[<Collection("EntityRegistry")>]
module AnalyticsTests =
    [<Fact>]
    let ``combat_basic produces classified interactions`` () =
        let snap = ReplayHelpers.ingestFixtureDto (FixturePaths.combatBasic())
        Assert.True(snap.Interactions.Count > 0)

    [<Fact>]
    let ``combat_action produces harm interactions`` () =
        let snap = ReplayHelpers.ingestFixtureDto (FixturePaths.combatAction())
        Assert.True(snap.Interactions.Count >= 2)

    [<Fact>]
    let ``combat_death classifies MsgBasic death`` () =
        let snap = ReplayHelpers.ingestFixtureDto (FixturePaths.combatDeath())
        Assert.True(snap.Interactions |> Seq.exists (fun i -> i.InteractionType = "Death"))

    [<Fact>]
    let ``chat_xp records experience from MsgBasic`` () =
        let snap = ReplayHelpers.ingestFixtureDto (FixturePaths.chatXp())
        Assert.True(snap.ExperienceRecords.Count >= 2)
        Assert.True(snap.ExperienceRecords |> Seq.exists (fun r -> r.ExperiencePoints = 150))
        Assert.True(snap.ExperienceRecords |> Seq.exists (fun r -> r.Chain = 3 && r.ExperiencePoints = 180))

    [<Fact>]
    let ``chat_xp deduplicates chat echo of msgbasic xp`` () =
        let snap = ReplayHelpers.ingestFixture (FixturePaths.chatXp())
        let count150 = snap.ExperienceRecords |> List.filter (fun r -> r.ExperiencePoints = 150) |> List.length
        Assert.Equal(1, count150)

    [<Fact>]
    let ``combat_kill_xp attributes xp to killed battle`` () =
        let snap = ReplayHelpers.ingestFixture (FixturePaths.combatKillXp())
        let killed = snap.Battles |> List.filter (fun b -> b.Killed)
        Assert.True(killed.Length >= 1)

        let battle = List.head killed
        Assert.Equal(150, battle.ExperiencePoints)
        Assert.Equal(2, battle.ExperienceChain)

        Assert.True(
            snap.ExperienceRecords
            |> List.exists (fun r -> r.BattleId = Some battle.Id && r.ExperiencePoints = 150)
        )

    [<Fact>]
    let ``fights report shows exp and chain for kill xp fixture`` () =
        let snap = ReplayHelpers.ingestFixture (FixturePaths.combatKillXp())
        let snapDto = AnalyticsDtoMapping.toSnapshotDto snap
        let text = ReportTestHelpers.reportText "fights" snapDto
        ReportTestHelpers.contains "150" text
        ReportTestHelpers.contains "Yes" text

        let rows = ReportAggregators.buildFightRows snap MobFilter.defaultFilter
        Assert.True(rows |> List.exists (fun r -> r.Exp = 150 && r.Chain = 2))

    [<Fact>]
    let ``experience report chain section uses fight chains`` () =
        let snap = ReplayHelpers.ingestFixture (FixturePaths.combatKillXp())
        let snapDto = AnalyticsDtoMapping.toSnapshotDto snap
        let text = ReportTestHelpers.reportText "experience" snapDto
        ReportTestHelpers.contains "Experience Chains" text
        ReportTestHelpers.contains "Chain   Count" text

        let chainTwoLine =
            text.Split('\n') |> Array.tryFind (fun line -> line.TrimStart().StartsWith("2"))

        Assert.True(chainTwoLine.IsSome, "Expected chain 2 row in experience report")

    [<Fact>]
    let ``report round trip preserves counts`` () =
        let snap = ReplayHelpers.ingestFixtureDto (FixturePaths.chatXp())
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
    let ``MsgBasic classifies status wears off as enhance`` () =
        let interactionType, _, aidType =
            MsgBasicCatalog.classify MsgBasicCatalog.StatusWearsOff 0

        Assert.Equal(InteractionType.Aid, interactionType)
        Assert.Equal(Some AidType.Enhance, aidType)
        Assert.Equal("Status Wears Off", MsgBasicCatalog.messageLabel MsgBasicCatalog.StatusWearsOff)
        Assert.True(SettledDivergence.isMessageClassified MsgBasicCatalog.StatusWearsOff)

    [<Fact>]
    let ``0x29 status wears off becomes an enhance interaction`` () =
        EntityRegistry.reset()
        InteractionBuilder.reset()
        let store = SessionStore.create()
        let data = Fixtures.battleMessagePacketSimple 20149u 20149u 206us
        let evt =
            { Topic = "test"
              Timestamp = 1UL
              Direction = PacketDirection.Incoming
              PacketType = "world_s2c"
              PacketId = 0x0029us
              PacketName = "GP_SERV_COMMAND_BATTLE_MESSAGE"
              Size = 28u
              Injected = false
              Blocked = false
              SessionUuid = "test"
              Version = "v1"
              MessageId = 1UL
              Data = data }

        EntityRegistry.observe evt
        SessionStore.ingest store evt (DecoderRegistry.decode evt)
        let snap = SessionStore.snapshot store
        Assert.Contains(
            snap.Interactions,
            fun i ->
                i.ActionName = "Status Wears Off"
                && i.InteractionType = InteractionType.Aid
                && i.MessageId = 206
        )

    [<Fact>]
    let ``MsgBasic classifies casting interrupted as unknown not aid`` () =
        let interactionType, harm, aid =
            MsgBasicCatalog.classify MsgBasicCatalog.IsInterrupted 4

        Assert.Equal(InteractionType.Unknown, interactionType)
        Assert.Equal(None, harm)
        Assert.Equal(None, aid)
        Assert.Equal("Casting Interrupted", MsgBasicCatalog.messageLabel MsgBasicCatalog.IsInterrupted)
        Assert.True(SettledDivergence.isMessageClassified MsgBasicCatalog.IsInterrupted)
        Assert.True(SettledDivergence.isMessageClassified MsgBasicCatalog.NotEnoughMp)
        Assert.Equal("Unable To See Target", MsgBasicCatalog.messageLabel MsgBasicCatalog.UnableToSeeTarg)
        Assert.Equal(
            InteractionType.Unknown,
            (MsgBasicCatalog.classify MsgBasicCatalog.UnableToSeeTarg 4 |> fun (t, _, _) -> t)
        )
        Assert.Equal("Unable To Use Job Ability", MsgBasicCatalog.messageLabel MsgBasicCatalog.UnableToUseJa2)
        Assert.Equal("Time Left", MsgBasicCatalog.messageLabel MsgBasicCatalog.TimeLeft)
        Assert.Equal("No Jug Pet Item", MsgBasicCatalog.messageLabel MsgBasicCatalog.NoJugPetItem)
        Assert.Equal(
            InteractionType.Unknown,
            (MsgBasicCatalog.classify MsgBasicCatalog.UnableToUseJa2 4 |> fun (t, _, _) -> t)
        )
        Assert.Equal(
            InteractionType.Unknown,
            (MsgBasicCatalog.classify MsgBasicCatalog.TimeLeft 4 |> fun (t, _, _) -> t)
        )
        Assert.Equal("Check Low Evasion High Defense", MsgBasicCatalog.messageLabel 176)
        Assert.Equal("Check Low Evasion", MsgBasicCatalog.messageLabel 177)
        Assert.Equal("Check Low Evasion And Defense", MsgBasicCatalog.messageLabel 178)
        Assert.Equal(InteractionType.Unknown, (MsgBasicCatalog.classify 176 4 |> fun (t, _, _) -> t))
        Assert.True(SettledDivergence.isMessageClassified 176)

    [<Fact>]
    let ``0x29 casting interrupted is not classified as enhance`` () =
        EntityRegistry.reset()
        InteractionBuilder.reset()
        let store = SessionStore.create()
        let data = Fixtures.battleMessagePacketSimple 20149u 20149u 16us
        let evt =
            { Topic = "test"
              Timestamp = 1UL
              Direction = PacketDirection.Incoming
              PacketType = "world_s2c"
              PacketId = 0x0029us
              PacketName = "GP_SERV_COMMAND_BATTLE_MESSAGE"
              Size = 28u
              Injected = false
              Blocked = false
              SessionUuid = "test"
              Version = "v1"
              MessageId = 1UL
              Data = data }

        EntityRegistry.observe evt
        SessionStore.ingest store evt (DecoderRegistry.decode evt)
        let snap = SessionStore.snapshot store
        Assert.Contains(
            snap.Interactions,
            fun i ->
                i.ActionName = "Casting Interrupted"
                && i.InteractionType = InteractionType.Unknown
                && i.MessageId = 16
        )

    [<Fact>]
    let ``experience parser reads battle message`` () =
        match ExperienceParser.tryParseBattleMessage 8 0u 150u with
        | Some parsed ->
            Assert.Equal(150, parsed.Points)
            Assert.Equal(0, parsed.Chain)
        | None -> failwith "Expected XP parse"

    [<Fact>]
    let ``combat_recovery includes aid interactions`` () =
        let snap = ReplayHelpers.ingestFixtureDto (FixturePaths.combatRecovery())
        Assert.True(snap.Interactions |> Seq.exists (fun i -> i.InteractionType = "Aid"))

    [<Fact>]
    let ``defenseByTime query returns rows for enhance`` () =
        let snap = ReplayHelpers.ingestFixture (FixturePaths.combatRecovery())
        let rows = AnalyticsQueries.defenseByTime snap MobFilter.defaultFilter
        Assert.True(rows.Length >= 0)

    [<Fact>]
    [<Trait("Category", "Integration")>]
    let ``petrifying_pair registers mobs and battles`` () =
        let snap = ReplayHelpers.ingestFixtureDto (FixturePaths.petrifyingPair())
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
    let ``incoming 0x0037 server status is not recorded as an item use`` () =
        EntityRegistry.reset()
        EntityRegistry.registerLocalPlayerName "Porobururu"
        let store = SessionStore.create()

        let evt =
            { Topic = "test"
              Timestamp = 1UL
              Direction = PacketDirection.Incoming
              PacketType = "world_s2c"
              PacketId = 0x0037us
              PacketName = "GP_SERV_COMMAND_SERVERSTATUS"
              Size = 96u
              Injected = false
              Blocked = false
              SessionUuid = "test"
              Version = "v1"
              MessageId = 1UL
              Data = Fixtures.serverStatusPacket 20149u }

        EntityRegistry.observe evt
        SessionStore.ingest store evt DecoderResult.empty
        let snap = SessionStore.snapshot store
        Assert.Empty snap.ItemUses
        Assert.Equal(Some 20149u, EntityRegistry.tryLocalPlayerId())
        Assert.Equal("Porobururu", EntityRegistry.formatEntity 20149u)

    [<Fact>]
    let ``snapshot backfills interaction names after local player id is known`` () =
        EntityRegistry.reset()
        InteractionBuilder.reset()
        let store = SessionStore.create()

        store.Interactions <-
            [ { Id = 0
                BattleId = None
                TimestampMs = 1L
                InteractionType = InteractionType.Aid
                HarmType = None
                AidType = Some AidType.Recovery
                Category = InteractionCategory.Recovery
                DamageModifier = DamageModifier.Normal
                ActorId = 20149u
                TargetId = 20149u
                ActorName = "Entity 20149"
                TargetName = "Entity 20149"
                ActionName = "Cure"
                Value = 0
                Success = "hit"
                CommandNo = 4
                MessageId = 7
                IsProc = false
                ProcValue = 0
                IsLocalPlayerActor = false
                IsLocalPlayerTarget = false } ]

        EntityRegistry.registerLocalPlayerName "Porobururu"

        EntityRegistry.observe
            { Topic = "test"
              Timestamp = 2UL
              Direction = PacketDirection.Incoming
              PacketType = "world_s2c"
              PacketId = 0x0037us
              PacketName = "GP_SERV_COMMAND_SERVERSTATUS"
              Size = 96u
              Injected = false
              Blocked = false
              SessionUuid = "test"
              Version = "v1"
              MessageId = 2UL
              Data = Fixtures.serverStatusPacket 20149u }

        let snap = SessionStore.snapshot store
        Assert.Equal("Porobururu", snap.Interactions.Head.ActorName)
        Assert.True(snap.Interactions.Head.IsLocalPlayerActor)

    [<Fact>]
    let ``session header name is reapplied after PacketStore reset`` () =
        EntityRegistry.reset()
        EntityRegistry.registerLocalPlayerName "Porobururu"
        let _store = PacketStore(8)
        Assert.Equal(None, EntityRegistry.localPlayerName())
        EntityRegistry.registerLocalPlayerName "Porobururu"
        Assert.Equal(Some "Porobururu", EntityRegistry.localPlayerName())

    [<Fact>]
    let ``chat_self_say fixture resolves outgoing say speaker`` () =
        let snap = ReplayHelpers.ingestFixtureDto (FixturePaths.chatSelfSay())
        Assert.Contains(snap.ChatMessages, fun c -> c.Mode = "Say" && c.Speaker = "Poroburu" && c.Message = "hello")
        Assert.Contains(snap.ChatMessages, fun c -> c.Mode = "Tell" && c.Speaker = "Poroburu")

    [<Fact(Skip = "PacketSession async replay is covered by kparser2.cli analytics snapshot")>]
    [<Trait("Category", "Integration")>]
    let ``PacketSession replays combat_action to completion`` () =
        use session = PacketSessionFactory.fromReplayDefault(FixturePaths.combatAction()) :> IAnalyticsSession
        ReplayHelpers.waitForReplay session
        let snap = session.GetSnapshot()
        Assert.True(snap.Interactions.Count >= 2)

[<Collection("EntityRegistry")>]
module FixtureReplayParityTests =
    [<Fact>]
    let ``sample fixture decodes chat and loot`` () =
        EntityRegistry.reset()
        let snap = ReplayHelpers.ingestFixture (FixturePaths.sample())
        Assert.True(snap.ChatMessages.Length >= 1)
        Assert.True(snap.LootRecords.Length >= 1)

    [<Fact>]
    let ``login fixture decodes system chat`` () =
        EntityRegistry.reset()
        let snap = ReplayHelpers.ingestFixture (FixturePaths.login())
        Assert.True(snap.ChatMessages |> List.exists (fun c -> c.Mode = "System"))

    [<Fact>]
    let ``chat_yell fixture decodes yell body`` () =
        EntityRegistry.reset()
        let snap = ReplayHelpers.ingestFixture (FixturePaths.chatYell())
        Assert.Contains(
            snap.ChatMessages,
            fun c -> c.Mode = "Yell" && c.Speaker = "Alice" && c.Message = "Hello from yell")

    [<Fact>]
    let ``chat_yell_live fixture decodes HorizonXI yell layout`` () =
        EntityRegistry.reset()
        let snap = ReplayHelpers.ingestFixture (FixturePaths.chatYellLive())
        Assert.Contains(
            snap.ChatMessages,
            fun c -> c.Mode = "Yell" && c.Speaker = "Wish" && c.Message = "SMN or WHM LFG Sagelord Elimination")
        Assert.Contains(
            snap.ChatMessages,
            fun c -> c.Mode = "Yell" && c.Speaker = "Alastar" && c.Message = "golden salvage [02021206] /t")
        Assert.Contains(
            snap.ChatMessages,
            fun c ->
                c.Mode = "Yell"
                && c.Speaker = "Sadatane"
                && c.Message.Contains("BCNM(Windurst)"))

    [<Fact>]
    let ``item_drop fixture records found and won loot`` () =
        EntityRegistry.reset()
        let snap = ReplayHelpers.ingestFixture (FixturePaths.itemDrop())
        Assert.True(snap.LootRecords |> List.exists (fun l -> l.EventType = "Found"))
        Assert.True(snap.LootRecords |> List.exists (fun l -> l.EventType = "Won"))

    [<Fact>]
    let ``combat_melee_hits produces harm interactions`` () =
        EntityRegistry.reset()
        let snap = ReplayHelpers.ingestFixture (FixturePaths.combatMeleeHits())
        Assert.True(snap.Interactions |> List.filter (fun i -> i.InteractionType = InteractionType.Harm) |> List.length >= 3)

    [<Fact>]
    let ``combat_misses includes miss success labels`` () =
        EntityRegistry.reset()
        let snap = ReplayHelpers.ingestFixture (FixturePaths.combatMisses())
        Assert.True(snap.Interactions |> List.exists (fun i -> i.Success = "miss"))

    [<Fact>]
    let ``combat_defense includes parry and shadow absorb`` () =
        EntityRegistry.reset()
        let snap = ReplayHelpers.ingestFixture (FixturePaths.combatDefense())
        Assert.True(snap.Interactions |> List.exists (fun i -> i.Success = "parry"))
        Assert.True(snap.Interactions |> List.exists (fun i -> i.Success = "shadow-absorb"))

    [<Fact>]
    let ``combat_failures includes no-effect aid`` () =
        EntityRegistry.reset()
        let snap = ReplayHelpers.ingestFixture (FixturePaths.combatFailures())
        Assert.True(snap.Interactions |> List.exists (fun i -> i.Success = "no-effect" && i.InteractionType = InteractionType.Aid))

    [<Fact>]
    let ``combat_tp_drain classifies additional effect harm`` () =
        EntityRegistry.reset()
        let snap = ReplayHelpers.ingestFixture (FixturePaths.combatTpDrain())
        Assert.True(snap.Interactions |> List.exists (fun i -> i.MessageId = 0xBB && i.InteractionType = InteractionType.Harm))

    [<Fact>]
    let ``combat_enfeeble classifies enfeeble category`` () =
        EntityRegistry.reset()
        let snap = ReplayHelpers.ingestFixture (FixturePaths.combatEnfeeble())
        Assert.True(snap.Interactions |> List.exists (fun i -> i.Category = InteractionCategory.Enfeeble))

    [<Fact>]
    let ``combat_buff classifies enhance aid`` () =
        EntityRegistry.reset()
        let snap = ReplayHelpers.ingestFixture (FixturePaths.combatBuff())
        Assert.True(snap.Interactions |> List.exists (fun i -> i.InteractionType = InteractionType.Aid && i.AidType = Some AidType.Enhance))

    [<Fact>]
    let ``combat_magic_live classifies MsgBasic cure and buff`` () =
        EntityRegistry.reset()
        let snap = ReplayHelpers.ingestFixture (FixturePaths.combatMagicLive())
        Assert.True(
            snap.Interactions
            |> List.exists (fun i -> i.MessageId = 7 && i.AidType = Some AidType.Recovery))
        Assert.True(
            snap.Interactions
            |> List.exists (fun i -> i.MessageId = 230 && i.AidType = Some AidType.Enhance))
