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
        Assert.False(InteractionClassification.isHpDamage drain)
        Assert.True(InteractionClassification.isHpDamage hit)

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
    let ``magic start cmd 8 emits a preparing interaction`` () =
        InteractionTestHelpers.resetEntities ()
        InteractionTestHelpers.registerLocalPlayer playerId "Motenten"

        match Battle0x28.decode (Fixtures.combatActionPacket playerId playerId 8 549 327 0) with
        | None -> failwith "Expected battle action decode"
        | Some action ->
            let rows = InteractionBuilder.fromCombatAction 1000L None action
            let i = Assert.Single rows
            Assert.Equal(InteractionType.Unknown, i.InteractionType)
            Assert.Equal("preparing", i.Success)
            Assert.Equal("Magic", i.ActionName)
            Assert.Equal(8, i.CommandNo)
            Assert.Equal(0, i.Value)

    [<Fact>]
    let ``react spikes emit reversed harm from the target`` () =
        InteractionTestHelpers.resetEntities ()
        InteractionTestHelpers.registerLocalPlayer playerId "Motenten"
        InteractionTestHelpers.registerMob mobId "Spiked Crab"

        match Battle0x28.decode (Fixtures.combatActionPacketWithReact playerId mobId 1 90 0x14 0 33 44) with
        | None -> failwith "Expected battle action decode"
        | Some action ->
            let rows = InteractionBuilder.fromCombatAction 1000L None action
            Assert.Equal(2, rows.Length)
            let hit = rows |> List.find (fun i -> i.ActionName <> "Spikes")
            let spike = rows |> List.find (fun i -> i.ActionName = "Spikes")
            Assert.Equal(InteractionType.Harm, hit.InteractionType)
            Assert.Equal(playerId, hit.ActorId)
            Assert.Equal(mobId, hit.TargetId)
            Assert.Equal(InteractionType.Harm, spike.InteractionType)
            Assert.Equal(Some HarmType.Other, spike.HarmType)
            Assert.Equal(mobId, spike.ActorId)
            Assert.Equal(playerId, spike.TargetId)
            Assert.Equal(33, spike.Value)
            Assert.Equal(44, spike.MessageId)

    [<Fact>]
    let ``hp drain keeps harm and dual-emits caster recovery`` () =
        InteractionTestHelpers.resetEntities ()
        InteractionTestHelpers.registerLocalPlayer playerId "Motenten"
        InteractionTestHelpers.registerMob mobId "Crab"

        match Battle0x28.decode (Fixtures.combatActionPacketEx playerId mobId 4 245u 40 227 0) with
        | None -> failwith "Expected battle action decode"
        | Some action ->
            let rows = InteractionBuilder.fromCombatAction 1000L None action
            Assert.Equal(2, rows.Length)
            let harm = rows |> List.find (fun i -> i.InteractionType = InteractionType.Harm)
            let aid = rows |> List.find (fun i -> i.InteractionType = InteractionType.Aid)
            Assert.Equal(Some HarmType.Spell, harm.HarmType)
            Assert.Equal(40, harm.Value)
            Assert.Equal(mobId, harm.TargetId)
            Assert.Equal(Some AidType.Recovery, aid.AidType)
            Assert.Equal(playerId, aid.ActorId)
            Assert.Equal(playerId, aid.TargetId)
            Assert.Equal(40, aid.Value)

    [<Fact>]
    let ``mp drain keeps harm and does not dual-emit caster recovery`` () =
        InteractionTestHelpers.resetEntities ()
        InteractionTestHelpers.registerLocalPlayer playerId "Motenten"
        InteractionTestHelpers.registerMob mobId "Crab"

        match Battle0x28.decode (Fixtures.combatActionPacketEx playerId mobId 4 247u 32 228 0) with
        | None -> failwith "Expected battle action decode"
        | Some action ->
            let rows = InteractionBuilder.fromCombatAction 1000L None action
            let harm = Assert.Single rows
            Assert.Equal(InteractionType.Harm, harm.InteractionType)
            Assert.Equal(Some HarmType.Spell, harm.HarmType)
            Assert.Equal(32, harm.Value)
            Assert.Equal(228, harm.MessageId)
            Assert.Equal(mobId, harm.TargetId)
            Assert.False(InteractionClassification.isHpDamage harm)

    [<Fact>]
    let ``item finish cmd 5 records an item use`` () =
        InteractionTestHelpers.resetEntities ()
        InteractionBuilder.reset ()
        InteractionTestHelpers.registerLocalPlayer playerId "Motenten"
        let store = SessionStore.create ()
        let data = Fixtures.combatActionPacketEx playerId playerId 5 4112u 0 0x51 0
        let evt = InteractionTestHelpers.packetEvent 0x0028us data
        SessionStore.ingest store evt (DecoderRegistry.decode evt)
        let snap = SessionStore.snapshot store
        let useRow = Assert.Single snap.ItemUses
        Assert.Equal(4112, useRow.ItemId)
        Assert.Equal("potion", useRow.ItemName)
        Assert.Equal(playerId, useRow.ActorId)
        Assert.True(snap.Interactions |> List.exists (fun i -> i.AidType = Some AidType.Item && i.ActionName = "potion"))

    [<Fact>]
    let ``incoming 0x0037 server status is not recorded as an item use`` () =
        InteractionTestHelpers.resetEntities ()
        InteractionBuilder.reset ()
        let store = SessionStore.create ()
        let data = Array.zeroCreate<byte> 32
        data.[0] <- 0x20uy
        data.[2] <- 0x37uy
        let evt = InteractionTestHelpers.packetEvent 0x0037us data
        SessionStore.ingest store evt (DecoderRegistry.decode evt)
        Assert.Empty (SessionStore.snapshot store).ItemUses
