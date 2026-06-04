namespace kparser2.Analytics

module BattleMessageCatalog =
    let private harmFromCommand (commandNo: int) (miss: int) (value: int) =
        if miss <> 0 && value = 0 then
            InteractionType.Harm,
            Some(
                match commandNo with
                | 1 -> HarmType.Melee
                | 2 -> HarmType.Ranged
                | n when n >= 3 && n <= 6 -> HarmType.Spell
                | n when n >= 7 -> HarmType.Weaponskill
                | _ -> HarmType.Other
            ),
            None
        elif value > 0 then
            let harmType =
                match commandNo with
                | 1 -> HarmType.Melee
                | 2 -> HarmType.Ranged
                | 3 -> HarmType.Ranged
                | n when n >= 4 && n <= 6 -> HarmType.Spell
                | n when n >= 7 && n <= 12 -> HarmType.Weaponskill
                | n when n >= 13 -> HarmType.Ability
                | _ -> HarmType.Other

            InteractionType.Harm, Some harmType, None
        else
            InteractionType.Unknown, None, None

    let classifyActionEffect (commandNo: int) (messageId: int) (miss: int) (value: int) =
        if messageId > 0 then
            let resolvedId = ParseCodesTables.resolveAlternateMessageId messageId

            match ParseCodesTables.tryInteractionType resolvedId with
            | Some interactionType ->
                let harmType =
                    if interactionType = InteractionType.Harm then
                        ParseCodesTables.tryHarmType resolvedId
                        |> Option.orElseWith (fun () ->
                            match commandNo with
                            | 1 -> Some HarmType.Melee
                            | 2 -> Some HarmType.Ranged
                            | n when n >= 4 && n <= 6 -> Some HarmType.Spell
                            | n when n >= 7 && n <= 12 -> Some HarmType.Weaponskill
                            | n when n >= 13 -> Some HarmType.Ability
                            | _ -> Some HarmType.Other)
                    else
                        None

                let aidType =
                    if interactionType = InteractionType.Aid then
                        ParseCodesTables.tryAidType resolvedId
                    else
                        None

                interactionType, harmType, aidType
            | None -> harmFromCommand commandNo miss value
        else
            harmFromCommand commandNo miss value

    let classifyCombatMessage (messageNum: int) (messageType: int) =
        MsgBasicCatalog.classify messageNum messageType

    /// Back-compat alias used by tests.
    let classifyHarm = classifyActionEffect

    let successLabel miss =
        match miss with
        | 0 -> "hit"
        | 1 -> "miss"
        | 2 -> "guard"
        | 3 -> "parry"
        | 4 -> "block"
        | 9 -> "evade"
        | n -> $"state-{n}"

    let actionName commandNo commandArg messageId =
        match ActionLookup.tryGetName commandArg with
        | Some name -> name
        | None ->
            match ActionLookup.tryGetName commandNo with
            | Some name -> name
            | None ->
                match messageId with
                | 0 -> $"cmd-{commandNo}"
                | id -> $"msg-{id}"

    let defensiveBuffNames =
        Set.ofList
            [ "Protect"
              "Protect II"
              "Protect III"
              "Protect IV"
              "Protect V"
              "Shell"
              "Shell II"
              "Shell III"
              "Shell IV"
              "Shell V"
              "Phalanx"
              "Phalanx II"
              "Blink"
              "Stoneskin"
              "Aquaveil"
              "Haste"
              "Refresh"
              "Regen"
              "Barrier"
              "Crusade"
              "Sentinel"
              "Defender"
              "Migawari"
              "Fan Dance"
              "Third Eye"
              "Seigan" ]

    let isDefensiveBuff (actionName: string) =
        defensiveBuffNames.Contains actionName
        || actionName.Contains("Protect", System.StringComparison.OrdinalIgnoreCase)
        || actionName.Contains("Shell", System.StringComparison.OrdinalIgnoreCase)
        || actionName.Contains("Phalanx", System.StringComparison.OrdinalIgnoreCase)

    let isThiefAction (actionName: string) =
        actionName.Contains("Sneak Attack", System.StringComparison.OrdinalIgnoreCase)
        || actionName.Contains("Trick Attack", System.StringComparison.OrdinalIgnoreCase)
        || actionName.Contains("Hide", System.StringComparison.OrdinalIgnoreCase)

    let isCorsairRoll (actionName: string) =
        actionName.Contains("Roll", System.StringComparison.OrdinalIgnoreCase)
        || actionName.Contains("Double-Up", System.StringComparison.OrdinalIgnoreCase)
        || actionName.Contains("Double Up", System.StringComparison.OrdinalIgnoreCase)
