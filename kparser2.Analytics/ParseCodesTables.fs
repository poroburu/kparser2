namespace kparser2.Analytics

/// ParseCodes message IDs (0x14-0xBC) from legacy kparser — used for 0x28 effect MessageId only.
module ParseCodesTables =
    let private interactionEntries =
        [|
            0x14, InteractionType.Harm
            0x15, InteractionType.Harm
            0x16, InteractionType.Harm
            0x17, InteractionType.Aid
            0x18, InteractionType.Aid
            0x19, InteractionType.Harm
            0x1A, InteractionType.Harm
            0x1B, InteractionType.Harm
            0x1C, InteractionType.Harm
            0x1D, InteractionType.Harm
            0x1E, InteractionType.Aid
            0x1F, InteractionType.Aid
            0x20, InteractionType.Harm
            0x21, InteractionType.Harm
            0x22, InteractionType.Aid
            0x23, InteractionType.Aid
            0x24, InteractionType.Death
            0x25, InteractionType.Death
            0x26, InteractionType.Death
            0x27, InteractionType.Death
            0x28, InteractionType.Harm
            0x29, InteractionType.Harm
            0x2A, InteractionType.Aid
            0x2B, InteractionType.Aid
            0x2C, InteractionType.Death
            0x38, InteractionType.Aid
            0x39, InteractionType.Harm
            0x3B, InteractionType.Aid
            0x3C, InteractionType.Aid
            0x3D, InteractionType.Harm
            0x40, InteractionType.Aid
            0x41, InteractionType.Harm
            0x43, InteractionType.Harm
            0x44, InteractionType.Aid
            0x45, InteractionType.Aid
            0x69, InteractionType.Harm
            0x51, InteractionType.Aid
            0x55, InteractionType.Aid
            0x5A, InteractionType.Aid
            0x5B, InteractionType.Aid
            0x65, InteractionType.Aid
            0x66, InteractionType.Harm
            0x68, InteractionType.Harm
            0x6A, InteractionType.Aid
            0x6B, InteractionType.Harm
            0x6D, InteractionType.Harm
            0x6F, InteractionType.Aid
            0x70, InteractionType.Harm
            0x72, InteractionType.Harm
            0x79, InteractionType.Aid
            0x7A, InteractionType.Harm
            0x8D, InteractionType.Harm
            0xA2, InteractionType.Aid
            0xA3, InteractionType.Harm
            0xA4, InteractionType.Harm
            0xA5, InteractionType.Harm
            0xA6, InteractionType.Death
            0xA7, InteractionType.Death
            0xAB, InteractionType.Aid
            0xAE, InteractionType.Harm
            0xAF, InteractionType.Aid
            0xB5, InteractionType.Harm
            0xB6, InteractionType.Harm
            0xB7, InteractionType.Aid
            0xB9, InteractionType.Harm
            0xBA, InteractionType.Harm
            0xBB, InteractionType.Aid
            0xBC, InteractionType.Aid
        |]

    let private harmEntries =
        [|
            0x14, HarmType.Melee
            0x15, HarmType.Melee
            0x16, HarmType.Other
            0x19, HarmType.Melee
            0x1A, HarmType.Melee
            0x1B, HarmType.Other
            0x1C, HarmType.Melee
            0x1D, HarmType.Melee
            0x20, HarmType.Melee
            0x21, HarmType.Melee
            0x28, HarmType.Melee
            0x29, HarmType.Melee
            0x39, HarmType.Enfeeble
            0x3D, HarmType.Enfeeble
            0x41, HarmType.Enfeeble
            0x43, HarmType.Enfeeble
            0x44, HarmType.Enfeeble
            0x69, HarmType.Melee
            0xBB, HarmType.Other
            0x66, HarmType.Enfeeble
            0x68, HarmType.Ability
            0x6B, HarmType.Enfeeble
            0x6D, HarmType.Melee
            0x70, HarmType.Enfeeble
            0x72, HarmType.Weaponskill
            0x7A, HarmType.Other
            0x8D, HarmType.Other
            0xA3, HarmType.Melee
            0xA4, HarmType.Melee
            0xA5, HarmType.Spell
            0xAE, HarmType.Enfeeble
            0xB5, HarmType.Other
            0xB6, HarmType.Enfeeble
            0xB9, HarmType.Spell
            0xBA, HarmType.Other
        |]

    let private aidEntries =
        [|
            0x17, AidType.Recovery
            0x18, AidType.Recovery
            0x1E, AidType.Recovery
            0x1F, AidType.Recovery
            0x22, AidType.Recovery
            0x23, AidType.Recovery
            0x2A, AidType.Recovery
            0x2B, AidType.Recovery
            0x38, AidType.Enhance
            0x3B, AidType.Enhance
            0x3C, AidType.Enhance
            0x44, AidType.Enhance
            0x45, AidType.Enhance
            0x51, AidType.Item
            0x55, AidType.Item
            0x5A, AidType.Item
            0x5B, AidType.Item
            0x65, AidType.Enhance
            0x6A, AidType.Enhance
            0x6F, AidType.Enhance
            0x79, AidType.Item
            0xA2, AidType.Recovery
            0xAB, AidType.Item
            0xAF, AidType.Enhance
            0xB7, AidType.Enhance
            0xBB, AidType.Recovery
            0xBC, AidType.Recovery
        |]

    let private alternateCodes =
        Map.ofList
            [ 0x38, [ 0x40; 0x6A; 0x3C; 0xAA; 0x45 ]
              0x3C, [ 0x45 ]
              0x40, [ 0x38; 0xAF; 0x6A; 0x3C; 0xAA; 0x45 ]
              0x6A, [ 0x38; 0x40; 0x3C ]
              0x39, [ 0x45; 0x3B; 0x3D; 0x3F; 0xB6 ]
              0x3D, [ 0x39; 0x3B; 0x45; 0x3F; 0xB6 ]
              0x1C, [ 0x14; 0x19; 0x6D; 0xB9 ]
              0x1E, [ 0x14; 0x19 ]
              0x22, [ 0x19 ]
              0x6D, [ 0x1C; 0xB9 ] ]

    let interactionTypeMap =
        interactionEntries |> Array.map (fun (k, v) -> k, v) |> Map.ofArray

    let harmTypeMap = harmEntries |> Array.map (fun (k, v) -> k, v) |> Map.ofArray

    let aidTypeMap = aidEntries |> Array.map (fun (k, v) -> k, v) |> Map.ofArray

    let tryInteractionType messageId =
        Map.tryFind messageId interactionTypeMap

    let tryHarmType messageId = Map.tryFind messageId harmTypeMap

    let tryAidType messageId = Map.tryFind messageId aidTypeMap

    let resolveAlternateMessageId messageId =
        if Map.containsKey messageId interactionTypeMap then
            messageId
        else
            alternateCodes
            |> Map.tryFind messageId
            |> Option.defaultValue []
            |> List.tryFind (fun alt -> Map.containsKey alt interactionTypeMap)
            |> Option.defaultValue messageId

