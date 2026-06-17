namespace kparser2.Decoders

open System

module Trophy0xD2 =
    let decode (data: byte[]) =
        try
            if data.Length < 60 then
                None
            else
                let itemCount = int (BitConverter.ToUInt32(data, 4))
                let dropperId = BitConverter.ToUInt32(data, 8)
                let gil = BitConverter.ToUInt16(data, 12)
                let itemId = int (BitConverter.ToUInt16(data, 16))
                let poolSlot = int data.[20]
                let entry = int data.[21]
                let isContainer = data.[22] <> 0uy

                let eventType =
                    match entry with
                    | 1 -> LootEventType.Pass
                    | 2 -> LootEventType.Lot
                    | _ -> LootEventType.Found

                let itemName = ItemLookup.getName itemId

                let detail =
                    $"dropper={dropperId} container={isContainer} entry={entry}"

                Some
                    { EventType = eventType
                      ItemId = itemId
                      ItemName = itemName
                      Quantity = max 1 itemCount
                      Gil = int gil
                      PoolSlot = poolSlot
                      ActorName = ""
                      ActorId = Some(int dropperId)
                      LotValue = None
                      Detail = detail }
        with _ ->
            None
