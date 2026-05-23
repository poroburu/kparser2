namespace kparser2.Decoders

open System

module Trophy0xD3 =
    let private readName (data: byte[]) (offset: int) =
        if offset + 16 > data.Length then
            ""
        else
            data.[offset .. offset + 15]
            |> Array.takeWhile (fun b -> b <> 0uy)
            |> fun bytes -> System.Text.Encoding.UTF8.GetString bytes
            |> fun s -> s.Trim()

    let decode (data: byte[]) =
        try
            if data.Length < 60 then
                None
            else
                let highestId = BitConverter.ToUInt32(data, 4)
                let currentId = BitConverter.ToUInt32(data, 8)
                let highestLot = BitConverter.ToInt16(data, 14)
                let entryWord = BitConverter.ToUInt16(data, 16)
                let currentLot = BitConverter.ToInt16(data, 18)
                let poolSlot = int data.[20]
                let judgeFlag = int data.[21]
                let highestName = readName data 22
                let currentName = readName data 38

                let eventType =
                    match judgeFlag with
                    | 0 -> LootEventType.Lot
                    | 1 -> LootEventType.Won
                    | 3 -> LootEventType.Floor
                    | _ -> LootEventType.Lost

                let actorName =
                    if not (System.String.IsNullOrWhiteSpace currentName) then
                        currentName
                    elif not (System.String.IsNullOrWhiteSpace highestName) then
                        highestName
                    else
                        "Unknown"

                let lotValue =
                    if currentLot > 0s && currentLot <> -1s then
                        Some(int currentLot)
                    elif highestLot > 0s && highestLot <> -1s then
                        Some(int highestLot)
                    else
                        None

                let detail =
                    $"judge={judgeFlag} highest={highestId} current={currentId} entryIndex={entryWord &&& 0x7FFFus}"

                Some
                    { EventType = eventType
                      ItemId = 0
                      ItemName = $"Pool slot {poolSlot}"
                      Quantity = 1
                      Gil = 0
                      PoolSlot = poolSlot
                      ActorName = actorName
                      ActorId =
                          if currentId <> 0u then
                              Some(int currentId)
                          else
                              Some(int highestId)
                      LotValue = lotValue
                      Detail = detail }
        with _ ->
            None
