namespace kparser2.Analytics

open System
open System.Collections.Generic
open kparser2.Decoders
open kparser2.Protocol

module SessionStore =
    type T =
        { mutable SessionStartMs: int64 option
          mutable ZoneName: string
          Combatants: Dictionary<uint32, Combatant>
          mutable Interactions: Interaction list
          mutable ChatMessages: ChatMessageRecord list
          mutable LootRecords: LootRecord list
          mutable ItemUses: ItemUseRecord list
          mutable ExperienceRecords: ExperienceRecord list
          mutable FightState: FightSegmenter.State
          mutable Battles: Battle list
          mutable PendingOutgoing: ChatIngest.PendingOutgoing list
          mutable RecentMsgBasicXp: (int64 * int) list }

    let create () =
        { SessionStartMs = None
          ZoneName = ""
          Combatants = Dictionary<uint32, Combatant>()
          Interactions = []
          ChatMessages = []
          LootRecords = []
          ItemUses = []
          ExperienceRecords = []
          FightState = FightSegmenter.initial
          Battles = []
          PendingOutgoing = []
          RecentMsgBasicXp = [] }

    let private timestampMs (store: T) (evt: PacketEvent) =
        let ts = int64 evt.Timestamp

        match store.SessionStartMs with
        | None ->
            store.SessionStartMs <- Some ts
            0L
        | Some start -> ts - start

    let private mapEntityKind kind =
        match kind with
        | EntityRegistry.EntityKind.Player -> EntityKind.Player
        | EntityRegistry.EntityKind.Mob -> EntityKind.Mob
        | EntityRegistry.EntityKind.Pet -> EntityKind.Pet
        | EntityRegistry.EntityKind.Fellow -> EntityKind.Fellow
        | EntityRegistry.EntityKind.Unknown -> EntityKind.Unknown

    let private upsertCombatant (store: T) (entityId: uint32) =
        let name = EntityRegistry.formatEntity entityId

        let kind =
            EntityRegistry.tryGetEntityKind entityId
            |> Option.map mapEntityKind
            |> Option.defaultValue EntityKind.Unknown

        let job = EntityRegistry.tryGetJob entityId |> Option.defaultValue ""

        let playerInfo =
            if kind = EntityKind.Player && not (String.IsNullOrWhiteSpace job) then
                Some job
            else
                None

        let combatant =
            { Id = entityId
              Name = name
              Kind = kind
              Job = job
              PlayerInfo = playerInfo }

        store.Combatants.[entityId] <- combatant

    let private syncBattles (store: T) =
        store.Battles <- store.FightState.Battles |> List.rev

    let private lootTypeLabel eventType =
        match eventType with
        | LootEventType.Found -> "Found"
        | LootEventType.Lot -> "Lot"
        | LootEventType.Pass -> "Pass"
        | LootEventType.Won -> "Won"
        | LootEventType.Floor -> "Floor"
        | LootEventType.Lost -> "Lost"

    let private needsSpeakerBackfill speaker =
        ChatIngest.isPlaceholderSpeaker speaker

    let private backfillPlaceholderSpeakers (store: T) =
        match EntityRegistry.localPlayerName () with
        | None -> ()
        | Some name ->
            store.ChatMessages <-
                store.ChatMessages
                |> List.map (fun c ->
                    if needsSpeakerBackfill c.Speaker then
                        { c with Speaker = name; IsLocalPlayer = true }
                    else
                        c)

    let private resolveChatForSnapshot (c: ChatMessageRecord) =
        if needsSpeakerBackfill c.Speaker then
            match EntityRegistry.localPlayerName () with
            | Some name -> { c with Speaker = name; IsLocalPlayer = true }
            | None -> c
        else
            c

    let private resolveInteractionForSnapshot (i: Interaction) =
        { i with
            ActorName = EntityRegistry.formatEntity i.ActorId
            TargetName = EntityRegistry.formatEntity i.TargetId
            IsLocalPlayerActor = EntityRegistry.isLocalPlayer i.ActorId
            IsLocalPlayerTarget = EntityRegistry.isLocalPlayer i.TargetId }

    let private trackMsgBasicXp (store: T) timestampMs points =
        if points > 0 then
            store.RecentMsgBasicXp <- (timestampMs, points) :: store.RecentMsgBasicXp |> List.truncate 8

    let private isDuplicateChatXp (store: T) timestampMs points =
        if points <= 0 then
            false
        else
            store.RecentMsgBasicXp
            |> List.exists (fun (ts, xp) -> xp = points && timestampMs - ts <= 2_000L)

    let private recordExperience (store: T) timestampMs actorId actorName points chain fromMsgBasic =
        if points <= 0 && chain <= 0 then
            ()
        elif not fromMsgBasic && isDuplicateChatXp store timestampMs points then
            ()
        else
            let fightState, battleId = FightSegmenter.applyExperience store.FightState timestampMs points chain
            store.FightState <- fightState
            syncBattles store

            if fromMsgBasic then
                trackMsgBasicXp store timestampMs points

            store.ExperienceRecords <-
                { TimestampMs = timestampMs
                  ActorId = actorId
                  ActorName = actorName
                  ExperiencePoints = points
                  Chain = chain
                  BattleId = battleId }
                :: store.ExperienceRecords

    let private ingestChat (store: T) (ts: int64) (evt: PacketEvent) (chat: ChatDecoded) =
        EntityRegistry.observeChatBootstrap chat evt.PacketId

        match
            ChatIngest.tryMatchOutgoingEcho store.PendingOutgoing ts chat.Mode chat.Message chat.Speaker
        with
        | Some speaker when not (String.IsNullOrWhiteSpace speaker) ->
            EntityRegistry.learnLocalPlayerFromEcho speaker chat.Mode chat.Message
        | _ -> ()

        let speaker =
            EntityRegistry.resolveChatSpeaker chat.Speaker evt.PacketId chat.ModeId

        if evt.PacketId = 0x00B5us then
            let pending: ChatIngest.PendingOutgoing =
                { TimestampMs = ts
                  Mode = chat.Mode
                  Message = chat.Message }

            store.PendingOutgoing <- pending :: store.PendingOutgoing |> List.truncate 32

        let record = ChatIngest.buildRecord ts evt chat speaker
        store.ChatMessages <- ChatIngest.appendChat store.ChatMessages record

        match ExperienceParser.tryParseChat chat.Message with
        | Some parsed ->
            let actorId =
                EntityRegistry.tryLocalPlayerId ()
                |> Option.defaultValue 0u

            let actorName =
                parsed.ActorName
                |> Option.defaultWith (fun () ->
                    if String.IsNullOrWhiteSpace speaker then
                        EntityRegistry.localPlayerName () |> Option.defaultValue "Unknown"
                    else
                        speaker)

            recordExperience store ts actorId actorName parsed.Points parsed.Chain false
        | None -> ()

    let ingest (store: T) (evt: PacketEvent) (decoded: DecoderResult) =
        EntityRegistry.observe evt
        let ts = timestampMs store evt

        match EntityRegistry.tryGetZoneId () with
        | Some zoneId ->
            match ZoneLookup.tryGetName zoneId with
            | Some name when store.ZoneName <> name ->
                store.ZoneName <- name
                store.FightState <- FightSegmenter.onZoneChange store.FightState ts
            | _ -> ()
        | None -> ()

        for id in EntityRegistry.allEntityIds () do
            upsertCombatant store id

        let mutable fightState = store.FightState

        let interactions =
            InteractionBuilder.fromDecoderEvents ts None decoded.Events
            |> List.map (fun interaction ->
                CombatEntityInference.inferFromInteraction interaction

                let fightState', battleId = FightSegmenter.applyInteraction fightState interaction
                fightState <- fightState'
                { interaction with BattleId = battleId })

        store.FightState <- fightState
        syncBattles store
        store.Interactions <- interactions @ store.Interactions

        for event in decoded.Events do
            match event with
            | DecoderEvent.Chat chat -> ingestChat store ts evt chat
            | DecoderEvent.CombatMessage message ->
                match
                    ExperienceParser.tryParseBattleMessage (int message.MessageNum) message.Param1 message.Param2
                with
                | Some parsed ->
                    let casterId = message.CasterId
                    let targetId = message.TargetId

                    let actorId, actorName =
                        if EntityRegistry.isLocalPlayer casterId then
                            casterId, EntityRegistry.formatEntity casterId
                        elif EntityRegistry.isLocalPlayer targetId then
                            targetId, EntityRegistry.formatEntity targetId
                        elif
                            EntityRegistry.tryGetEntityKind casterId = Some EntityRegistry.EntityKind.Pet
                            && EntityRegistry.tryLocalPlayerId().IsSome
                        then
                            EntityRegistry.tryLocalPlayerId().Value,
                            EntityRegistry.localPlayerName ()
                            |> Option.defaultWith (fun () -> EntityRegistry.formatEntity casterId)
                        else
                            casterId, EntityRegistry.formatEntity casterId

                    recordExperience store ts actorId actorName parsed.Points parsed.Chain true
                | None -> ()
            | DecoderEvent.Loot loot ->
                match loot.ActorId with
                | Some id when EntityRegistry.isLocalPlayer (uint32 id) ->
                    if not (String.IsNullOrWhiteSpace loot.ActorName) then
                        EntityRegistry.registerLocalPlayerName loot.ActorName
                | _ -> ()

                store.LootRecords <-
                    { TimestampMs = ts
                      EventType = lootTypeLabel loot.EventType
                      ItemId = loot.ItemId
                      ItemName = loot.ItemName
                      Quantity = loot.Quantity
                      Gil = loot.Gil
                      PoolSlot = loot.PoolSlot
                      ActorName =
                          if String.IsNullOrWhiteSpace loot.ActorName then
                              match loot.ActorId with
                              | Some id -> EntityRegistry.formatEntity (uint32 id)
                              | None -> "Unknown"
                          else
                              loot.ActorName
                      Detail = loot.Detail }
                    :: store.LootRecords
            | _ -> ()

        backfillPlaceholderSpeakers store

    let snapshot (store: T) =
        { SessionStartMs = store.SessionStartMs |> Option.defaultValue 0L
          ZoneName = store.ZoneName
          Combatants = store.Combatants.Values |> Seq.toList
          Battles = store.Battles
          Interactions = store.Interactions |> List.rev |> List.map resolveInteractionForSnapshot
          ChatMessages = store.ChatMessages |> List.rev |> List.map resolveChatForSnapshot
          LootRecords = store.LootRecords |> List.rev
          ItemUses = store.ItemUses |> List.rev
          ExperienceRecords = store.ExperienceRecords |> List.rev }

    let loadSnapshot (store: T) (snap: AnalyticsSnapshot) =
        store.SessionStartMs <- Some snap.SessionStartMs
        store.ZoneName <- snap.ZoneName
        store.Combatants.Clear()

        for c in snap.Combatants do
            store.Combatants.[c.Id] <- c

        store.Interactions <- snap.Interactions |> List.rev
        store.ChatMessages <- snap.ChatMessages |> List.rev
        store.LootRecords <- snap.LootRecords |> List.rev
        store.ItemUses <- snap.ItemUses |> List.rev
        store.ExperienceRecords <- snap.ExperienceRecords |> List.rev
        store.Battles <- snap.Battles
        store.PendingOutgoing <- []
        store.RecentMsgBasicXp <- []

        store.FightState <-
            { FightSegmenter.initial with
                Battles = snap.Battles |> List.rev
                NextBattleId =
                    (snap.Battles |> List.map (fun b -> b.Id) |> function
                     | [] -> 1
                     | ids -> List.max ids + 1)
                PendingExperience = [] }

    let reset (store: T) =
        EntityRegistry.reset ()
        InteractionBuilder.reset ()
        store.SessionStartMs <- None
        store.ZoneName <- ""
        store.Combatants.Clear()
        store.Interactions <- []
        store.ChatMessages <- []
        store.LootRecords <- []
        store.ItemUses <- []
        store.ExperienceRecords <- []
        store.FightState <- FightSegmenter.initial
        store.Battles <- []
        store.PendingOutgoing <- []
        store.RecentMsgBasicXp <- []
