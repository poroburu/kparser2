namespace kparser2.Analytics

open kparser2.Decoders

module InteractionClassification =
    let private criticalMessageIds =
        Set.ofList [ 0x1C; 0x1D; 0x1E; 0x1F; 0x6D; 0xB9 ]

    let classifyDamageModifier (messageId: int) (hasProc: bool) =
        if criticalMessageIds.Contains messageId then
            DamageModifier.Critical
        elif hasProc then
            DamageModifier.MagicBurst
        else
            DamageModifier.Normal

    let classifyCategory
        (interactionType: InteractionType)
        (harmType: HarmType option)
        (aidType: AidType option)
        (damageModifier: DamageModifier)
        (actionName: string)
        =
        if actionName.Contains("Skillchain", System.StringComparison.OrdinalIgnoreCase) then
            InteractionCategory.Skillchain
        else
            match interactionType with
            | InteractionType.Death -> InteractionCategory.Death
            | InteractionType.Aid ->
                match aidType with
                | Some AidType.Recovery -> InteractionCategory.Recovery
                | Some AidType.Enhance -> InteractionCategory.Enhance
                | _ -> InteractionCategory.Other
            | InteractionType.Harm ->
                match harmType with
                | Some HarmType.Enfeeble -> InteractionCategory.Enfeeble
                | Some HarmType.Spell ->
                    if damageModifier = DamageModifier.MagicBurst then
                        InteractionCategory.Spell
                    else
                        InteractionCategory.Spell
                | Some HarmType.Weaponskill -> InteractionCategory.Weaponskill
                | Some HarmType.Ability -> InteractionCategory.Ability
                | Some HarmType.Ranged ->
                    if damageModifier = DamageModifier.Critical then
                        InteractionCategory.RangedCrit
                    else
                        InteractionCategory.Ranged
                | Some HarmType.Melee ->
                    if damageModifier = DamageModifier.Critical then
                        InteractionCategory.MeleeCrit
                    else
                        InteractionCategory.Melee
                | Some HarmType.Other -> InteractionCategory.OtherPhysical
                | None -> InteractionCategory.Other
            | _ -> InteractionCategory.Other

    let categoryLabel category =
        match category with
        | InteractionCategory.Melee -> "Melee"
        | InteractionCategory.MeleeCrit -> "Melee Crit"
        | InteractionCategory.Ranged -> "Ranged"
        | InteractionCategory.RangedCrit -> "Ranged Crit"
        | InteractionCategory.Spell -> "Spell"
        | InteractionCategory.Ability -> "Ability"
        | InteractionCategory.Weaponskill -> "Weaponskill"
        | InteractionCategory.Skillchain -> "Skillchain"
        | InteractionCategory.Enfeeble -> "Enfeeble"
        | InteractionCategory.OtherPhysical -> "Other Physical"
        | InteractionCategory.OtherMagical -> "Other Magical"
        | InteractionCategory.Recovery -> "Recovery"
        | InteractionCategory.Enhance -> "Enhance"
        | InteractionCategory.Death -> "Death"
        | InteractionCategory.Other -> "Other"
