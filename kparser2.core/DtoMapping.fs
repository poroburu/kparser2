namespace kparser2.Core

open kparser2.Abstractions
open kparser2.Protocol

module DtoMapping =
    let toPacketRow (evt: PacketEvent) =
        PacketRowDto(
            Topic = evt.Topic,
            Timestamp = evt.Timestamp,
            Direction = PacketEvent.directionToString evt.Direction,
            PacketType = evt.PacketType,
            PacketId = int evt.PacketId,
            PacketName = evt.PacketName,
            Size = int evt.Size,
            Injected = evt.Injected,
            Blocked = evt.Blocked,
            MessageId = evt.MessageId,
            Data = evt.Data
        )

    let toChatEvent (evt: PacketEvent) (speaker: string) (message: string) =
        ChatEventDto(
            Timestamp = evt.Timestamp,
            Direction = PacketEvent.directionToString evt.Direction,
            PacketId = int evt.PacketId,
            Speaker = speaker,
            Message = message
        )

    let toLootEvent (evt: PacketEvent) (itemName: string) (source: string) (detail: string) =
        LootEventDto(
            Timestamp = evt.Timestamp,
            PacketId = int evt.PacketId,
            ItemName = itemName,
            Source = source,
            Detail = detail
        )
