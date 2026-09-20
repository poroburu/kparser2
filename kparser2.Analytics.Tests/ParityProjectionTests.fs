namespace kparser2.Analytics.Tests

open Xunit
open kparser2.Analytics

module ParityProjectionTests =
    [<Fact>]
    let ``interaction projection is name keyed and preserves combat semantics`` () =
        let snap =
            { AnalyticsSnapshot.empty with
                Interactions =
                    [ { Id = 9
                        BattleId = Some 2
                        TimestampMs = 123L
                        InteractionType = InteractionType.Harm
                        HarmType = Some HarmType.Melee
                        AidType = None
                        Category = InteractionCategory.Melee
                        DamageModifier = DamageModifier.Normal
                        ActorId = 10u
                        TargetId = 20u
                        ActorName = "Alice"
                        TargetName = "Crab"
                        ActionName = "Attack"
                        Value = 128
                        Success = "hit"
                        CommandNo = 1
                        SpellId = None
                        MessageId = 0x14
                        IsProc = false
                        ProcValue = 0
                        IsLocalPlayerActor = true
                        IsLocalPlayerTarget = false
                        SourcePacketId = Some "0x28" } ] }

        let row = ParityProjection.interactions snap |> List.head

        Assert.Equal("Alice", row.actorName)
        Assert.Equal("Crab", row.targetName)
        Assert.Equal("Harm", row.interactionType)
        Assert.Equal("Melee", row.actionType)
        Assert.Equal(128, row.amount)
        Assert.Equal("hit", row.success)

    [<Fact>]
    let ``chat projection excludes outgoing rows and gives system rows a speaker`` () =
        let snap =
            { AnalyticsSnapshot.empty with
                ChatMessages =
                    [ { TimestampMs = 1L
                        Mode = "Say"
                        ModeId = 13
                        IsGm = false
                        Speaker = "Alice"
                        Message = "hello"
                        PacketId = 0x17
                        Direction = "incoming"
                        IsLocalPlayer = false
                        TargetName = None }
                      { TimestampMs = 2L
                        Mode = "Say"
                        ModeId = 13
                        IsGm = false
                        Speaker = "Alice"
                        Message = "outgoing"
                        PacketId = 0x17
                        Direction = "outgoing"
                        IsLocalPlayer = true
                        TargetName = None }
                      { TimestampMs = 3L
                        Mode = "System"
                        ModeId = 0
                        IsGm = false
                        Speaker = ""
                        Message = "Welcome"
                        PacketId = 0x29
                        Direction = "incoming"
                        IsLocalPlayer = false
                        TargetName = None } ] }

        let rows = ParityProjection.chat snap

        Assert.Equal(2, rows.Length)
        Assert.Equal("Alice", rows.[0].speaker)
        Assert.Equal("System", rows.[1].speaker)