/// LandSandBoat MsgBasic values for 0x29 GP_SERV_COMMAND_BATTLE_MESSAGE.
module MsgBasicCatalog =
    let ExperiencePointsGained = 8
    let MagicRecoversHP = 7
    let DefeatsTarget = 6
    let FallsToGround = 20
    let ExpChain = 253
    let AttackHits = 1
    let AttackMisses = 15
    let TargetRecoversHPSimple = 24
    let MagicDmg = 2
    let MagicNoEffect = 75
    let MagicFail = 114
    let MagicGainEffect = 230
    let MagicEnfeebIs = 236
    let MagicEnfeeb = 237
    // xi.msg.basic status region (LandSandBoat scripts/enum/msg.lua); live Horizon 0x29 used 206.
    let IsStatus = 203
    let IsNoLongerStatus = 204
    let GainsEffectOfStatus = 205
    let StatusWearsOff = 206

    /// 0x28 BattleResult.message uses xi.msg.basic, not kparser chatline ParseCodes.
    let tryClassifyAction messageId =
        match messageId with
        | n when n = MagicRecoversHP || n = TargetRecoversHPSimple ->
            Some(InteractionType.Aid, None, Some AidType.Recovery)
        | n when n = MagicGainEffect -> Some(InteractionType.Aid, None, Some AidType.Enhance)
        | n when n = MagicNoEffect || n = MagicFail -> Some(InteractionType.Aid, None, Some AidType.Enhance)
        | n when n = MagicDmg -> Some(InteractionType.Harm, Some HarmType.Spell, None)
        | n when n = MagicEnfeebIs || n = MagicEnfeeb -> Some(InteractionType.Harm, Some HarmType.Enfeeble, None)
        | n when
            n = IsStatus
            || n = IsNoLongerStatus
            || n = GainsEffectOfStatus
            || n = StatusWearsOff
            ->
            Some(InteractionType.Aid, None, Some AidType.Enhance)
        | _ -> None

    let classify (messageNum: int) (messageType: int) =
        match messageNum with
        | n when n = DefeatsTarget || n = FallsToGround -> InteractionType.Death, None, None
        | n when n = MagicRecoversHP || n = TargetRecoversHPSimple ->
            InteractionType.Aid, None, Some AidType.Recovery
        | n when n = ExperiencePointsGained || n = ExpChain -> InteractionType.Unknown, None, None
        | n when n = AttackHits -> InteractionType.Harm, Some HarmType.Melee, None
        | n when n = AttackMisses -> InteractionType.Harm, Some HarmType.Melee, None
        | n when
            n = IsStatus
            || n = IsNoLongerStatus
            || n = GainsEffectOfStatus
            || n = StatusWearsOff
            ->
            InteractionType.Aid, None, Some AidType.Enhance
        | _ when messageType >= 4 -> InteractionType.Aid, None, Some AidType.Enhance
        | _ -> InteractionType.Unknown, None, None

    let isExperienceMessage messageNum =
        messageNum = ExperiencePointsGained || messageNum = ExpChain

    let messageLabel messageNum =
        match messageNum with
        | 8 -> "Experience Points"
        | 253 -> "EXP Chain"
        | 6 -> "Defeats Target"
        | 20 -> "Falls to Ground"
        | 7 -> "Magic Recovers HP"
        | 1 -> "Attack Hits"
        | 15 -> "Attack Misses"
        | 203 -> "Is Status"
        | 204 -> "No Longer Status"
        | 205 -> "Gains Effect"
        | 206 -> "Status Wears Off"
        | n -> $"MsgBasic-{n}"
