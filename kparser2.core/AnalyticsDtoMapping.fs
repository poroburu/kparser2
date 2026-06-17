namespace kparser2.Core

open kparser2.Abstractions
open kparser2.Analytics

module AnalyticsDtoMapping =
    let private enumToString value = $"{value}"

    let private parseEntityKind (value: string) =
        match value.ToLowerInvariant() with
        | "player" -> EntityKind.Player
        | "mob" -> EntityKind.Mob
        | "pet" -> EntityKind.Pet
        | "fellow" -> EntityKind.Fellow
        | _ -> EntityKind.Unknown

    let private parseInteractionType (value: string) =
        match value.ToLowerInvariant() with
        | "harm" -> InteractionType.Harm
        | "aid" -> InteractionType.Aid
        | "death" -> InteractionType.Death
        | _ -> InteractionType.Unknown

    let private parseHarmType (value: string) =
        match value.ToLowerInvariant() with
        | "melee" -> HarmType.Melee
        | "ranged" -> HarmType.Ranged
        | "spell" -> HarmType.Spell
        | "weaponskill" -> HarmType.Weaponskill
        | "enfeeble" -> HarmType.Enfeeble
        | "ability" -> HarmType.Ability
        | _ -> HarmType.Other

    let private parseAidType (value: string) =
        match value.ToLowerInvariant() with
        | "recovery" -> AidType.Recovery
        | "enhance" -> AidType.Enhance
        | "item" -> AidType.Item
        | "removeenmity" -> AidType.RemoveEnmity
        | _ -> AidType.Unknown

    let private parseDamageModifier (value: string) =
        match value.ToLowerInvariant() with
        | "critical" -> DamageModifier.Critical
        | "magicburst" -> DamageModifier.MagicBurst
        | "normal" -> DamageModifier.Normal
        | _ -> DamageModifier.Unknown

    let private parseCategory (value: string) =
        match value with
        | "Melee Crit" -> InteractionCategory.MeleeCrit
        | "Melee" -> InteractionCategory.Melee
        | "Ranged Crit" -> InteractionCategory.RangedCrit
        | "Ranged" -> InteractionCategory.Ranged
        | "Spell" -> InteractionCategory.Spell
        | "Ability" -> InteractionCategory.Ability
        | "Weaponskill" -> InteractionCategory.Weaponskill
        | "Skillchain" -> InteractionCategory.Skillchain
        | "Enfeeble" -> InteractionCategory.Enfeeble
        | "Other Physical" -> InteractionCategory.OtherPhysical
        | "Other Magical" -> InteractionCategory.OtherMagical
        | "Recovery" -> InteractionCategory.Recovery
        | "Enhance" -> InteractionCategory.Enhance
        | "Death" -> InteractionCategory.Death
        | _ -> InteractionCategory.Other

    let private toCombatant (c: Combatant) =
        CombatantDto(
            Id = c.Id,
            Name = c.Name,
            Kind = enumToString c.Kind,
            Job = c.Job,
            PlayerInfo = Option.toObj c.PlayerInfo
        )

    let private toBattle (b: Battle) =
        BattleDto(
            Id = b.Id,
            EnemyName = b.EnemyName,
            EnemyId = Option.toNullable b.EnemyId,
            StartMs = b.StartMs,
            EndMs = Option.toNullable b.EndMs,
            Killed = b.Killed,
            KillerId = Option.toNullable b.KillerId,
            ExperiencePoints = b.ExperiencePoints,
            ExperienceChain = b.ExperienceChain
        )

    let private toInteraction (i: Interaction) =
        let harmType = i.HarmType |> Option.map enumToString |> Option.toObj
        let aidType = i.AidType |> Option.map enumToString |> Option.toObj

        InteractionDto(
            Id = i.Id,
            BattleId = Option.toNullable i.BattleId,
            TimestampMs = i.TimestampMs,
            InteractionType = enumToString i.InteractionType,
            HarmType = harmType,
            AidType = aidType,
            Category = InteractionClassification.categoryLabel i.Category,
            DamageModifier = enumToString i.DamageModifier,
            ActorId = i.ActorId,
            TargetId = i.TargetId,
            ActorName = i.ActorName,
            TargetName = i.TargetName,
            ActionName = i.ActionName,
            Value = i.Value,
            Success = i.Success,
            CommandNo = i.CommandNo,
            MessageId = i.MessageId,
            IsProc = i.IsProc,
            ProcValue = i.ProcValue,
            IsLocalPlayerActor = i.IsLocalPlayerActor,
            IsLocalPlayerTarget = i.IsLocalPlayerTarget
        )

    let private toChat (c: ChatMessageRecord) =
        ChatMessageDto(
            TimestampMs = c.TimestampMs,
            Mode = c.Mode,
            ModeId = c.ModeId,
            IsGm = c.IsGm,
            Speaker = c.Speaker,
            Message = c.Message,
            PacketId = c.PacketId,
            Direction = c.Direction,
            IsLocalPlayer = c.IsLocalPlayer,
            TargetName = Option.toObj c.TargetName
        )

    let private toLoot (l: LootRecord) =
        LootRecordDto(
            TimestampMs = l.TimestampMs,
            EventType = l.EventType,
            ItemId = l.ItemId,
            ItemName = l.ItemName,
            Quantity = l.Quantity,
            Gil = l.Gil,
            PoolSlot = l.PoolSlot,
            ActorName = l.ActorName,
            Detail = l.Detail
        )

    let private toItemUse (u: ItemUseRecord) =
        ItemUseDto(
            TimestampMs = u.TimestampMs,
            ActorId = u.ActorId,
            ActorName = u.ActorName,
            ItemId = u.ItemId,
            ItemName = u.ItemName,
            Quantity = u.Quantity
        )

    let private toExperience (x: ExperienceRecord) =
        ExperienceRecordDto(
            TimestampMs = x.TimestampMs,
            ActorId = x.ActorId,
            ActorName = x.ActorName,
            ExperiencePoints = x.ExperiencePoints,
            Chain = x.Chain,
            BattleId = Option.toNullable x.BattleId
        )

    let toSnapshotDto (snap: AnalyticsSnapshot) =
        let combatants = snap.Combatants |> List.map toCombatant
        let battles = snap.Battles |> List.map toBattle
        let interactions = snap.Interactions |> List.map toInteraction
        let chatMessages = snap.ChatMessages |> List.map toChat
        let lootRecords = snap.LootRecords |> List.map toLoot
        let itemUses = snap.ItemUses |> List.map toItemUse
        let experienceRecords = snap.ExperienceRecords |> List.map toExperience

        new AnalyticsSnapshotDto(
            SessionStartMs = snap.SessionStartMs,
            ZoneName = snap.ZoneName,
            Combatants = combatants,
            Battles = battles,
            Interactions = interactions,
            ChatMessages = chatMessages,
            LootRecords = lootRecords,
            ItemUses = itemUses,
            ExperienceRecords = experienceRecords
        )

    let private fromCombatant (c: CombatantDto) =
        { Id = c.Id
          Name = c.Name
          Kind = parseEntityKind c.Kind
          Job = c.Job
          PlayerInfo = Option.ofObj c.PlayerInfo }

    let private fromBattle (b: BattleDto) =
        { Id = b.Id
          EnemyName = b.EnemyName
          EnemyId =
              if b.EnemyId.HasValue then
                  Some b.EnemyId.Value
              else
                  None
          StartMs = b.StartMs
          EndMs =
              if b.EndMs.HasValue then
                  Some b.EndMs.Value
              else
                  None
          Killed = b.Killed
          KillerId =
              if b.KillerId.HasValue then
                  Some b.KillerId.Value
              else
                  None
          ExperiencePoints = b.ExperiencePoints
          ExperienceChain = b.ExperienceChain }

    let private fromInteraction (i: InteractionDto) =
        { Id = i.Id
          BattleId =
              if i.BattleId.HasValue then
                  Some i.BattleId.Value
              else
                  None
          TimestampMs = i.TimestampMs
          InteractionType = parseInteractionType i.InteractionType
          HarmType = i.HarmType |> Option.ofObj |> Option.map parseHarmType
          AidType = i.AidType |> Option.ofObj |> Option.map parseAidType
          Category = parseCategory i.Category
          DamageModifier = parseDamageModifier i.DamageModifier
          ActorId = i.ActorId
          TargetId = i.TargetId
          ActorName = i.ActorName
          TargetName = i.TargetName
          ActionName = i.ActionName
          Value = i.Value
          Success = i.Success
          CommandNo = i.CommandNo
          MessageId = i.MessageId
          IsProc = i.IsProc
          ProcValue = i.ProcValue
          IsLocalPlayerActor = i.IsLocalPlayerActor
          IsLocalPlayerTarget = i.IsLocalPlayerTarget }

    let private fromChat (c: ChatMessageDto) =
        { TimestampMs = c.TimestampMs
          Mode = c.Mode
          ModeId = c.ModeId
          IsGm = c.IsGm
          Speaker = c.Speaker
          Message = c.Message
          PacketId = c.PacketId
          Direction = c.Direction
          IsLocalPlayer = c.IsLocalPlayer
          TargetName = c.TargetName |> Option.ofObj }

    let private fromLoot (l: LootRecordDto) =
        { TimestampMs = l.TimestampMs
          EventType = l.EventType
          ItemId = l.ItemId
          ItemName = l.ItemName
          Quantity = l.Quantity
          Gil = l.Gil
          PoolSlot = l.PoolSlot
          ActorName = l.ActorName
          Detail = l.Detail }

    let private fromItemUse (u: ItemUseDto) =
        { TimestampMs = u.TimestampMs
          ActorId = u.ActorId
          ActorName = u.ActorName
          ItemId = u.ItemId
          ItemName = u.ItemName
          Quantity = u.Quantity }

    let private fromExperience (x: ExperienceRecordDto) =
        { TimestampMs = x.TimestampMs
          ActorId = x.ActorId
          ActorName = x.ActorName
          ExperiencePoints = x.ExperiencePoints
          Chain = x.Chain
          BattleId =
              if x.BattleId.HasValue then
                  Some x.BattleId.Value
              else
                  None }

    let fromSnapshotDto (dto: AnalyticsSnapshotDto) =
        { SessionStartMs = dto.SessionStartMs
          ZoneName = dto.ZoneName
          Combatants = dto.Combatants |> Seq.map fromCombatant |> List.ofSeq
          Battles = dto.Battles |> Seq.map fromBattle |> List.ofSeq
          Interactions = dto.Interactions |> Seq.map fromInteraction |> List.ofSeq
          ChatMessages = dto.ChatMessages |> Seq.map fromChat |> List.ofSeq
          LootRecords = dto.LootRecords |> Seq.map fromLoot |> List.ofSeq
          ItemUses = dto.ItemUses |> Seq.map fromItemUse |> List.ofSeq
          ExperienceRecords = dto.ExperienceRecords |> Seq.map fromExperience |> List.ofSeq }

    let toMobFilter (dto: MobFilterDto) =
        { GroupMobs = dto.GroupMobs
          ExcludeZeroXp = dto.ExcludeZeroXp
          SelectedMobName = dto.SelectedMobName |> Option.ofObj
          SelectedBattleId =
              if dto.SelectedBattleId.HasValue then
                  Some dto.SelectedBattleId.Value
              else
                  None
          SelectedPlayerName = dto.SelectedPlayerName |> Option.ofObj }

    let toRowDto (row: QueryRow) =
        AnalyticsRowDto(Label = row.Label, Value = row.Value, Count = row.Count, Total = row.Total)

    let toReportSpanDto (span: ReportSpan) =
        AnalyticsReportSpanDto(
            Text = span.Text,
            Bold = span.Bold,
            Underline = span.Underline,
            Color = span.Color
        )

    let toReportDto (report: AnalyticsReport) =
        let spans = report.Spans |> List.map toReportSpanDto
        AnalyticsReportDto(Spans = spans)

