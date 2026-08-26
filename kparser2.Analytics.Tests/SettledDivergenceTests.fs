namespace kparser2.Analytics.Tests

open System
open System.IO
open Xunit
open kparser2.Analytics
open kparser2.Decoders
open kparser2.Decoders.Tests
open kparser2.Ingest
open kparser2.Protocol

module private SettledHelpers =
    let fixture name =
        [ Path.Combine(AppContext.BaseDirectory, "fixtures", "sessions", name)
          Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "fixtures", "sessions", name)) ]
        |> List.find File.Exists

    let ingestFixture path =
        EntityRegistry.reset ()
        let store = SessionStore.create ()

        for topic, metaJson, data in Ndjson.readAll path do
            let meta = PacketMeta.parseString metaJson
            let evt = PacketMeta.toEvent topic meta data
            EntityRegistry.observe evt
            SessionStore.ingest store evt (DecoderRegistry.decode evt)

        SessionStore.snapshot store
    let mkEvt (ts: int) packetId direction (data: byte[]) : PacketEvent =
        { Topic = "test"
          Timestamp = uint64 ts
          Direction = direction
          PacketType =
            if direction = PacketDirection.Outgoing then
                "world_c2s"
            else
                "world_s2c"
          PacketId = packetId
          PacketName = "test"
          Size = uint32 data.Length
          Injected = false
          Blocked = false
          SessionUuid = "test"
          Version = "v1"
          MessageId = uint64 ts
          Data = data }

    let ingest decoded evt store =
        SessionStore.ingest store evt decoded

    let ingestPacket store ts packetId direction data =
        let evt = mkEvt ts packetId direction data
        EntityRegistry.observe evt
        SessionStore.ingest store evt (DecoderRegistry.decode evt)

    let interaction commandNo messageId interactionType actionName : Interaction =
        { Id = 0
          BattleId = None
          TimestampMs = 10L
          InteractionType = interactionType
          HarmType = Some HarmType.Spell
          AidType = None
          Category = InteractionCategory.Spell
          DamageModifier = DamageModifier.Normal
          ActorId = 1u
          TargetId = 2u
          ActorName = "Alice"
          TargetName = "Crab"
          ActionName = actionName
          Value = 1
          Success = "hit"
          CommandNo = commandNo
          MessageId = messageId
          IsProc = false
          ProcValue = 0
          IsLocalPlayerActor = true
          IsLocalPlayerTarget = false }

    let chat mode modeId speaker packetId : ChatMessageRecord =
        { TimestampMs = 10L
          Mode = mode
          ModeId = modeId
          IsGm = false
          Speaker = speaker
          Message = "hello"
          PacketId = packetId
          Direction = "incoming"
          IsLocalPlayer = true
          TargetName = None }

    let hasCode (report: SettledDivergence.Report) code =
        report.Actionable |> List.exists (fun i -> i.Code = code)
        || report.Deferred |> List.exists (fun i -> i.Code = code)

