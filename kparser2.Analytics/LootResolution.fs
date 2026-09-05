namespace kparser2.Analytics

open System
open System.Collections.Generic

module LootResolution =
    type private PendingItem =
        { ItemName: string
          SourceName: string option }

    type ResolvedLoot =
        { Loot: LootRecord
          ItemName: string
          SourceName: string option }

    let isDistribution (loot: LootRecord) =
        loot.Detail.StartsWith("judge=", StringComparison.Ordinal)

    let private isPoolPlaceholder (name: string) =
        name.StartsWith("Pool slot ", StringComparison.OrdinalIgnoreCase)

    let private isZeroItemPlaceholder (loot: LootRecord) =
        loot.ItemId = 0
        && loot.ItemName.StartsWith("Item #", StringComparison.OrdinalIgnoreCase)

    let private hasItemFromPacket (loot: LootRecord) =
        loot.ItemId > 0 && not (isPoolPlaceholder loot.ItemName)

    let private isTerminalDistribution (loot: LootRecord) =
        isDistribution loot && not (loot.EventType.Equals("Lot", StringComparison.OrdinalIgnoreCase))

    let private queueFor (pending: Dictionary<int, Queue<PendingItem>>) slot =
        match pending.TryGetValue slot with
        | true, queue -> queue
        | _ ->
            let queue = Queue<PendingItem>()
            pending.[slot] <- queue
            queue

    /// Resolves 0x00D3 distribution rows against the preceding 0x00D2 item rows.
    /// The protocol repeats pool slot numbers as items are immediately distributed,
    /// so a FIFO per slot is required instead of a single last-seen value.
    let resolve (records: LootRecord list) =
        let pending = Dictionary<int, Queue<PendingItem>>()

        records
        |> List.map (fun loot ->
            if hasItemFromPacket loot then
                queueFor pending loot.PoolSlot
                |> fun queue ->
                    queue.Enqueue
                        { ItemName = loot.ItemName
                          SourceName = Some loot.ActorName }

            let itemName, sourceName =
                if isPoolPlaceholder loot.ItemName then
                    match pending.TryGetValue loot.PoolSlot with
                    | true, queue when queue.Count > 0 ->
                        let resolved = queue.Peek()

                        if isTerminalDistribution loot then
                            queue.Dequeue() |> ignore

                        resolved.ItemName, resolved.SourceName
                    | _ -> "Unknown item", None
                elif isZeroItemPlaceholder loot then
                    if loot.Gil > 0 then "Gil", Some loot.ActorName else "Unknown item", Some loot.ActorName
                else
                    loot.ItemName,
                    if isDistribution loot then None else Some loot.ActorName

            { Loot = loot
              ItemName = itemName
              SourceName = sourceName })

    /// Ignore 0x00D2 state-refresh rows that contain neither an item nor gil.
    let isMeaningful (resolved: ResolvedLoot) =
        (isDistribution resolved.Loot
         || resolved.Loot.ItemId > 0
         || resolved.Loot.Gil > 0)
