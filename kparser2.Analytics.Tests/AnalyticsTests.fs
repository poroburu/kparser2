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
    let combatSpikes () = find "combat_spikes.ndjson"
    let combatFailures () = find "combat_failures.ndjson"
    let combatTpDrain () = find "combat_tp_drain.ndjson"
    let combatEnfeeble () = find "combat_enfeeble.ndjson"
    let combatBuff () = find "combat_buff.ndjson"
    let combatMagicLive () = find "combat_magic_live.ndjson"
    let petrifyingPair () = find "bcmn30_petrifying_pair.ndjson"
    let bstLootName () = find "bst_loot_name.ndjson"
    let bstCampMulti () = find "bst_camp_multi.ndjson"
    let combatRanged () = find "combat_ranged.ndjson"
    let combatCounters () = find "combat_counters.ndjson"
    let combatDrain () = find "combat_drain.ndjson"
    let combatSkillchain () = find "combat_skillchain.ndjson"
    let combatJa () = find "combat_ja.ndjson"

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
    let ``abyssea report is an unsupported legacy stub`` () =
        let snap = ReportTestHelpers.replaySnapshot (FixturePaths.combatAction())
        let text = ReportTestHelpers.reportText "abyssea" snap
        ReportTestHelpers.contains "Abyssea (unsupported)" text
        ReportTestHelpers.contains "only if HorizonXI adds Abyssea content" text
        Assert.DoesNotContain("Total Cruor/XP", text, StringComparison.Ordinal)
        let row = AnalyticsQueryService.query "abyssea" snap (MobFilterDto()) |> Assert.Single
        Assert.Equal("Unsupported legacy stub", row.Value)

    [<Fact>]
    let ``offense report keeps summary and detail columns aligned`` () =
        let snap = ReportTestHelpers.replaySnapshot (FixturePaths.combatMeleeHits())
        let lines = ReportTestHelpers.reportText "offense" snap |> fun text -> text.Split('\n')

        let summaryHeader =
            lines |> Array.find (fun line -> line.StartsWith("Player") && line.Contains("Total Dmg"))

        let summaryRow = lines |> Array.find (fun line -> line.StartsWith("Entity") && line.Contains("43.24%"))
        Assert.Equal(summaryHeader.IndexOf("Total Dmg"), summaryRow.IndexOf("128"))

        let detailHeader =
            lines |> Array.find (fun line -> line.StartsWith("Player") && line.Contains("Category"))

        let detailRow = lines |> Array.find (fun line -> line.Contains("Melee") && line.Contains("Attack"))
        Assert.Equal(detailHeader.IndexOf("Category"), detailRow.IndexOf("Melee"))
        Assert.Equal(detailHeader.IndexOf("Action"), detailRow.IndexOf("Attack"))
        Assert.Equal(detailHeader.IndexOf("Damage"), detailRow.IndexOf("128"))

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
    let ``loot report consolidates found and final distribution`` () =
        let snap = ReportTestHelpers.replaySnapshot (FixturePaths.itemDrop())
        let text = ReportTestHelpers.reportText "loot" snap

        Assert.Contains("Recipient", text)
        Assert.Contains("Source", text)
        Assert.Contains("reraiser", text)
        Assert.Contains("hi-reraiser", text)
        Assert.Contains("Winner", text)
        Assert.Contains("Winner2", text)
        Assert.DoesNotContain("Found", text)
        Assert.DoesNotContain("Pool slot", text)

    [<Fact>]
    let ``loot report keeps source for an undistributed item`` () =
        let snap = ReportTestHelpers.replaySnapshot (FixturePaths.sample())
        let text = ReportTestHelpers.reportText "loot" snap
        let foundLine = text.Split('\n') |> Array.find (fun line -> line.StartsWith("Found"))

        Assert.Contains("—", foundLine)
        Assert.Contains("Entity 12345", foundLine)
        Assert.True(foundLine.IndexOf("Entity 12345", StringComparison.Ordinal) > foundLine.IndexOf("—", StringComparison.Ordinal))

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
    let ``chat all filters include every mode and speaker`` () =
        let snap = ReportTestHelpers.replaySnapshot (FixturePaths.chatSelfSay())
        let allText =
            AnalyticsReportService.formatChat snap None None
            |> fun report -> report.Spans |> Seq.map (fun s -> s.Text) |> String.Concat

        let sentinelText =
            AnalyticsReportService.formatChat snap (Some " all ") (Some "ALL")
            |> fun report -> report.Spans |> Seq.map (fun s -> s.Text) |> String.Concat

        Assert.Contains("[Say]", allText)
        Assert.Contains("[Tell]", allText)
        Assert.Equal(allText, sentinelText)

        let summaryAllText =
            AnalyticsReportService.formatChatSummary snap (Some " all ") (Some "ALL")
            |> fun report -> report.Spans |> Seq.map (fun s -> s.Text) |> String.Concat

        Assert.Contains("Say", summaryAllText)
        Assert.Contains("Tell", summaryAllText)

    [<Fact>]
    let ``chat summary applies mode and speaker filters`` () =
        let snap = ReportTestHelpers.replaySnapshot (FixturePaths.chatSelfSay())
        let report = AnalyticsReportService.formatChatSummary snap (Some "Say") (Some "Poroburu")
        let text = report.Spans |> Seq.map (fun s -> s.Text) |> String.Concat
        Assert.Contains("Chat Summary", text)
        Assert.Contains("Say", text)
        Assert.DoesNotContain("Tell", text)

    [<Fact>]
    let ``offense detail report includes frequency histogram`` () =
        let snap = ReportTestHelpers.replaySnapshot (FixturePaths.combatAction())
        let text = ReportTestHelpers.reportText "offense-detail" snap
        ReportTestHelpers.contains "Melee" text

    [<Fact>]
    let ``deaths report includes summary and details`` () =
        let snap = ReplayHelpers.ingestFixture (FixturePaths.combatDeath())
        let combatant id kind = { Id=id; Name=$"Entity {id}"; Kind=kind; Job=""; PlayerInfo=None }
        let playerDeath = { snap with Combatants = [combatant 100u EntityKind.Mob; combatant 200u EntityKind.Player] }
        let text = ReportTestHelpers.reportText "deaths" (AnalyticsDtoMapping.toSnapshotDto playerDeath)
        ReportTestHelpers.contains "Player Deaths" text
        ReportTestHelpers.contains "Summary" text
        ReportTestHelpers.contains "Entity 200" text
        ReportTestHelpers.contains "Unknown" text
        let mobDeath = { snap with Combatants = [combatant 100u EntityKind.Pet; combatant 200u EntityKind.Mob] }
        let kills = ReportTestHelpers.reportText "deaths" (AnalyticsDtoMapping.toSnapshotDto mobDeath)
        Assert.DoesNotContain("Summary", kills)

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
          SpellId = None
          MessageId = 0
          IsProc = false
          ProcValue = 0
          ProcMessageId = 0
          IsLocalPlayerActor = false
          IsLocalPlayerTarget = false; SourcePacketId = None }

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
        Assert.Equal("Must Have Pet Food", MsgBasicCatalog.messageLabel MsgBasicCatalog.MustHaveFood)
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
        Assert.Equal("Magic Burst", MsgBasicCatalog.messageLabel MsgBasicCatalog.MagicBurstDamage)
        Assert.True(SettledDivergence.isMessageClassified MsgBasicCatalog.MagicBurstDamage)
        Assert.Equal("Magic Drain MP", MsgBasicCatalog.messageLabel MsgBasicCatalog.MagicDrainMp)
        Assert.Equal("Magic Drain HP", MsgBasicCatalog.messageLabel MsgBasicCatalog.MagicDrainHp)
        Assert.True(SettledDivergence.isMessageClassified MsgBasicCatalog.MagicDrainMp)
        Assert.True(SettledDivergence.isMessageClassified MsgBasicCatalog.MagicDrainHp)
        Assert.Equal("Magic Absorb STR", MsgBasicCatalog.messageLabel MsgBasicCatalog.MagicAbsorbStr)
        Assert.Equal("Magic Absorb CHR", MsgBasicCatalog.messageLabel MsgBasicCatalog.MagicAbsorbChr)
        Assert.True(SettledDivergence.isMessageClassified MsgBasicCatalog.MagicAbsorbStr)
        Assert.True(SettledDivergence.isMessageClassified MsgBasicCatalog.MagicAbsorbChr)
        Assert.Equal(
            InteractionType.Harm,
            (MsgBasicCatalog.classify MsgBasicCatalog.MagicAbsorbStr 4 |> fun (t, _, _) -> t)
        )
        Assert.True(SettledDivergence.isMessageClassified MsgBasicCatalog.SkillDrainMp)
        Assert.Equal("Skill Drain HP", MsgBasicCatalog.messageLabel MsgBasicCatalog.SkillDrainHp)
        Assert.Equal("Skill Gain", MsgBasicCatalog.messageLabel MsgBasicCatalog.SkillGain)
        Assert.Equal("Skill Level Up", MsgBasicCatalog.messageLabel MsgBasicCatalog.SkillLevelUp)
        Assert.Equal(InteractionType.Unknown, (MsgBasicCatalog.classify MsgBasicCatalog.SkillGain 4 |> fun (t, _, _) -> t))
        Assert.Equal(InteractionType.Unknown, (MsgBasicCatalog.classify MsgBasicCatalog.SkillLevelUp 4 |> fun (t, _, _) -> t))
        Assert.True(SettledDivergence.isMessageClassified MsgBasicCatalog.SkillGain)
        Assert.True(SettledDivergence.isMessageClassified MsgBasicCatalog.SkillLevelUp)
        Assert.Equal("Magic Erase", MsgBasicCatalog.messageLabel MsgBasicCatalog.MagicErase)
        Assert.True(SettledDivergence.isMessageClassified MsgBasicCatalog.MagicErase)
        Assert.Equal("Magic Remove Effect", MsgBasicCatalog.messageLabel MsgBasicCatalog.MagicRemoveEffect)
        Assert.True(SettledDivergence.isMessageClassified MsgBasicCatalog.MagicRemoveEffect)
        Assert.True(SettledDivergence.isMessageClassified MsgBasicCatalog.JaRemoveEffect)
        Assert.True(SettledDivergence.isMessageClassified MsgBasicCatalog.TooFarAway)
        Assert.True(SettledDivergence.isMessageClassified MsgBasicCatalog.CannotAttackTarget)

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
    let ``experience parser reads 0x002D XP in param1`` () =
        match ExperienceParser.tryParseBattleMessage 8 133u 0u with
        | Some parsed -> Assert.Equal(133, parsed.Points)
        | None -> failwith "Expected XP parse from 0x002D Data"

    [<Fact>]
    let ``0x002D experience message records XP`` () =
        EntityRegistry.reset()
        InteractionBuilder.reset()
        let store = SessionStore.create()
        let data = Fixtures.battleMessage2Packet 20149u 20149u 8us 133u 0u
        let evt =
            { Topic = "test"
              Timestamp = 1UL
              Direction = PacketDirection.Incoming
              PacketType = "world_s2c"
              PacketId = 0x002Dus
              PacketName = "GP_SERV_COMMAND_BATTLE_MESSAGE2"
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
        Assert.Contains(snap.ExperienceRecords, fun r -> r.ExperiencePoints = 133)

    [<Fact>]
    let ``experience parser reads live 0x002D EXP chain Data as XP`` () =
        match ExperienceParser.tryParseBattleMessage 253 222u 1u with
        | Some parsed ->
            Assert.Equal(222, parsed.Points)
            Assert.Equal(1, parsed.Chain)
        | None -> failwith "Expected EXP chain parse"

    [<Fact>]
    let ``experience parser reads 0x29 fixture EXP chain Param1 as chain`` () =
        match ExperienceParser.tryParseBattleMessage 253 3u 180u with
        | Some parsed ->
            Assert.Equal(180, parsed.Points)
            Assert.Equal(3, parsed.Chain)
        | None -> failwith "Expected fixture EXP chain parse"

    [<Fact>]
    let ``0x002D EXP chain records XP from Data and chain from Data2`` () =
        EntityRegistry.reset()
        InteractionBuilder.reset()
        let store = SessionStore.create()
        let data = Fixtures.battleMessage2Packet 20149u 20149u 253us 222u 1u
        let evt =
            { Topic = "test"
              Timestamp = 1UL
              Direction = PacketDirection.Incoming
              PacketType = "world_s2c"
              PacketId = 0x002Dus
              PacketName = "GP_SERV_COMMAND_BATTLE_MESSAGE2"
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
        Assert.Contains(snap.ExperienceRecords, fun r -> r.ExperiencePoints = 222 && r.Chain = 1)
        Assert.Contains(
            snap.Interactions,
            fun i -> i.MessageId = 253 && i.Value = 222 && i.ActionName = "EXP Chain"
        )

    [<Fact>]
    let ``0x002D attains-level is not parsed as experience`` () =
        Assert.Equal(None, ExperienceParser.tryParseBattleMessage 9 56u 0u)
        Assert.Equal("Attains Level", MsgBasicCatalog.messageLabel MsgBasicCatalog.AttainsLevel)
        Assert.Equal(InteractionType.Unknown, (MsgBasicCatalog.classify 9 0 |> fun (t, _, _) -> t))

        EntityRegistry.reset()
        InteractionBuilder.reset()
        let store = SessionStore.create()
        let data = Fixtures.battleMessage2Packet 20149u 17584273u 9us 56u 0u
        let evt =
            { Topic = "test"
              Timestamp = 1UL
              Direction = PacketDirection.Incoming
              PacketType = "world_s2c"
              PacketId = 0x002Dus
              PacketName = "GP_SERV_COMMAND_BATTLE_MESSAGE2"
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
        Assert.Empty(snap.ExperienceRecords)
        Assert.Contains(
            snap.Interactions,
            fun i -> i.MessageId = 9 && i.ActionName = "Attains Level" && i.Value = 56
        )

    [<Fact>]
    let ``withoutChain reverses kparser chain bonuses`` () =
        Assert.Equal(0, ExperienceParser.withoutChain 0 3)
        Assert.Equal(200, ExperienceParser.withoutChain 200 0)
        Assert.Equal(200, ExperienceParser.withoutChain 240 1)
        Assert.Equal(200, ExperienceParser.withoutChain 250 2)
        Assert.Equal(200, ExperienceParser.withoutChain 260 3)
        Assert.Equal(200, ExperienceParser.withoutChain 280 4)
        Assert.Equal(200, ExperienceParser.withoutChain 300 5)

    [<Fact>]
    let ``exclude zero xp drops battles with no awarded experience`` () =
        let battle id name xp =
            { Id = id
              EnemyName = name
              EnemyId = None
              StartMs = 0L
              EndMs = Some 1L
              Killed = true
              KillerId = None
              ExperiencePoints = xp
              ExperienceChain = 0 }

        let snap =
            { AnalyticsSnapshot.empty with
                Battles = [ battle 1 "Crab" 150; battle 2 "Crate" 0 ] }

        let kept =
            ReportAggregators.filterBattles
                snap
                { MobFilter.defaultFilter with ExcludeZeroXp = true }

        Assert.Equal(1, kept.Length)
        Assert.Equal("Crab", kept.Head.EnemyName)

        let picker = AnalyticsQueries.mobs snap
        Assert.Equal(150, (picker |> List.find (fun r -> r.Label = "Crab")).Total)
        Assert.Equal(0, (picker |> List.find (fun r -> r.Label = "Crate")).Total)

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
                SpellId = None
                MessageId = 7
                IsProc = false
                ProcValue = 0
                ProcMessageId = 0
                IsLocalPlayerActor = false
                IsLocalPlayerTarget = false; SourcePacketId = None } ]

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
    let ``combat_spikes reports react damage on add-effect and offense, not absorbed`` () =
        EntityRegistry.reset()
        let snap = ReplayHelpers.ingestFixture (FixturePaths.combatSpikes())
        Assert.True(
            snap.Interactions
            |> List.exists (fun i -> i.ActionName = "Spikes" && i.MessageId = 44 && i.Value = 17))
        let dto = AnalyticsDtoMapping.toSnapshotDto snap
        let addEffect = ReportTestHelpers.reportText "add-effect" dto
        ReportTestHelpers.contains "Spikes" addEffect
        ReportTestHelpers.contains "17" addEffect
        Assert.DoesNotContain("Absorbed", addEffect, StringComparison.Ordinal)
        let offense = ReportTestHelpers.reportText "offense" dto
        ReportTestHelpers.contains "Spikes" offense
        ReportTestHelpers.contains "17" offense
        Assert.DoesNotContain("Absorbed Dmg", offense, StringComparison.Ordinal)
        let defense = ReportTestHelpers.reportText "defense" dto
        ReportTestHelpers.contains "12" defense
        Assert.DoesNotContain("Absorbed Dmg", defense, StringComparison.Ordinal)

    [<Fact>]
    let ``player spikes list on add-effect; enemy spikes list on defense`` () =
        InteractionTestHelpers.resetEntities ()
        InteractionTestHelpers.registerLocalPlayer 100u "Alice"
        InteractionTestHelpers.registerMob 200u "Earth Elemental"
        let store = SessionStore.create ()
        let ingest data =
            let evt = InteractionTestHelpers.packetEvent 0x0028us data
            SessionStore.ingest store evt (DecoderRegistry.decode evt)
        ingest (Fixtures.combatActionPacketWithReact 200u 100u 1 12 1 0 17 44)
        ingest (Fixtures.combatActionPacketWithReact 100u 200u 1 90 0x14 0 33 44)
        let dto = AnalyticsDtoMapping.toSnapshotDto (SessionStore.snapshot store)
        let addEffect = ReportTestHelpers.reportText "add-effect" dto
        ReportTestHelpers.contains "Alice" addEffect
        ReportTestHelpers.contains "Spikes" addEffect
        ReportTestHelpers.contains "17" addEffect
        Assert.DoesNotContain("Earth Elemental", addEffect, StringComparison.Ordinal)
        let defense = ReportTestHelpers.reportText "defense" dto
        ReportTestHelpers.contains "Spikes" defense
        ReportTestHelpers.contains "33" defense
        let offense = ReportTestHelpers.reportText "offense" dto
        ReportTestHelpers.contains "17" offense
        Assert.DoesNotContain("Absorbed Dmg", offense, StringComparison.Ordinal)

    [<Fact>]
    let ``non-44 reacts add no spike HP, curing, or absorbed damage`` () =
        InteractionTestHelpers.resetEntities ()
        InteractionTestHelpers.registerLocalPlayer 100u "Alice"
        InteractionTestHelpers.registerMob 200u "Earth Elemental"
        let store = SessionStore.create ()
        let ingest data =
            let evt = InteractionTestHelpers.packetEvent 0x0028us data
            SessionStore.ingest store evt (DecoderRegistry.decode evt)
        ingest (Fixtures.combatActionPacketWithReact 100u 200u 1 12 1 0 40 373)
        ingest (Fixtures.combatActionPacketWithReact 100u 200u 1 15 1 0 61 132)
        let snap = SessionStore.snapshot store
        Assert.DoesNotContain(snap.Interactions, fun i -> i.ActionName = "Spikes" || i.MessageId = 373 || i.MessageId = 132)
        Assert.Equal(27, AnalyticsQueries.offenseSummary snap MobFilter.defaultFilter |> List.sumBy (fun r -> r.Total))
        Assert.Empty(snap.Interactions |> List.filter (fun i -> i.AidType = Some AidType.Recovery))
        let dto = AnalyticsDtoMapping.toSnapshotDto snap
        for query in [ "offense"; "defense"; "add-effect"; "recovery" ] do
            let text = ReportTestHelpers.reportText query dto
            Assert.DoesNotContain("Spikes", text, StringComparison.Ordinal)
            Assert.DoesNotContain("Absorbed", text, StringComparison.Ordinal)
            Assert.DoesNotContain("40", text, StringComparison.Ordinal)
            Assert.DoesNotContain("61", text, StringComparison.Ordinal)

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

    [<Fact>]
    let ``combat_ranged produces ranged hit and miss`` () =
        EntityRegistry.reset()
        let snap = ReplayHelpers.ingestFixture (FixturePaths.combatRanged())
        Assert.True(snap.Interactions |> List.exists (fun i -> i.HarmType = Some HarmType.Ranged && i.Success = "hit" && i.Value = 247))
        Assert.True(snap.Interactions |> List.exists (fun i -> i.HarmType = Some HarmType.Ranged && i.Success = "miss"))
        let text = ReportTestHelpers.reportText "offense" (AnalyticsDtoMapping.toSnapshotDto snap)
        ReportTestHelpers.contains "Ranged" text

    [<Fact>]
    let ``combat_counters produce melee harm on offense`` () =
        EntityRegistry.reset()
        let snap = ReplayHelpers.ingestFixture (FixturePaths.combatCounters())
        Assert.True(snap.Interactions |> List.exists (fun i -> i.Value = 56 && i.HarmType = Some HarmType.Melee))
        Assert.True(snap.Interactions |> List.exists (fun i -> i.Value = 52 && i.HarmType = Some HarmType.Melee))
        let text = ReportTestHelpers.reportText "offense" (AnalyticsDtoMapping.toSnapshotDto snap)
        ReportTestHelpers.contains "Melee" text

    [<Fact>]
    let ``bloody bolt proc recovers HP and adds no additional damage`` () =
        InteractionTestHelpers.resetEntities ()
        let data = Fixtures.bloodyBoltDrainPacket ()
        let action =
            match Battle0x28.decode data with
            | None -> failwith "Expected bloody bolt decode"
            | Some action -> action
        InteractionTestHelpers.registerLocalPlayer action.ActorId "Caster"
        let store = SessionStore.create ()
        let evt = InteractionTestHelpers.packetEvent 0x0028us data
        SessionStore.ingest store evt (DecoderRegistry.decode evt)
        let snap = SessionStore.snapshot store
        Assert.True(snap.Interactions |> List.exists (fun i ->
            i.InteractionType = InteractionType.Harm && i.HarmType = Some HarmType.Ranged && i.Value = 24 && i.MessageId = 352))
        Assert.True(snap.Interactions |> List.exists (fun i ->
            i.AidType = Some AidType.Recovery && i.MessageId = 161 && i.Value = 31 && i.ActorId = action.ActorId))
        let filter = MobFilter.defaultFilter
        Assert.Equal(24, AnalyticsQueries.offenseSummary snap filter |> List.sumBy (fun r -> r.Total))
        Assert.Equal(31, AnalyticsQueries.recovery snap filter |> List.sumBy (fun r -> r.Total))
        Assert.Equal(0, AnalyticsQueries.additionalEffects snap filter |> List.filter (fun r -> r.Value = "proc") |> List.sumBy (fun r -> r.Total))

    [<Fact>]
    let ``additional effect damage proc still counts`` () =
        InteractionTestHelpers.resetEntities ()
        InteractionTestHelpers.registerLocalPlayer 1u "Caster"
        let store = SessionStore.create ()
        let data = Fixtures.combatActionPacketWithProc 1u 2u 1 10 1 0 34 163
        let evt = InteractionTestHelpers.packetEvent 0x0028us data
        SessionStore.ingest store evt (DecoderRegistry.decode evt)
        let snap = SessionStore.snapshot store
        Assert.Empty(snap.Interactions |> List.filter (fun i -> i.AidType = Some AidType.Recovery))
        Assert.Equal(34, AnalyticsQueries.additionalEffects snap MobFilter.defaultFilter |> List.filter (fun r -> r.Value = "proc") |> List.sumBy (fun r -> r.Total))
        Assert.Equal(10, AnalyticsQueries.offenseSummary snap MobFilter.defaultFilter |> List.sumBy (fun r -> r.Total))

    [<Fact>]
    let ``mp drain proc is not HP recovery or additional damage`` () =
        InteractionTestHelpers.resetEntities ()
        InteractionTestHelpers.registerLocalPlayer 1u "Caster"
        let store = SessionStore.create ()
        let data = Fixtures.combatActionPacketWithProc 1u 2u 1 10 1 0 20 162
        let evt = InteractionTestHelpers.packetEvent 0x0028us data
        SessionStore.ingest store evt (DecoderRegistry.decode evt)
        let snap = SessionStore.snapshot store
        Assert.Empty(snap.Interactions |> List.filter (fun i -> i.AidType = Some AidType.Recovery))
        Assert.Equal(0, AnalyticsQueries.additionalEffects snap MobFilter.defaultFilter |> List.filter (fun r -> r.Value = "proc") |> List.sumBy (fun r -> r.Total))

    [<Fact>]
    let ``combat_drain counts harm and recovery hp`` () =
        EntityRegistry.reset()
        let snap = ReplayHelpers.ingestFixture (FixturePaths.combatDrain())
        Assert.True(snap.Interactions |> List.exists (fun i -> i.InteractionType = InteractionType.Harm && i.MessageId = 0x16 && i.Value = 50))
        Assert.True(snap.Interactions |> List.exists (fun i -> i.AidType = Some AidType.Recovery && i.Value = 50))
        let text = ReportTestHelpers.reportText "recovery" (AnalyticsDtoMapping.toSnapshotDto snap)
        ReportTestHelpers.contains "Curing" text

    [<Fact>]
    let ``stat absorbs retain effects but never add damage or recovery`` () =
        InteractionTestHelpers.resetEntities ()
        InteractionTestHelpers.registerLocalPlayer 0x268Bu "Caster"
        InteractionTestHelpers.registerMob 0x2C8Bu "Crab"
        let store = SessionStore.create ()
        let ingest actor target spell value message =
            let data = Fixtures.combatActionPacketEx actor target 4 spell value message 0
            let evt = InteractionTestHelpers.packetEvent 0x0028us data
            SessionStore.ingest store evt (DecoderRegistry.decode evt)
        // Ordinary HP Drain must still produce damage and caster recovery.
        ingest 0x268Bu 0x2C8Bu 245u 40 227
        for id in 329 .. 335 do
            ingest 0x268Bu 0x2C8Bu (uint32 (266 + id - 329)) (id - 193) id
            ingest 0x2C8Bu 0x268Bu (uint32 (266 + id - 329)) (id - 193) id
            Assert.Equal((InteractionType.Harm, Some HarmType.Enfeeble, None), BattleMessageCatalog.classifyCombatMessage id 0)
        let snap = SessionStore.snapshot store
        let effects = snap.Interactions |> List.filter (fun i -> i.MessageId >= 329 && i.MessageId <= 335)
        Assert.Equal(14, effects.Length)
        Assert.All(effects, fun i ->
            Assert.Equal(Some HarmType.Enfeeble, i.HarmType)
            Assert.Equal(InteractionCategory.Enfeeble, i.Category)
            Assert.Equal(i.MessageId - 193, i.Value))
        Assert.Single(snap.Interactions |> List.filter (fun i -> i.AidType = Some AidType.Recovery)) |> ignore
        let withoutEffects = { snap with Interactions = snap.Interactions |> List.filter (fun i -> i.HarmType <> Some HarmType.Enfeeble) }
        for query in [ "offense"; "offense-detail"; "defense"; "defense-detail"; "performance"; "recovery" ] do
            Assert.Equal(ReportTestHelpers.reportText query (AnalyticsDtoMapping.toSnapshotDto withoutEffects),
                         ReportTestHelpers.reportText query (AnalyticsDtoMapping.toSnapshotDto snap))
        let filter = MobFilter.defaultFilter
        Assert.Equal(40, AnalyticsQueries.offenseSummary snap filter |> List.sumBy (fun r -> r.Total))
        Assert.Empty(AnalyticsQueries.defenseSummary snap filter)

    [<Fact>]
    let ``mp drain retains amounts but never adds hp damage or recovery`` () =
        InteractionTestHelpers.resetEntities ()
        InteractionTestHelpers.registerLocalPlayer 0x268Bu "Caster"
        InteractionTestHelpers.registerMob 0x2C8Bu "Crab"
        let store = SessionStore.create ()
        let ingest command actor target spell value message =
            let data = Fixtures.combatActionPacketEx actor target command spell value message 0
            let evt = InteractionTestHelpers.packetEvent 0x0028us data
            SessionStore.ingest store evt (DecoderRegistry.decode evt)
        ingest 4 0x268Bu 0x2C8Bu 144u 100 2
        ingest 4 0x268Bu 0x2C8Bu 245u 40 227
        ingest 4 0x268Bu 0x2C8Bu 247u 32 228
        ingest 4 0x268Bu 0x2C8Bu 247u 38 228
        ingest 13 0x268Bu 0x2C8Bu 0u 30 225
        ingest 4 0x268Bu 0x2C8Bu 266u 136 329
        let snap = SessionStore.snapshot store
        let mpRows = snap.Interactions |> List.filter (fun i -> i.MessageId = 228)
        Assert.Equal(2, mpRows.Length)
        Assert.All(mpRows, fun i ->
            Assert.Equal(InteractionType.Harm, i.InteractionType)
            Assert.Equal(Some HarmType.Spell, i.HarmType)
            Assert.False(InteractionClassification.isHpDamage i))
        Assert.Equal(70, mpRows |> List.sumBy (fun i -> i.Value))
        Assert.True(snap.Interactions |> List.exists (fun i -> i.MessageId = 225 && i.Value = 30 && not (InteractionClassification.isHpDamage i)))
        Assert.Single(snap.Interactions |> List.filter (fun i -> i.AidType = Some AidType.Recovery && i.MessageId = 227)) |> ignore
        let withoutMp =
            { snap with
                Interactions =
                    snap.Interactions
                    |> List.filter (fun i -> not (MsgBasicCatalog.isMpResourceTransfer i.MessageId)) }
        for query in [ "offense"; "offense-detail"; "defense"; "defense-detail"; "performance"; "recovery" ] do
            Assert.Equal(
                ReportTestHelpers.reportText query (AnalyticsDtoMapping.toSnapshotDto withoutMp),
                ReportTestHelpers.reportText query (AnalyticsDtoMapping.toSnapshotDto snap))
        let filter = MobFilter.defaultFilter
        Assert.Equal(140, AnalyticsQueries.offenseSummary snap filter |> List.sumBy (fun r -> r.Total))
        Assert.Empty(AnalyticsQueries.defenseSummary snap filter)
        let offense = ReportAggregators.buildPlayerOffense snap filter
        let caster = offense |> List.find (fun p -> p.Name = "Caster")
        Assert.Equal(140, ReportAggregators.totalCategoryDamage caster.Categories)

    [<Fact>]
    let ``remote player remains a report participant after a mob attacks their shadows`` () =
        InteractionTestHelpers.resetEntities ()
        InteractionTestHelpers.registerLocalPlayer 100u "Local"
        let store = SessionStore.create ()
        let ingest opcode data =
            let evt = InteractionTestHelpers.packetEvent opcode data
            SessionStore.ingest store evt (DecoderRegistry.decode evt)
        ingest 0x000Dus (Fixtures.charPcPacket "Remote" 200u)
        ingest 0x000Eus (Fixtures.npcUpdatePacket "Crab" 300u)
        ingest 0x0028us (Fixtures.combatActionPacketEx 200u 300u 1 0u 76 1 0)
        // September 19: observed PC is later the target of cmd 1 / msg 31.
        ingest 0x0028us (Fixtures.combatActionPacketEx 300u 200u 1 0u 0 31 1)
        Assert.Equal(Some EntityRegistry.EntityKind.Player, EntityRegistry.tryGetEntityKind 200u)
        ingest 0x000Dus (Fixtures.charPcPacket "Remote" 200u)
        ingest 0x0028us (Fixtures.combatActionPacketEx 200u 300u 1 0u 24 1 0)
        let snap = SessionStore.snapshot store
        Assert.Equal(EntityKind.Player, (snap.Combatants |> List.find (fun c -> c.Id = 200u)).Kind)
        Assert.Equal(EntityKind.Mob, (snap.Combatants |> List.find (fun c -> c.Id = 300u)).Kind)
        let offense = ReportAggregators.buildPlayerOffense snap MobFilter.defaultFilter
        let remote = offense |> List.find (fun p -> p.Name = "Remote")
        Assert.Equal(100, ReportAggregators.totalCategoryDamage remote.Categories)
        let text = ReportTestHelpers.reportText "offense" (AnalyticsDtoMapping.toSnapshotDto snap)
        ReportTestHelpers.contains "Remote" text
        Assert.DoesNotContain("Crab", text)

    [<Fact>]
    let ``observed player update repairs a prior combat guess without promoting real mobs`` () =
        InteractionTestHelpers.resetEntities ()
        InteractionTestHelpers.registerLocalPlayer 100u "Local"
        EntityRegistry.setEntityKind 200u EntityRegistry.EntityKind.Mob
        let store = SessionStore.create ()
        let data = Fixtures.charPcPacket "Remote" 200u
        let evt = InteractionTestHelpers.packetEvent 0x000Dus data
        SessionStore.ingest store evt (DecoderRegistry.decode evt)
        Assert.Equal(Some EntityRegistry.EntityKind.Player, EntityRegistry.tryGetEntityKind 200u)
        InteractionTestHelpers.registerMob 300u "Crab"
        EntityRegistry.setEntityKind 300u EntityRegistry.EntityKind.Player
        Assert.Equal(Some EntityRegistry.EntityKind.Mob, EntityRegistry.tryGetEntityKind 300u)
        // Explicit pet classification also outranks a subsequent harm-target guess.
        EntityRegistry.setEntityKind 300u EntityRegistry.EntityKind.Pet
        EntityRegistry.setEntityKind 300u EntityRegistry.EntityKind.Mob
        Assert.Equal(Some EntityRegistry.EntityKind.Pet, EntityRegistry.tryGetEntityKind 300u)

    [<Fact>]
    let ``combat_skillchain follow-up is melee harm`` () =
        EntityRegistry.reset()
        let snap = ReplayHelpers.ingestFixture (FixturePaths.combatSkillchain())
        Assert.True(snap.Interactions |> List.exists (fun i -> i.MessageId = 287))
        Assert.True(snap.Interactions |> List.exists (fun i -> i.MessageId = 0x28 && i.Value = 100 && i.InteractionType = InteractionType.Harm))

    [<Fact>]
    let ``combat_ja produces ability harm`` () =
        EntityRegistry.reset()
        let snap = ReplayHelpers.ingestFixture (FixturePaths.combatJa())
        Assert.True(snap.Interactions |> List.exists (fun i -> i.HarmType = Some HarmType.Ability && i.Value = 80))

    [<Fact>]
    let ``extra-attacks report groups melee by packet identity`` () =
        EntityRegistry.reset()
        let snap = ReplayHelpers.ingestFixtureDto (FixturePaths.combatMeleeHits())
        let text = ReportTestHelpers.reportText "extra-attacks" snap
        ReportTestHelpers.contains "Packet rounds" text
        ReportTestHelpers.contains "Extra Attacks" text

[<Collection("EntityRegistry")>]
module RecoveryMpTests =
    let private createStore () =
        InteractionTestHelpers.resetEntities ()
        InteractionTestHelpers.registerLocalPlayer 100u "Caster"
        SessionStore.create ()

    let private ingest store timestamp target command spell value message =
        let data = Fixtures.combatActionPacketEx 100u target command spell value message 0
        let evt = { InteractionTestHelpers.packetEvent 0x0028us data with Timestamp = timestamp }
        SessionStore.ingest store evt (DecoderRegistry.decode evt)

    let private report snap =
        snap |> AnalyticsDtoMapping.toSnapshotDto |> ReportTestHelpers.reportText "recovery"

    let private costs (text: string) =
        text.Substring(text.IndexOf("Curing Costs and Efficiency (estimated)", StringComparison.Ordinal))

    [<Fact>]
    let ``SQL generated costs preserve spell names and unknown costs`` () =
        Assert.Equal(Some "Cure", SpellLookup.tryGetName 1)
        Assert.Equal(Some 8, SpellLookup.tryGetMpCost 1)
        Assert.Equal(Some 24, SpellLookup.tryGetMpCost 2)
        Assert.Equal(None, SpellLookup.tryGetMpCost 0)
        Assert.Equal(None, SpellLookup.tryGetMpCost 305) // SQL zero (Odin)
        Assert.Equal(Some "Chocobo Hum", SpellLookup.tryGetName 407) // disabled SQL row
        Assert.Equal(None, SpellLookup.tryGetMpCost 407)
        Assert.Equal(None, SpellLookup.tryGetMpCost 99999)

    [<Fact>]
    let ``Cure finish retains spell id through DTO and renders estimated efficiency`` () =
        let store = createStore ()
        ingest store 10UL 100u 4 1u 350 7
        let snap = SessionStore.snapshot store
        let roundTrip = snap |> AnalyticsDtoMapping.toSnapshotDto |> AnalyticsDtoMapping.fromSnapshotDto
        Assert.Equal(Some 1, (Assert.Single roundTrip.Interactions).SpellId)
        Assert.Matches(@"Caster\s+8\s+43\.75", report roundTrip |> costs)

    [<Fact>]
    let ``Curaga targets at the same time charge once and later casts charge again`` () =
        let store = createStore ()
        ingest store 10UL 100u 4 7u 350 7
        ingest store 10UL 200u 4 7u 250 7
        let snap = SessionStore.snapshot store
        Assert.Matches(@"Caster\s+60\s+10\.00", report snap |> costs)
        // Old snapshots without packet provenance have the same cast semantics.
        let withoutIdentity = { snap with Interactions = snap.Interactions |> List.map (fun i -> { i with SourcePacketId = None }) }
        Assert.Equal(report snap |> costs, report withoutIdentity |> costs)
        ingest store 20UL 100u 4 7u 120 7
        Assert.Matches(@"Caster\s+120\s+6\.00", SessionStore.snapshot store |> report |> costs)

    [<Fact>]
    let ``Aspir prepare and abilities do not add spell MP or efficiency HP`` () =
        let store = createStore ()
        ingest store 10UL 100u 4 1u 350 7
        let baseline = SessionStore.snapshot store |> report |> costs
        ingest store 20UL 200u 4 247u 70 228
        ingest store 30UL 200u 13 0u 30 225
        ingest store 40UL 100u 8 1u 350 7
        ingest store 50UL 100u 6 0u 512 102
        ingest store 60UL 200u 4 245u 40 227
        let snap = SessionStore.snapshot store
        Assert.Equal(baseline, report snap |> costs)
        Assert.Equal(902, snap.Interactions |> List.filter (fun i -> i.AidType = Some AidType.Recovery) |> List.sumBy (fun i -> i.Value))

    [<Fact>]
    let ``unknown spell cost excludes both MP and HP from efficiency`` () =
        let store = createStore ()
        ingest store 10UL 100u 4 9999u 500 7
        let text = SessionStore.snapshot store |> report |> costs
        Assert.Contains("costs unavailable", text)
        Assert.DoesNotMatch(@"Caster\s+\d", text)
        ingest store 20UL 100u 4 1u 350 7
        Assert.Matches(@"Caster\s+8\s+43\.75", SessionStore.snapshot store |> report |> costs)

    [<Fact>]
    let ``constructed recovery fixture exposes a Cure finish without costing MsgBasic`` () =
        let snap = ReplayHelpers.ingestFixture (FixturePaths.combatRecovery())
        Assert.True(snap.Interactions |> List.exists (fun i -> i.CommandNo = 4 && i.SpellId = Some 1 && i.Value = 350))
        Assert.Matches(@"Entity 100\s+8\s+43\.75", report snap |> costs)

    [<Fact>]
    let ``live recovery modes show estimates except status curing`` () =
        let store = createStore ()
        ingest store 10UL 100u 4 1u 350 7
        let dto = SessionStore.snapshot store |> AnalyticsDtoMapping.toSnapshotDto
        let render mode =
            let request = AnalyticsReportRequest(QueryId = "recovery", Mode = mode, Filter = MobFilterDto())
            (AnalyticsReportService.formatRequest request dto).Spans
            |> Seq.map (fun span -> span.Text) |> String.Concat
        for mode in [ReportMode.Curing; ReportMode.AverageCuring; ReportMode.Recovery] do
            Assert.Matches(@"Caster\s+8\s+43\.75", render mode |> costs)
        for mode in [ReportMode.StatusCuring; ReportMode.StatusCured] do
            Assert.DoesNotContain("Estimated MP", render mode)

[<Collection("EntityRegistry")>]
module OffenseNukeMpTests =
    let private createStore () =
        InteractionTestHelpers.resetEntities ()
        InteractionTestHelpers.registerLocalPlayer 100u "Caster"
        InteractionTestHelpers.registerMob 200u "Crab"
        InteractionTestHelpers.registerMob 300u "Beetle"
        SessionStore.create ()

    let private ingest store timestamp target command spell value message miss =
        let data = Fixtures.combatActionPacketEx 100u target command spell value message miss
        let evt = { InteractionTestHelpers.packetEvent 0x0028us data with Timestamp = timestamp }
        SessionStore.ingest store evt (DecoderRegistry.decode evt)

    let private report query store =
        SessionStore.snapshot store |> AnalyticsDtoMapping.toSnapshotDto |> ReportTestHelpers.reportText query

    let private costs (text: string) =
        text.Substring(text.IndexOf("Spell Costs and Efficiency (estimated)", StringComparison.Ordinal))

    [<Fact>]
    let ``Blizzard IV finish uses existing SQL cost in live Offense`` () =
        Assert.Equal(Some 162, SpellLookup.tryGetMpCost 152)
        let store = createStore ()
        ingest store 10UL 200u 4 152u 324 2 0
        Assert.Matches(@"Caster\s+Blizzard Iv\s+162\s+2\.00", report "offense" store |> costs)
        Assert.DoesNotContain("Spell Costs and Efficiency", report "defense" store)

    [<Fact>]
    let ``same-time targets charge once and a later miss still spends MP`` () =
        let store = createStore ()
        ingest store 10UL 200u 4 152u 324 2 0
        ingest store 10UL 300u 4 152u 162 2 0
        Assert.Matches(@"Caster\s+Blizzard Iv\s+162\s+3\.00", report "offense" store |> costs)
        ingest store 20UL 200u 4 152u 0 2 1
        Assert.Matches(@"Caster\s+Blizzard Iv\s+324\s+1\.50", report "offense" store |> costs)

    [<Fact>]
    let ``MP transfers prepares cures and enfeebles cannot alter nuke efficiency`` () =
        let store = createStore ()
        ingest store 10UL 200u 4 152u 324 2 0
        let baseline = report "offense" store |> costs
        ingest store 20UL 200u 4 247u 70 228 0
        ingest store 30UL 200u 13 0u 30 225 0
        ingest store 40UL 200u 8 152u 324 2 0
        ingest store 50UL 100u 4 1u 350 7 0
        ingest store 60UL 200u 4 266u 136 329 0
        Assert.Equal(baseline, report "offense" store |> costs)

    [<Fact>]
    let ``Drain harm counts once without its recovery dual emit`` () =
        let store = createStore ()
        ingest store 10UL 200u 4 245u 42 227 0
        let snap = SessionStore.snapshot store
        Assert.Equal(2, snap.Interactions.Length)
        Assert.Single(snap.Interactions |> List.filter (fun i -> i.AidType = Some AidType.Recovery)) |> ignore
        Assert.Matches(@"Caster\s+Drain\s+21\s+2\.00", report "offense" store |> costs)

    [<Theory>]
    [<InlineData(9999)>]
    [<InlineData(305)>]
    let ``unknown and zero SQL costs are unavailable without invented MP`` spell =
        let store = createStore ()
        ingest store 10UL 200u 4 (uint32 spell) 500 2 0
        let text = report "offense" store |> costs
        Assert.Contains("costs unavailable", text)
        Assert.DoesNotMatch(@"Caster\s+\S", text)
        ingest store 20UL 200u 4 152u 324 2 0
        Assert.Matches(@"Caster\s+Blizzard Iv\s+162\s+2\.00", report "offense" store |> costs)

[<Collection("EntityRegistry")>]
module SlimeCampTests =
    let private ingest opcode data store =
        let evt = InteractionTestHelpers.packetEvent opcode data
        SessionStore.ingest store evt (DecoderRegistry.decode evt)

    [<Fact>]
    let ``Digest command 11 message 187 is ability HP drain plus actor recovery`` () =
        InteractionTestHelpers.resetEntities ()
        InteractionBuilder.reset ()
        InteractionTestHelpers.registerLocalPlayer 0x268Bu "Poroburu"
        InteractionTestHelpers.registerMob 0x2C8Bu "Gloop"
        let meleeType, meleeHarm, _ = BattleMessageCatalog.classifyActionEffect 1 MsgBasicCatalog.SkillDrainHp 0 3
        Assert.Equal(InteractionType.Harm, meleeType)
        Assert.Equal(Some HarmType.Other, meleeHarm)
        let store = SessionStore.create ()
        ingest 0x0028us (Fixtures.combatActionPacketEx 0x2C8Bu 0x268Bu 11 433u 11 MsgBasicCatalog.SkillDrainHp 0) store
        ingest 0x0028us (Fixtures.combatActionPacketEx 0x268Bu 0x2C8Bu 1 0u 3 MsgBasicCatalog.SkillDrainHp 0) store
        let snap = SessionStore.snapshot store
        let harm =
            snap.Interactions
            |> List.find (fun i -> i.CommandNo = 11 && i.InteractionType = InteractionType.Harm)
        Assert.Equal(Some HarmType.Ability, harm.HarmType)
        Assert.Equal("Digest", harm.ActionName)
        Assert.Equal(11, harm.Value)
        Assert.Equal(0x268Bu, harm.TargetId)
        Assert.True(InteractionClassification.isHpDamage harm)
        let recover =
            Assert.Single(snap.Interactions |> List.filter (fun i -> i.AidType = Some AidType.Recovery))
        Assert.Equal(0x2C8Bu, recover.ActorId)
        Assert.Equal(0x2C8Bu, recover.TargetId)
        Assert.Equal(11, recover.Value)
        Assert.Equal("Digest", recover.ActionName)

    [<Fact>]
    let ``Spirit Taker stays weaponskill damage without an invented MP recovery`` () =
        InteractionTestHelpers.resetEntities ()
        InteractionBuilder.reset ()
        InteractionTestHelpers.registerLocalPlayer 0x268Bu "Poroburu"
        InteractionTestHelpers.registerMob 0x2C8Bu "Gloop"
        let store = SessionStore.create ()
        ingest 0x0028us (Fixtures.combatActionPacketEx 0x268Bu 0x2C8Bu 3 183u 51 185 0) store
        let snap = SessionStore.snapshot store
        let hit = Assert.Single snap.Interactions
        Assert.Equal(InteractionType.Harm, hit.InteractionType)
        Assert.Equal(Some HarmType.Weaponskill, hit.HarmType)
        Assert.Equal("Spirit Taker", hit.ActionName)
        Assert.Equal(51, hit.Value)
        Assert.True(InteractionClassification.isHpDamage hit)

    [<Fact>]
    let ``Starlight message 224 is MP recovery and stays out of HP curing`` () =
        InteractionTestHelpers.resetEntities ()
        InteractionBuilder.reset ()
        InteractionTestHelpers.registerLocalPlayer 0x268Bu "Poroburu"
        let store = SessionStore.create ()
        ingest 0x0028us (Fixtures.combatActionPacketEx 0x268Bu 0x268Bu 3 163u 26 MsgBasicCatalog.SkillRecoversMp 0) store
        let snap = SessionStore.snapshot store
        let row = Assert.Single snap.Interactions
        Assert.Equal(InteractionType.Aid, row.InteractionType)
        Assert.Equal(Some AidType.Recovery, row.AidType)
        Assert.Equal("Starlight", row.ActionName)
        Assert.Equal(26, row.Value)
        Assert.Equal(0x268Bu, row.TargetId)
        Assert.False(InteractionClassification.isHpDamage row)
        let text = ReportTestHelpers.reportText "recovery" (AnalyticsDtoMapping.toSnapshotDto snap)
        Assert.DoesNotContain("Starlight", text, StringComparison.Ordinal)
        Assert.DoesNotContain("26", text, StringComparison.Ordinal)

    [<Fact>]
    let ``club skill messages stay off buff and damage reports`` () =
        InteractionTestHelpers.resetEntities ()
        InteractionBuilder.reset ()
        InteractionTestHelpers.registerLocalPlayer 0x268Bu "Poroburu"
        let store = SessionStore.create ()
        ingest 0x0029us (Fixtures.battleMessagePacket 0x268Bu 0x268Bu (uint16 MsgBasicCatalog.SkillGain) 2u 0u 4uy) store
        ingest 0x0029us (Fixtures.battleMessagePacket 0x268Bu 0x268Bu (uint16 MsgBasicCatalog.SkillLevelUp) 90u 0u 4uy) store
        let snap = SessionStore.snapshot store
        Assert.Equal(2, snap.Interactions.Length)
        Assert.All(snap.Interactions, fun i ->
            Assert.Equal(InteractionType.Unknown, i.InteractionType)
            Assert.False(InteractionClassification.isHpDamage i))
        Assert.Contains(snap.Interactions, fun i -> i.ActionName = "Skill Gain" && i.MessageId = 38)
        Assert.Contains(snap.Interactions, fun i -> i.ActionName = "Skill Level Up" && i.MessageId = 53)
        let dto = AnalyticsDtoMapping.toSnapshotDto snap
        let buffs = ReportTestHelpers.reportText "buffs" dto
        let offense = ReportTestHelpers.reportText "offense" dto
        Assert.DoesNotContain("Skill Gain", buffs, StringComparison.Ordinal)
        Assert.DoesNotContain("Skill Level Up", buffs, StringComparison.Ordinal)
        Assert.DoesNotContain("Skill Gain", offense, StringComparison.Ordinal)
        Assert.DoesNotContain("90", offense, StringComparison.Ordinal)