[<Collection("EntityRegistry")>]
module SettledDivergenceTests =
    [<Fact>]
    let ``cmd 8 start is not a settled harm gap on combat_magic_live`` () =
        EntityRegistry.reset ()
        let snap = SettledHelpers.ingestFixture (SettledHelpers.fixture "combat_magic_live.ndjson")
        let report = SettledDivergence.evaluate snap Set.empty
        Assert.False(SettledHelpers.hasCode report "start_as_harm-8")
        Assert.Empty(report.Actionable |> List.filter (fun i -> i.Code.StartsWith("start_as_harm-")))

    [<Fact>]
    let ``unknown Kind is an actionable gap`` () =
        EntityRegistry.reset ()

        let snap =
            { AnalyticsSnapshot.empty with
                ChatMessages = [ SettledHelpers.chat "Mode 0x24" 0x24 "Alice" 0x17 ] }

        let report = SettledDivergence.evaluate snap Set.empty
        Assert.True(SettledHelpers.hasCode report "unknown_kind-0x24")
        Assert.False(SettledDivergence.printReport report None)

    [<Fact>]
    let ``known yell Kind is not a gap`` () =
        EntityRegistry.reset ()
        let snap = SettledHelpers.ingestFixture (SettledHelpers.fixture "chat_yell.ndjson")
        let report = SettledDivergence.evaluate snap Set.empty
        Assert.Empty(report.Actionable |> List.filter (fun i -> i.Code.StartsWith("unknown_kind-")))

    [<Fact>]
    let ``start classified as Harm is decode corruption`` () =
        EntityRegistry.reset ()

        let snap =
            { AnalyticsSnapshot.empty with
                Interactions = [ SettledHelpers.interaction 8 0 InteractionType.Harm "cmd-8" ] }

        let report = SettledDivergence.evaluate snap Set.empty
        Assert.True(SettledHelpers.hasCode report "start_as_harm-8")

    [<Fact>]
    let ``fourcc used as spell name is decode corruption`` () =
        EntityRegistry.reset ()
        let fourcc = 1752654179

        let snap =
            { AnalyticsSnapshot.empty with
                Interactions =
                    [ SettledHelpers.interaction 8 0 InteractionType.Unknown $"spell-{fourcc}" ] }

        let report = SettledDivergence.evaluate snap Set.empty
        Assert.True(SettledHelpers.hasCode report $"fourcc_as_spell-{fourcc}")

    [<Fact>]
    let ``unclassified cmd 4 messageId is a gap; classified 7 is not`` () =
        EntityRegistry.reset ()
        Assert.False(SettledDivergence.isMessageClassified 99999)
        Assert.True(SettledDivergence.isMessageClassified MsgBasicCatalog.MagicRecoversHP)

        let unclassified =
            { AnalyticsSnapshot.empty with
                Interactions = [ SettledHelpers.interaction 4 99999 InteractionType.Harm "Cure" ] }

        let classified =
            { AnalyticsSnapshot.empty with
                Interactions =
                    [ SettledHelpers.interaction 4 MsgBasicCatalog.MagicRecoversHP InteractionType.Aid "Cure" ] }

        Assert.True(SettledHelpers.hasCode (SettledDivergence.evaluate unclassified Set.empty) "unclassified_message-99999")
        Assert.False(SettledHelpers.hasCode (SettledDivergence.evaluate classified Set.empty) "unclassified_message-7")

    [<Fact>]
    let ``scoped prove ignores leftover codes`` () =
        EntityRegistry.reset ()

        let both =
            { AnalyticsSnapshot.empty with
                Interactions =
                    [ SettledHelpers.interaction 4 99999 InteractionType.Harm "A"
                      SettledHelpers.interaction 4 88888 InteractionType.Harm "B" ] }

        let leftover =
            { AnalyticsSnapshot.empty with
                Interactions = [ SettledHelpers.interaction 4 88888 InteractionType.Harm "B" ] }

        let bothReport = SettledDivergence.evaluate both Set.empty
        Assert.True(SettledDivergence.targetedPresent bothReport "unclassified_message-99999")
        Assert.True(SettledDivergence.targetedPresent bothReport "unclassified_message")

        let leftoverReport = SettledDivergence.evaluate leftover Set.empty
        Assert.False(SettledDivergence.targetedPresent leftoverReport "unclassified_message-99999")
        Assert.True(SettledDivergence.printReport leftoverReport (Some "unclassified_message-99999"))
        Assert.False(SettledDivergence.printReport leftoverReport (Some "unclassified_message-88888"))

    [<Fact>]
    let ``unnamed entities are deferred and do not fail assert-settled`` () =
        EntityRegistry.reset ()

        let snap =
            { AnalyticsSnapshot.empty with
                Combatants =
                    [ { Id = 9u
                        Name = "Entity 9"
                        Kind = EntityKind.Mob
                        Job = ""
                        PlayerInfo = None } ] }

        let report = SettledDivergence.evaluate snap Set.empty
        Assert.True(SettledHelpers.hasCode report "unnamed_entities")
        Assert.Empty(report.Actionable)
        Assert.True(SettledDivergence.printReport report None)

    [<Fact>]
    let ``skip-code drops a family from the report`` () =
        EntityRegistry.reset ()

        let snap =
            { AnalyticsSnapshot.empty with
                ChatMessages = [ SettledHelpers.chat "Mode 0x24" 0x24 "Alice" 0x17 ] }

        let report = SettledDivergence.evaluate snap (Set.ofList [ "unknown_kind" ])
        Assert.Empty(report.Actionable)

    [<Fact>]
    let ``isLikelyFourcc detects cawh and rejects spell id 1`` () =
        Assert.True(SettledDivergence.isLikelyFourcc 1752654179)
        Assert.False(SettledDivergence.isLikelyFourcc 1)
        Assert.False(SettledDivergence.isLikelyFourcc 0)

