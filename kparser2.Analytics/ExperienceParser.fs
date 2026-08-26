namespace kparser2.Analytics

open System.Text.RegularExpressions

module ExperienceParser =
    let private expPoints =
        Regex(@"^(?<name>.+?) gains (?<xp>\d+) experience points\.?$", RegexOptions.IgnoreCase)

    let private expChain =
        Regex(@"^EXP chain #(?<chain>\d+)!$", RegexOptions.IgnoreCase)

    let private expPointsLegacy =
        Regex(@"^(?<name>.+?) gains (?<xp>\d+) experience points", RegexOptions.IgnoreCase)

    type ParsedXp =
        { Points: int
          Chain: int
          ActorName: string option }

    let tryParseChat (message: string) =
        let trimmed = message.Trim()

        match expChain.Match trimmed with
        | m when m.Success ->
            Some
                { Points = 0
                  Chain = int m.Groups.["chain"].Value
                  ActorName = None }
        | _ ->
            match expPoints.Match trimmed with
            | m when m.Success ->
                Some
                    { Points = int m.Groups.["xp"].Value
                      Chain = 0
                      ActorName = Some m.Groups.["name"].Value }
            | _ ->
                match expPointsLegacy.Match trimmed with
                | m when m.Success ->
                    Some
                        { Points = int m.Groups.["xp"].Value
                          Chain = 0
                          ActorName = Some m.Groups.["name"].Value }
                | _ -> None

    let tryParseBattleMessage (messageNum: int) (param1: uint32) (param2: uint32) =
        // 0x29 synthetic fixtures put XP in Data2 (param2). Live 0x002D puts XP in Data (param1), Data2=0.
        let xpAmount =
            if param2 <> 0u then
                int param2
            else
                int param1

        match messageNum with
        | n when n = MsgBasicCatalog.ExperiencePointsGained ->
            Some
                { Points = xpAmount
                  Chain = 0
                  ActorName = None }
        | n when n = MsgBasicCatalog.ExpChain ->
            // Live 0x002D: Data=XP, Data2=chain (1-10). Windower 253 is chain + XP.
            // 0x29 fixtures put chain in Param1 and XP in Param2 (Param2 often > 10).
            let chain, points =
                if param2 >= 1u && param2 <= 10u && param1 > param2 then
                    int param2, int param1
                else
                    int param1, (if param2 <> 0u then int param2 else 0)

            Some
                { Points = points
                  Chain = chain
                  ActorName = None }
        | _ -> None
