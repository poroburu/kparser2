namespace kparser2.Core

open System.Collections.Generic
open kparser2.Abstractions
open kparser2.Protocol

type PacketStore(maxEntries: int) =
    let lockObj = obj ()
    let packets = Queue<PacketRowDto>()
    let chatEvents = Queue<ChatEventDto>()
    let lootEvents = Queue<LootEventDto>()
    let mutable totalPackets = 0L
    let mutable selected: PacketRowDto option = None

    member _.MaxEntries = maxEntries

    member _.Add(evt: PacketEvent) =
        let row = DtoMapping.toPacketRow evt
        let result = Transforms.run evt

        lock lockObj (fun () ->
            totalPackets <- totalPackets + 1L
            packets.Enqueue row

            while packets.Count > maxEntries do
                packets.Dequeue() |> ignore

            for chat in result.ChatEvents do
                chatEvents.Enqueue(DtoMapping.toChatEvent evt chat.Speaker chat.Message)

                while chatEvents.Count > maxEntries do
                    chatEvents.Dequeue() |> ignore

            for loot in result.LootEvents do
                lootEvents.Enqueue(DtoMapping.toLootEvent evt loot.ItemName loot.Source loot.Detail)

                while lootEvents.Count > maxEntries do
                    lootEvents.Dequeue() |> ignore

            selected <- Some row)

        row, result

    member _.GetRecent(count: int) =
        lock lockObj (fun () ->
            packets
            |> Seq.rev
            |> Seq.truncate count
            |> Seq.toList)

    member _.GetChatEvents(count: int) =
        lock lockObj (fun () ->
            chatEvents
            |> Seq.rev
            |> Seq.truncate count
            |> Seq.toList)

    member _.GetLootEvents(count: int) =
        lock lockObj (fun () ->
            lootEvents
            |> Seq.rev
            |> Seq.truncate count
            |> Seq.toList)

    member _.TotalPackets =
        lock lockObj (fun () -> totalPackets)

    member _.Selected
        with get () = lock lockObj (fun () -> selected)
        and set value = lock lockObj (fun () -> selected <- value)

    member _.ChatCount =
        lock lockObj (fun () -> int64 chatEvents.Count)

    member _.LootCount =
        lock lockObj (fun () -> int64 lootEvents.Count)
