namespace kparser2.Analytics.Tests

open Xunit
open kparser2.Analytics

/// Maps active kparser TestParser scenarios (+ empty regions) to 0x28 effect fields.
type ParseCodesParityTests() =
    static member private row
        (name: string)
        (commandNo: int)
        (messageId: int)
        (miss: int)
        (value: int)
        (interaction: InteractionType)
        (harm: HarmType option)
        (aid: AidType option)
        (success: string)
        =
        [| box name
           box commandNo
           box messageId
           box miss
           box value
           box interaction
           box harm
           box aid
           box success |]

    static member ParityRows =
        seq {
            // Melee hits
            yield ParseCodesParityTests.row "TestPlayerHitMob" 1 0x14 0 128 InteractionType.Harm (Some HarmType.Melee) None "hit"
            yield ParseCodesParityTests.row "TestMobHitPlayer" 1 0x1C 0 170 InteractionType.Harm (Some HarmType.Melee) None "hit"
            yield ParseCodesParityTests.row "TestPartyHitMob" 1 0x19 0 168 InteractionType.Harm (Some HarmType.Melee) None "hit"
            yield ParseCodesParityTests.row "TestMobHitParty" 1 0x20 0 120 InteractionType.Harm (Some HarmType.Melee) None "hit"
            // Ranged hits
            yield ParseCodesParityTests.row "TestPartyRHitMob" 2 0x19 0 247 InteractionType.Harm (Some HarmType.Ranged) None "hit"
            // Melee misses
            yield ParseCodesParityTests.row "TestPlayerMissMob" 1 0x15 1 0 InteractionType.Harm (Some HarmType.Melee) None "miss"
            yield ParseCodesParityTests.row "TestMobMissPlayer" 1 0x1D 1 0 InteractionType.Harm (Some HarmType.Melee) None "miss"
            yield ParseCodesParityTests.row "TestPartyMissMob" 1 0x1A 1 0 InteractionType.Harm (Some HarmType.Melee) None "miss"
            yield ParseCodesParityTests.row "TestMobMissParty" 1 0x21 1 0 InteractionType.Harm (Some HarmType.Melee) None "miss"
            // Ranged misses
            yield ParseCodesParityTests.row "TestPlayerRMissMob" 2 0x15 1 0 InteractionType.Harm (Some HarmType.Ranged) None "miss"
            // Blocks
            yield ParseCodesParityTests.row "TestPlayerBlink" 1 0x1D 0 0 InteractionType.Harm (Some HarmType.Melee) None "shadow-absorb"
            yield ParseCodesParityTests.row "TestMobBlink" 1 0x1A 0 0 InteractionType.Harm (Some HarmType.Melee) None "shadow-absorb"
            yield ParseCodesParityTests.row "TestPlayerParry" 1 0x1D 3 0 InteractionType.Harm (Some HarmType.Melee) None "parry"
            yield ParseCodesParityTests.row "TestPlayerAnticipate" 1 0x69 0 0 InteractionType.Harm (Some HarmType.Melee) None "anticipate"
            // Counters
            yield ParseCodesParityTests.row "TestPlayerCounter" 1 0x14 0 56 InteractionType.Harm (Some HarmType.Melee) None "hit"
            yield ParseCodesParityTests.row "TestPartyCounter" 1 0x19 0 27 InteractionType.Harm (Some HarmType.Melee) None "hit"
            yield ParseCodesParityTests.row "TestMobCounterPlayer" 1 0x1C 0 148 InteractionType.Harm (Some HarmType.Melee) None "hit"
            yield ParseCodesParityTests.row "TestMobCounterParty" 1 0x20 0 71 InteractionType.Harm (Some HarmType.Melee) None "hit"
            // Retaliate
            yield ParseCodesParityTests.row "TestPartyRetaliateHit" 1 0x19 0 52 InteractionType.Harm (Some HarmType.Melee) None "hit"
            // Failures
            yield ParseCodesParityTests.row "FailSelfBuff" 4 0x44 0 0 InteractionType.Aid None (Some AidType.Enhance) "no-effect"
            yield ParseCodesParityTests.row "FailDebuff" 4 0x3B 0 0 InteractionType.Aid None (Some AidType.Enhance) "no-effect"
            yield ParseCodesParityTests.row "FailParalyna" 4 0x44 0 0 InteractionType.Aid None (Some AidType.Enhance) "no-effect"
            // Additional effects
            yield ParseCodesParityTests.row "TPDrainNotCharmedMob_hit" 1 0xA3 0 90 InteractionType.Harm (Some HarmType.Melee) None "hit"
            yield ParseCodesParityTests.row "TPDrainNotCharmedMob_drain" 1 0xBB 0 3 InteractionType.Harm (Some HarmType.Other) None "hit"
            // Empty regions — cover / magic / skillchains / enfeebling / buffing / curing / JAs / deaths / preparing / drains / experience / loot
            yield ParseCodesParityTests.row "region_magic_damage" 4 0xA5 0 250 InteractionType.Harm (Some HarmType.Spell) None "hit"
            yield ParseCodesParityTests.row "region_enfeeble" 4 0x39 0 0 InteractionType.Harm (Some HarmType.Enfeeble) None "hit"
            yield ParseCodesParityTests.row "region_buff" 4 0x38 0 0 InteractionType.Aid None (Some AidType.Enhance) "hit"
            yield ParseCodesParityTests.row "region_curing" 4 0x17 0 350 InteractionType.Aid None (Some AidType.Recovery) "hit"
            // Live 0x28 BattleResult.message (xi.msg.basic), not kparser chatline ParseCodes.
            yield ParseCodesParityTests.row "live_magic_recovers_hp" 4 7 0 0 InteractionType.Aid None (Some AidType.Recovery) "hit"
            yield ParseCodesParityTests.row "live_magic_gain_effect" 4 230 0 0 InteractionType.Aid None (Some AidType.Enhance) "hit"
            yield ParseCodesParityTests.row "live_magic_no_effect" 4 75 0 0 InteractionType.Aid None (Some AidType.Enhance) "no-effect"
            yield ParseCodesParityTests.row "live_magic_drain_mp" 4 228 0 50 InteractionType.Harm (Some HarmType.Spell) None "hit"
            yield ParseCodesParityTests.row "live_magic_drain_hp" 4 227 0 40 InteractionType.Harm (Some HarmType.Spell) None "hit"
            yield ParseCodesParityTests.row "live_skill_drain_mp" 13 225 0 30 InteractionType.Harm (Some HarmType.Ability) None "hit"
            yield ParseCodesParityTests.row "region_ja" 13 0x68 0 80 InteractionType.Harm (Some HarmType.Ability) None "hit"
            yield ParseCodesParityTests.row "region_death_player" 0 0x26 0 0 InteractionType.Death None None "hit"
            yield ParseCodesParityTests.row "region_prepare_spell" 4 0x32 0 0 InteractionType.Unknown None None "hit"
            yield ParseCodesParityTests.row "region_drain" 4 0x16 0 50 InteractionType.Harm (Some HarmType.Other) None "hit"
            yield ParseCodesParityTests.row "region_cover" 1 0x6D 1 0 InteractionType.Harm (Some HarmType.Melee) None "miss"
            yield ParseCodesParityTests.row "region_skillchain_followup" 1 0x28 0 100 InteractionType.Harm (Some HarmType.Melee) None "hit"
            yield ParseCodesParityTests.row "FailRemoveStatus" 4 0x44 0 0 InteractionType.Aid None (Some AidType.Enhance) "no-effect"
            yield ParseCodesParityTests.row "region_loot_found" 0 0x79 0 0 InteractionType.Aid None (Some AidType.Item) "hit"
        }

    [<Theory>]
    [<MemberData(nameof (ParseCodesParityTests.ParityRows))>]
    member _.``kparser parity classifyActionEffect``
        (
            name: string,
            commandNo: int,
            messageId: int,
            miss: int,
            value: int,
            expectedInteraction: InteractionType,
            expectedHarm: HarmType option,
            expectedAid: AidType option,
            expectedSuccess: string
        )
        =
        let interactionType, harmType, aidType =
            BattleMessageCatalog.classifyActionEffect commandNo messageId miss value

        let success = BattleMessageCatalog.successLabelForEffect messageId miss value

        Assert.Equal(expectedInteraction, interactionType)
        Assert.Equal(expectedHarm, harmType)
        Assert.Equal(expectedAid, aidType)
        Assert.Equal(expectedSuccess, success)
        Assert.False(System.String.IsNullOrWhiteSpace name)

    [<Fact>]
    member _.``parity row count covers kparser active scenarios`` () =
        let rows = ParseCodesParityTests.ParityRows |> Seq.length
        Assert.True(rows >= 34, $"Expected at least 34 parity rows, got {rows}")

    [<Fact>]
    member _.``0x28 magic start cmd 8 is not weaponskill damage`` () =
        // Live HorizonXI: cmd_no 8 + cmd_arg `cabl` (1818386787) + message 327 + value 549.
        // XiPackets: Magic (Start). kparser chatlines never see this packet.
        let interactionType, harmType, aidType =
            BattleMessageCatalog.classifyActionEffect 8 327 0 549

        Assert.True(BattleMessageCatalog.isActionStartCommand 8)
        Assert.Equal(InteractionType.Unknown, interactionType)
        Assert.Equal(None, harmType)
        Assert.Equal(None, aidType)

    [<Fact>]
    member _.``0x28 white-magic start fourcc is not an action id`` () =
        // Live: cmd_arg 1752654179 = `cawh` (XiPackets white-magic start).
        let interactionType, _, _ =
            BattleMessageCatalog.classifyActionEffect 8 327 0 43

        Assert.Equal(InteractionType.Unknown, interactionType)
