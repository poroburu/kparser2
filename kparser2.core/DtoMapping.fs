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

    let toChatEvent (evt: PacketEvent) (chat: ChatExtract) =
        ChatEventDto(
            Timestamp = evt.Timestamp,
            Direction = PacketEvent.directionToString evt.Direction,
            PacketId = int evt.PacketId,
            Mode = chat.Mode,
            IsGm = chat.IsGm,
            Speaker = chat.Speaker,
            Message = chat.Message
        )

    let toLootEvent (evt: PacketEvent) (loot: LootExtract) =
        LootEventDto(
            Timestamp = evt.Timestamp,
            PacketId = int evt.PacketId,
            EventType = loot.EventType,
            ItemId = loot.ItemId,
            ItemName = loot.ItemName,
            Quantity = loot.Quantity,
            Gil = loot.Gil,
            PoolSlot = loot.PoolSlot,
            ActorName = loot.ActorName,
            Source = loot.Source,
            Detail = loot.Detail
        )

    let toCombatEvent (evt: PacketEvent) (combat: CombatExtract) =
        CombatEventDto(
            Timestamp = evt.Timestamp,
            PacketId = int evt.PacketId,
            EventKind = combat.EventKind,
            ActorId = combat.ActorId,
            TargetId = combat.TargetId,
            CommandNo = combat.CommandNo,
            CommandArg = combat.CommandArg,
            MessageNum = combat.MessageNum,
            MessageType = combat.MessageType,
            Param1 = combat.Param1,
            Param2 = combat.Param2,
            Miss = combat.Miss,
            Damage = combat.Damage,
            MessageId = combat.MessageId,
            Summary = combat.Summary
        )
