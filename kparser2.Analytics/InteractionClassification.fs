namespace kparser2.Analytics

open kparser2.Decoders

module InteractionClassification =
    let private criticalMessageIds =
        Set.ofList [ 0x1C; 0x1D; 0x1E; 0x1F; 67; 0x6D ]

    let classifyDamageModifier (messageId: int) (_hasProc: bool) =
        if criticalMessageIds.Contains messageId then
            DamageModifier.Critical
        elif messageId = 252 || messageId = 274 then
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

    /// Proc values that are additional HP damage. Heals, drains, and status ids are not.
    let isAdditionalDamageProc (i: Interaction) =
        i.IsProc && i.ProcValue > 0 && not (MsgBasicCatalog.isNonDamageProc i.ProcMessageId)

    let isHpDamage (i: Interaction) =
        i.InteractionType = InteractionType.Harm
        && i.HarmType <> Some HarmType.Enfeeble
        && not (MsgBasicCatalog.isNonHpResourceTransfer i.MessageId)

    /// HP restored to a combatant. Skill MP recovery (224) stays a recovery row and is not curing.
    let isHpRecovery (i: Interaction) =
        i.InteractionType = InteractionType.Aid
        && i.AidType = Some AidType.Recovery
        && i.MessageId <> MsgBasicCatalog.SkillRecoversMp

    /// React message 44 (SPIKES_EFFECT_DMG) only. Recover-HP and other reacts are not spike HP.
    let isSpike (i: Interaction) =
        i.InteractionType = InteractionType.Harm
        && i.MessageId = MsgBasicCatalog.SpikesEffectDmg
        && i.ActionName.Equals("Spikes", System.StringComparison.OrdinalIgnoreCase)

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