[<Collection("EntityRegistry")>]
module SelfChatSettleTests =
    [<Fact>]
    let ``nameless self-chat stays Unknown until local name exists and is not a halt`` () =
        EntityRegistry.reset ()
        InteractionBuilder.reset ()
        let store = SessionStore.create ()
        let data = Fixtures.chatPacket "" "hello from self" 0x0Duy
        SettledHelpers.ingestPacket store 1 0x0017us PacketDirection.Incoming data
        let snap = SessionStore.snapshot store
        Assert.True(EntityRegistry.localPlayerName().IsNone)

        Assert.Contains(
            snap.ChatMessages,
            fun c -> c.Mode = "Say" && c.Message = "hello from self" && ChatIngest.isPlaceholderSpeaker c.Speaker
        )

        let report = SettledDivergence.evaluate snap Set.empty
        Assert.False(SettledHelpers.hasCode report "nameless_self_unnamed")

    [<Fact>]
    let ``snapshot backfills nameless self-chat after 0x00A login name`` () =
        EntityRegistry.reset ()
        InteractionBuilder.reset ()
        let store = SessionStore.create ()
        SettledHelpers.ingestPacket store 1 0x0017us PacketDirection.Incoming (Fixtures.chatPacket "" "hello from self" 0x0Duy)
        SettledHelpers.ingestPacket store 2 0x000Aus PacketDirection.Incoming (Fixtures.loginPacket "Porobururu" 0x950F5u)
        let snap = SessionStore.snapshot store
        Assert.Equal(Some "Porobururu", EntityRegistry.localPlayerName ())

        Assert.Contains(
            snap.ChatMessages,
            fun c -> c.Mode = "Say" && c.Speaker = "Porobururu" && c.Message = "hello from self"
        )

        let report = SettledDivergence.evaluate snap Set.empty
        Assert.False(SettledHelpers.hasCode report "nameless_self_unnamed")

    [<Fact>]
    let ``snapshot backfills nameless self-chat after 0x00DF plus 0x00D`` () =
        EntityRegistry.reset ()
        InteractionBuilder.reset ()
        let store = SessionStore.create ()
        SettledHelpers.ingestPacket store 1 0x0017us PacketDirection.Incoming (Fixtures.chatPacket "" "hello from self" 0x0Duy)
        InteractionTestHelpers.registerLocalPlayer 0x950F5u "Porobururu"
        SessionStore.ingest store (SettledHelpers.mkEvt 2 0x00DFus PacketDirection.Incoming (Fixtures.groupAttrPacket 0x950F5u 140us)) DecoderResult.empty
        let snap = SessionStore.snapshot store
        Assert.Equal(Some "Porobururu", EntityRegistry.localPlayerName ())

        Assert.Contains(
            snap.ChatMessages,
            fun c -> c.Speaker = "Porobururu" && c.Message = "hello from self"
        )

    [<Fact>]
    let ``session header name resolves nameless self-chat without waiting for 0x00A`` () =
        EntityRegistry.reset ()
        InteractionBuilder.reset ()
        EntityRegistry.registerLocalPlayerName "Porobururu"
        let store = SessionStore.create ()
        SettledHelpers.ingestPacket store 1 0x0017us PacketDirection.Incoming (Fixtures.chatPacket "" "hello from self" 0x0Duy)
        let snap = SessionStore.snapshot store
        Assert.Equal(Some "Porobururu", EntityRegistry.localPlayerName ())

        Assert.Contains(
            snap.ChatMessages,
            fun c -> c.Speaker = "Porobururu" && c.Message = "hello from self"
        )

    [<Fact>]
    let ``nameless self still Unknown after name is known is a settled gap`` () =
        EntityRegistry.reset ()
        EntityRegistry.registerLocalPlayerName "Porobururu"

        let snap =
            { AnalyticsSnapshot.empty with
                ChatMessages = [ SettledHelpers.chat "Say" 0x0D "Unknown" 0x17 ] }

        let report = SettledDivergence.evaluate snap Set.empty
        Assert.True(SettledHelpers.hasCode report "nameless_self_unnamed")