module AnalyticsReportBridge =
    let run (queryId: string) (snap: AnalyticsSnapshot) (filter: MobFilter) =
        AnalyticsReports.format queryId snap filter |> AnalyticsDtoMapping.toReportDto

    let runChat (snap: AnalyticsSnapshot) (modeFilter: string option) (speakerFilter: string option) =
        AnalyticsReports.formatChat snap modeFilter speakerFilter |> AnalyticsDtoMapping.toReportDto

    let runChatSummary (snap: AnalyticsSnapshot) (modeFilter: string option) (speakerFilter: string option) =
        AnalyticsReports.formatChatSummary snap modeFilter speakerFilter |> AnalyticsDtoMapping.toReportDto

module AnalyticsQueryBridge =
    let run (queryId: string) (snap: AnalyticsSnapshot) (filter: MobFilter) =
        let rows =
            match queryId with
            | "fights" -> AnalyticsQueries.fights snap
            | "offense" -> AnalyticsQueries.offenseSummary snap filter
            | "offense-detail" -> AnalyticsQueries.offenseDetail snap filter
            | "defense" -> AnalyticsQueries.defenseSummary snap filter
            | "defense-detail" -> AnalyticsQueries.defenseDetail snap filter
            | "deaths" -> AnalyticsQueries.deaths snap filter
            | "recovery" -> AnalyticsQueries.recovery snap filter
            | "buffs" -> AnalyticsQueries.buffs snap filter
            | "debuffs" -> AnalyticsQueries.debuffs snap filter
            | "enfeebling" -> AnalyticsQueries.enfeebling snap filter
            | "experience" -> AnalyticsQueries.experience snap
            | "chat" -> AnalyticsQueries.chat snap None None
            | "chat-summary" -> AnalyticsQueries.chatSummary snap None None
            | "skillchain" -> AnalyticsQueries.skillchain snap filter
            | "buffs-by-time" -> AnalyticsQueries.timelineBuffs snap filter
            | "def-by-time" -> AnalyticsQueries.defenseByTime snap filter
            | "extra-attacks" -> AnalyticsQueries.extraAttacks snap filter
            | "add-effect" -> AnalyticsQueries.additionalEffects snap filter
            | "ws-rates" -> AnalyticsQueries.wsRates snap filter
            | "thief" -> AnalyticsQueries.thiefStats snap filter
            | "corsair" -> AnalyticsQueries.corsairRolls snap filter
            | "performance" -> AnalyticsQueries.performance snap filter
            | "items" -> AnalyticsQueries.itemUses snap
            | "loot" -> AnalyticsQueries.lootSummary snap
            | "players" -> AnalyticsQueries.players snap
            | "mobs" -> AnalyticsQueries.mobs snap
            | "abyssea" -> AnalyticsQueries.abysseaChests snap
            | _ -> []

        rows |> List.map AnalyticsDtoMapping.toRowDto

module AnalyticsQueryService =
    let query (queryId: string) (dto: AnalyticsSnapshotDto) (filter: MobFilterDto) =
        let snap = AnalyticsDtoMapping.fromSnapshotDto dto
        let mobFilter = AnalyticsDtoMapping.toMobFilter filter
        AnalyticsQueryBridge.run queryId snap mobFilter

module AnalyticsReportService =
    let format (queryId: string) (dto: AnalyticsSnapshotDto) (filter: MobFilterDto) =
        let snap = AnalyticsDtoMapping.fromSnapshotDto dto
        let mobFilter = AnalyticsDtoMapping.toMobFilter filter
        AnalyticsReportBridge.run queryId snap mobFilter

    let formatChat (dto: AnalyticsSnapshotDto) (modeFilter: string option) (speakerFilter: string option) =
        let snap = AnalyticsDtoMapping.fromSnapshotDto dto
        AnalyticsReportBridge.runChat snap modeFilter speakerFilter

    let formatChatSummary (dto: AnalyticsSnapshotDto) (modeFilter: string option) (speakerFilter: string option) =
        let snap = AnalyticsDtoMapping.fromSnapshotDto dto
        AnalyticsReportBridge.runChatSummary snap modeFilter speakerFilter
