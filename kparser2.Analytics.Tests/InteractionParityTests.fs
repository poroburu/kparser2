namespace kparser2.Analytics.Tests

open Xunit
open kparser2.Analytics
open kparser2.Decoders
open kparser2.Decoders.Tests

[<Collection("EntityRegistry")>]
module InteractionParityTests =
    let private playerId = 0x268Bu
    let private mobId = 0x2C8Bu
    let private partyId = 0x26CAu

    [<Fact>]
    let ``TestPlayerHitMob builds harm melee interaction`` () =
        InteractionTestHelpers.resetEntities ()
        InteractionTestHelpers.registerLocalPlayer playerId "Motenten"
        InteractionTestHelpers.registerMob mobId "Greater Colibri"

        let i =
            InteractionTestHelpers.buildInteraction playerId mobId 1 0x14 0 128

        Assert.Equal(InteractionType.Harm, i.InteractionType)
        Assert.Equal(Some HarmType.Melee, i.HarmType)
        Assert.Equal("Motenten", i.ActorName)
        Assert.Equal("Greater Colibri", i.TargetName)
        Assert.Equal(128, i.Value)
        Assert.Equal("hit", i.Success)

    [<Fact>]
    let ``TestMobHitPlayer builds harm melee interaction`` () =
        InteractionTestHelpers.resetEntities ()
        InteractionTestHelpers.registerLocalPlayer playerId "Motenten"
        InteractionTestHelpers.registerMob mobId "Greater Colibri"

        let i =
            InteractionTestHelpers.buildInteraction mobId playerId 1 0x1C 0 170

        Assert.Equal(InteractionType.Harm, i.InteractionType)
        Assert.Equal("Greater Colibri", i.ActorName)
        Assert.Equal("Motenten", i.TargetName)
        Assert.Equal(170, i.Value)

    [<Fact>]
    let ``TestPartyHitMob builds harm melee interaction`` () =
        InteractionTestHelpers.resetEntities ()
        InteractionTestHelpers.registerPartyMember partyId "Lans"
        InteractionTestHelpers.registerMob mobId "Greater Colibri"

        let i =
            InteractionTestHelpers.buildInteraction partyId mobId 1 0x19 0 168

        Assert.Equal(InteractionType.Harm, i.InteractionType)
        Assert.Equal("Lans", i.ActorName)
        Assert.Equal("Greater Colibri", i.TargetName)

    [<Fact>]
    let ``TestPlayerMissMob builds miss interaction`` () =
        InteractionTestHelpers.resetEntities ()
        InteractionTestHelpers.registerLocalPlayer playerId "Motenten"
        InteractionTestHelpers.registerMob mobId "Greater Colibri"

        let i =
            InteractionTestHelpers.buildInteraction playerId mobId 1 0x15 1 0

        Assert.Equal(InteractionType.Harm, i.InteractionType)
        Assert.Equal(0, i.Value)
        Assert.Equal("miss", i.Success)

    [<Fact>]
    let ``TestPlayerBlink builds shadow absorb interaction`` () =
        InteractionTestHelpers.resetEntities ()
        InteractionTestHelpers.registerLocalPlayer playerId "Motenten"

        let i =
            InteractionTestHelpers.buildInteraction mobId playerId 1 0x1D 0 0

        Assert.Equal(InteractionType.Harm, i.InteractionType)
        Assert.Equal("shadow-absorb", i.Success)

    [<Fact>]
    let ``TestPlayerParry builds parry interaction`` () =
        InteractionTestHelpers.resetEntities ()
        InteractionTestHelpers.registerLocalPlayer playerId "Motenten"

        let i =
            InteractionTestHelpers.buildInteraction mobId playerId 1 0x1D 3 0

        Assert.Equal("parry", i.Success)

    [<Fact>]
    let ``FailSelfBuff builds no-effect enhance interaction`` () =
        InteractionTestHelpers.resetEntities ()
        InteractionTestHelpers.registerLocalPlayer playerId "Aurun"

        let i =
            InteractionTestHelpers.buildInteraction playerId playerId 4 0x44 0 0

        Assert.Equal(InteractionType.Aid, i.InteractionType)
        Assert.Equal(Some AidType.Enhance, i.AidType)
        Assert.Equal("no-effect", i.Success)

    [<Fact>]
    let ``TPDrainNotCharmedMob builds additional effect harm`` () =
        InteractionTestHelpers.resetEntities ()
        InteractionTestHelpers.registerLocalPlayer playerId "Vixx"
        InteractionTestHelpers.registerMob mobId "Vanguard Enchanter"

        let hit =
            InteractionTestHelpers.buildInteraction playerId mobId 1 0xA3 0 90

        let drain =
            InteractionTestHelpers.buildInteraction playerId mobId 1 0xBB 0 3

        Assert.Equal(InteractionType.Harm, hit.InteractionType)
        Assert.Equal(InteractionType.Harm, drain.InteractionType)
        Assert.Equal(Some HarmType.Other, drain.HarmType)
        Assert.Equal(3, drain.Value)

    [<Fact>]
    let ``region_enfeeble builds enfeeble interaction`` () =
        InteractionTestHelpers.resetEntities ()
        InteractionTestHelpers.registerLocalPlayer playerId "Motenten"
        InteractionTestHelpers.registerMob mobId "Colibri"

        let i =
            InteractionTestHelpers.buildInteraction playerId mobId 4 0x39 0 0

        Assert.Equal(InteractionType.Harm, i.InteractionType)
        Assert.Equal(Some HarmType.Enfeeble, i.HarmType)
        Assert.Equal(InteractionCategory.Enfeeble, i.Category)

    [<Fact>]
    let ``region_buff builds enhance interaction`` () =
        InteractionTestHelpers.resetEntities ()
        InteractionTestHelpers.registerLocalPlayer playerId "Motenten"

        let i =
            InteractionTestHelpers.buildInteraction playerId playerId 4 0x38 0 0

        Assert.Equal(InteractionType.Aid, i.InteractionType)
        Assert.Equal(Some AidType.Enhance, i.AidType)
        Assert.Equal(InteractionCategory.Enhance, i.Category)

    [<Fact>]
    let ``live MAGIC_RECOVERS_HP cmd 4 is recovery even at zero HP`` () =
        InteractionTestHelpers.resetEntities ()
        InteractionTestHelpers.registerLocalPlayer playerId "Motenten"

        match Battle0x28.decode (Fixtures.combatActionPacketEx playerId playerId 4 1u 0 7 0) with
        | None -> failwith "Expected battle action decode"
        | Some action ->
            let rows = InteractionBuilder.fromCombatAction 1000L None action
            let i = List.head rows
            Assert.Equal(InteractionType.Aid, i.InteractionType)
            Assert.Equal(Some AidType.Recovery, i.AidType)
            Assert.Equal("Cure", i.ActionName)
            Assert.Equal(0, i.Value)

    [<Fact>]
    let ``live MAGIC_GAIN_EFFECT cmd 4 names Protect and Blaze Spikes`` () =
        InteractionTestHelpers.resetEntities ()
        InteractionTestHelpers.registerLocalPlayer playerId "Motenten"

        let protect =
            match Battle0x28.decode (Fixtures.combatActionPacketEx playerId playerId 4 43u 0 230 0) with
            | None -> failwith "Expected Protect decode"
            | Some action -> InteractionBuilder.fromCombatAction 1000L None action |> List.head

        let spikes =
            match Battle0x28.decode (Fixtures.combatActionPacketEx playerId playerId 4 249u 0 230 0) with
            | None -> failwith "Expected Blaze Spikes decode"
            | Some action -> InteractionBuilder.fromCombatAction 1000L None action |> List.head

        Assert.Equal(InteractionType.Aid, protect.InteractionType)
        Assert.Equal(Some AidType.Enhance, protect.AidType)
        Assert.Equal("Protect", protect.ActionName)
        Assert.True(BattleMessageCatalog.isDefensiveBuff protect.ActionName)
        Assert.Equal("Blaze Spikes", spikes.ActionName)
        Assert.True(BattleMessageCatalog.isDefensiveBuff spikes.ActionName)

    [<Fact>]
    let ``magic start cmd 8 does not emit an interaction`` () =
        InteractionTestHelpers.resetEntities ()
        InteractionTestHelpers.registerLocalPlayer playerId "Motenten"

        match Battle0x28.decode (Fixtures.combatActionPacket playerId playerId 8 549 327 0) with
        | None -> failwith "Expected battle action decode"
        | Some action ->
            let rows = InteractionBuilder.fromCombatAction 1000L None action
            Assert.True(List.isEmpty rows)
