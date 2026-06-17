namespace kparser2.Analytics

/// Format string constants ported from kparser Combat.resx and NonCombat.resx.
module ReportTemplates =
    module Public =
        let total = "Total"
        let all = "All"

    module Performance =
        let overallTitle = "Overall"
        let overallHeader = "Total # Fights      Total Fights Length"
        let overallFormat = "{0,-18}{1,21}"
        let participateTitle = "Player Participation (Offensive)"
        let participateFightsHeader = "Player           Number of fights   % Participation"
        let participateFightsFormat = "{0,-17}{1,16}{2,18:p2}"
        let participateTimeHeader = "Player           Total time fighting   Total fight lengths   Avg Time/Fight   % Fights' Time   % Overall Time"
        let participateTimeFormat = "{0,-17}{1,19}{2,22}{3,17}{4,17:p2}{5,17:p2}"
        let dpsTitle = "Damage Per Second"
        let dpsHeader = "Player              Melee DPS   Ranged DPS     WS DPS    Magic DPS    Other DPS    Total DPS"
        let dpsFormat = "{0,-17}{1,12:f2}{2,13:f2}{3,11:f2}{4,13:f2}{5,13:f2}{6,13:f2}"

    module Offense =
        let titleSummary = "Damage Summary"
        let headerSummary = "Player               Total Dmg   Damage %   Melee Dmg   Range Dmg   Abil. Dmg  WSkill Dmg   Spell Dmg  Other Dmg   Absorbed Dmg"
        let formatSummary = "{0,-20}{1,10}{2,11:p2}{3,12}{4,12}{5,12}{6,12}{7,12}{8,11}{9,15}"
        let titleMelee = "Melee Damage"
        let headerMelee = "Player            Melee Dmg  Abs'd.Dmg   Net Dmg   Melee %   Hit/Miss    M.HR %   M.Acc %  M.Low/Hi  M+0.Avg  M-0.Avg"
        let formatMelee = "{0,-17}{1,10}{2,11}{3,10}{4,10:p2}{5,11}{6,10:p2}{7,10:p2}{8,10}{9,9:f2}{10,9:f2}"
        let titleMeleeCrit = "Melee Crit Damage"
        let headerMeleeCrit = "Player                #Crit  C.Low/Hi   C-0.Avg     Crit%"
        let formatMeleeCrit = "{0,-17}{1,10}{2,10}{3,10:f2}{4,10:p2}"
        let titleRanged = "Ranged Damage"
        let headerRanged = "Player            Range Dmg   Range %   Hit/Miss    R.HR %   R.Acc %  R.Low/Hi  R+0.Avg  R-0.Avg  #Crit  C.Low/Hi   C-0.Avg     Crit%"
        let formatRanged = "{0,-17}{1,10}{2,10:p2}{3,11}{4,10:p2}{11,10:p2}{5,10}{6,9:f2}{12,9:f2}{7,7}{8,10}{9,10:f2}{10,10:p2}"
        let titleAbility = "Ability Damage"
        let headerAbility = "Player                  Abil. Dmg  Abs'd.Dmg   Net Dmg    Abil. %  Hit/Miss    A.Acc %    A.Low/Hi    A.Avg"
        let formatAbility = "{0,-23}{1,10}{2,11}{3,10}{4,11:p2}{5,10}{6,11:p2}{7,12}{8,9:f2}"
        let titleWeaponskill = "Weaponskill Damage"
        let headerWeaponskill = "Player                 WSkill Dmg  Abs'd.Dmg   Net Dmg   WSkill %  Hit/Miss   WS.Acc %   WS.Low/Hi   WS.Avg"
        let formatWeaponskill = "{0,-23}{1,10}{2,11}{3,10}{4,11:p2}{5,10}{6,11:p2}{7,12}{8,9:f2}"
        let titleSpell = "Spell Damage"
        let headerSpell = "Player                  Spell Dmg  Abs'd.Dmg   Net Dmg   Spell %  #Spells  #Fail  S.Low/Hi     S.Avg  #MBurst"
        let formatSpell = "{0,-23}{1,10}{2,11}{3,10}{4,10:p2}{5,9}{6,7}{7,10}{8,10:f2}{9,9}"
        let titleSkillchain = "Skillchain Damage"
        let headerSkillchain = "Skillchain          SC Dmg  Abs'd.Dmg   Net Dmg   # SC  SC.Low/Hi  SC.Avg"
        let formatSkillchain = "{0,-20}{1,6}{2,11}{3,10}{4,7}{5,11}{6,8:f2}"

    module Defense =
        let titleSummary = "Damage Taken Summary"
        let headerSummary = "Player             Total Dmg   Damage %   Melee Dmg   Range Dmg   Abil. Dmg  WSkill Dmg   Spell Dmg  Other Dmg"
        let formatSummary = "{0,-18}{1,10}{2,11:p2}{3,12}{4,12}{5,12}{6,12}{7,12}{8,11}"
        let titleMelee = "Melee Damage Taken"
        let headerMelee = "Player             Melee Dmg   Melee %   Hit/Miss   M.Low/Hi    M.Avg  #Crit  C.Low/Hi   C.Avg     Crit%"
        let formatMelee = "{0,-18}{1,10}{2,10:p2}{3,11}{5,11}{6,9:f2}{7,7}{8,10}{9,8:f2}{10,10:p2}"
        let titleRanged = "Ranged Damage Taken"
        let headerRanged = "Player             Range Dmg   Range %   Hit/Miss   R.Low/Hi    R.Avg  #Crit  C.Low/Hi   C.Avg     Crit%"
        let formatRanged = "{0,-18}{1,10}{2,10:p2}{3,11}{5,11}{6,9:f2}{7,7}{8,10}{9,8:f2}{10,10:p2}"
        let titleAbility = "Ability Damage Taken"
        let headerAbility = "Player                  Abil. Dmg    Abil. %  Hit/Miss    A.Acc %    A.Low/Hi    A.Avg"
        let formatAbility = "{0,-23}{1,10}{2,11:p2}{3,10}{4,11:p2}{5,12}{6,9:f2}"
        let titleWeaponskill = "Weaponskill Damage Taken"
        let headerWeaponskill = "Player                  WSkill Dmg   WSkill %  Hit/Miss   WS.Acc %   WS.Low/Hi   WS.Avg"
        let formatWeaponskill = "{0,-23}{1,10}{2,11:p2}{3,10}{4,11:p2}{5,12}{6,9:f2}"
        let titleSpell = "Spell Damage Taken"
        let headerSpells = "Player                  Spell Dmg   Spell %  #Spells  #Fail  S.Low/Hi     S.Avg  #MBurst  MB.Low/Hi   MB.Avg"
        let formatSpells = "{0,-23}{1,10}{2,10:p2}{3,9}{4,7}{5,10}{6,10:f2}{7,9}{8,11}{9,9:f2}"
        let titleSkillchain = "Skillchain Damage Taken"
        let headerSkillchain = "Skillchain          SC Dmg  # SC  SC.Low/Hi  SC.Avg"
        let formatSkillchain = "{0,-20}{1,6}{2,6}{3,11}{4,8:f2}"

    module Fights =
        let fightHeader = "Fight #  Enemy                 Killed?  Killed By      Start     End       Length     Exp  Chain"
        let fightFormat = "{0,-8}{1,-22}{2,-9}{3,-15}{4,9}{5,10}{6,11}{7,5}{8,6}"

    module Death =
        let title = "Player Deaths"
        let summaryTitle = "Summary"
        let summaryHeader = "Player               # Deaths"
        let summaryFormat = "{0,-20}{1,9}"
        let detailsTitle = "Details"
        let detailsHeader = "Player               Time of Death                 Killed By"
        let detailsFormat = "{0,-20}{1,14}{2,26}"

    module Recovery =
        let titleRecovery = "Damage Recovery"
        let headerRecovery = "Player           Dmg Taken   HP Drained   HP Cured   #Regen   #Regen 2   #Regen 3   #Regen 4"
        let formatRecovery = "{0,-17}{1,9}{2,13}{3,11}{4,9}{5,11}{6,11}{7,11}"
        let titleCuring = "Curing (Whm spells or equivalent)"
        let headerCuring = "Player           Cured (Sp)  Cured (Ab)  C.1s  C.2s  C.3s  C.4s  C.5s  C.6s  Curagas  Rg.1s  Rg.2s  Rg.3s  Rg.4s"
        let formatCuring = "{0,-17}{1,9}{2,12}{3,7}{4,6}{5,6}{6,6}{7,6}{8,6}{9,9}{10,7}{11,7}{12,7}{13,7}"
        let titleAvgCuring = "Average Curing (Whm spells or equivalent)"
        let headerAvgCuring = "Player           Cure 1   Cure 2   Cure 3   Cure 4   Cure 5   Cure 6   Curaga   Ability"
        let formatAvgCuring = "{0,-17}{1,6:f2}{2,9:f2}{3,9:f2}{4,9:f2}{5,9:f2}{6,9:f2}{7,9:f2}{8,10:f2}"
        let titleStatusCuring = "Status Curing"
        let headerStatusCuring = "Status               # Times Cast     # No Effect"
        let titleStatusCured = "Statuses Cured"
        let formatStatusCures = "{0,-20}{1,13}{2,16}"
        let formatStatusCuresSub = " - {0,-17} {1,12}"

    module Buff =
        let receivedHeader = "Buff                Used by             # Times   Min Interval   Max Interval   Avg Interval"
        let usedHeader = "Buff                Used on             # Times   Min Interval   Max Interval   Avg Interval"
        let intervalsFormat = "{0,15}{1,15}{2,15}"
        let numTimesFormat = "{0,7}"

    module Debuff =
        let debuffHeader = "Debuff                # Times   # Successful   # No Effect   % Successful"
        let debuffWithTargetsHeader = "Debuff              Target              # Times   # Successful   # No Effect   % Successful"
        let mobDebuffFormat = "{0,7:d}{1,15:d}{2,14:d}{3,15:p2}"
        let playerDebuffFormat = "{0,9:d}{1,15:d}{2,14:d}{3,15:p2}"

    module Enfeeble =
        let titleDurations = "Enfeeble Durations"
        let headerDurations = "Debuff               #Successful     Total Duration     Avg Duration"
        let formatDurations = "{0,12:d}{1,19}{2,17}"
        let titleParalyze = "Paralyzed Actions"
        let headerParalyze = "# Fights      # Paralyze Cast    # Times Paralyzed    Max # Paralyzable Actions    Paralyze Rate"
        let formatParalyze = "{0,8:d}{1,21}{2,21}{3,29}{4,17:p2}"
        let titleTPMoves = "TP Moves"
        let headerTPMoves = "# Moves      Total Time      # Fights    Avg Time/TP Move      Avg TP Moves/Minute"
        let formatTPMoves = "{0,7:d}{1,16}{2,14}{3,20}{4,25:f2}"

    module ExtraAttacks =
        let mainSectionTitle = "Melee Data"
        let headerMain1 = "Player               # Melee Attacks    # Melee Rounds    Attacks/Round    # Extra Attacks"
        let formatMain1 = "{0,-20}{1,16}{2,18}{3,17}{4,19}"
        let headerMain2 = "Player               # +1 Rounds   # +2 Rounds   # +3 Rounds   # +4 Rounds    # >+4 Rounds"
        let formatMain2 = "{0,-20}{2,12}{3,14}{4,14}{5,14}{6,16}"
        let headerMain3 = "Player               # MultiAttack Rounds    MultiAttack %     Kills w/Min Attacks    Kills w/<Min Attacks"
        let formatMain3 = "{0,-20}{1,21}{2,17:p2}{3,24}{4,24}"

    module WSRate =
        let title = "Weaponskill/TP Rates"
        let mainHeader = "Player               # Melee Hits   # Retal.   # WSkills   Min Hits   Max Hits     Mean   Median   Mode"
        let mainFormat = "{0,-20}{1,13}{2,10}{3,13}{4,11}{5,11}{6,9:f2}{7,9:f2}{8,9}{9,7}"
        let detailsTitle = "Details"
        let detailsMHeader = "     Melee      Frequency"
        let detailsFormat = "{0,10}  :  {1,10}"
        let wsHeader = "Player               Weaponskills    Avg Interval"
        let wsFormat = "{0,-20}{1,13}{2,16}"

    module Thief =
        let formatDataLineLong = "    {0,-15}{1,15}{2,10}{3,10}"
        let formatSummary1 = "    {0,-20}{1,10}{2,20}"
        let formatSummaryP = "    {0,-20}{1,10}{2,20:f2}"

    module Corsair =
        let rollFrequency = "Roll Frequency"
        let fullRollHeader = "                   1       2       3       4       5       6       7       8       9      10      11    Bust"
        let longFrequencyFormat = "Frequency:  {0,8:d}{1,8:d}{2,8:d}{3,8:d}{4,8:d}{5,8:d}{6,8:d}{7,8:d}{8,8:d}{9,8:d}{10,8:d}{11,8:d}"
        let longFloatFormat = "Percentage: {0,8:f2}{1,8:f2}{2,8:f2}{3,8:f2}{4,8:f2}{5,8:f2}{6,8:f2}{7,8:f2}{8,8:f2}{9,8:f2}{10,8:f2}{11,8:f2}"
        let shortFormat = "{0,-11} {1,8:d}{2,8:d}{3,8:d}{4,8:d}{5,8:d}{6,8:d}"
        let averageFormat = "Average:    {0,8:f2}"

    module Items =
        let generalHeader = "Item                                  Used"

    module Experience =
        let experienceRates = "Experience Rates"
        let experienceChains = "Experience Chains"
        let chainHeader = "Chain   Count   Total XP   Avg XP"
        let chainFormat = "{0,-5}{1,8}{2,11}{3,9:F2}"
        let mobListing = "Mob Listing"
        let mobListingHeader = "Mob                        Base XP   Number   Avg Fight Time"
        let xpListFormatNum = "{0,-16} : {1}"
        let xpListFormatTime = "{0,-16} : {1:d}:{2:d2}:{3:d2}"
        let xpListFormatSec = "{0,-16} : {1:F2}"
        let xpListFormatDec = "{0,-16} : {1:F2}"
        let mobListingHeaderWithGain = "Mob                        Base XP  Gained XP   Number   Avg Fight Time"

    module Treasure =
        let dropItemFormat = "{0,9} {1,-28} [Max #: {2}]  [Items/Kill: {3,6:f3}]  [Drop Rate: {4,8:p2}]  [% of Drops: {5,8:p2}]"
        let dropGilFormat = "{0,9} {1,-28} [Average:   {2,8:f2}]"
        let timesKilledFormat = "{0} (Killed {1} times)"

    module Chat =
        let summaryHeader = "Mode                 Speaker              Count"
        let summaryFormat = "{0,-20}{1,-20}{2,8}"
