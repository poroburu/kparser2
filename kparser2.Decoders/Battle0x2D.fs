namespace kparser2.Decoders

open System

/// GP_SERV_COMMAND_BATTLE_MESSAGE2 — XP / limit / merit / chain (XiPackets 0x002D).
/// Layout differs from 0x0029: ActIndex comes before Data/Data2.
module Battle0x2D =
    let decode (data: byte[]) =
        try
            if data.Length < 28 then
                None
            else
                Some
                    { CasterId = BitConverter.ToUInt32(data, 4)
                      TargetId = BitConverter.ToUInt32(data, 8)
                      CasterIndex = BitConverter.ToUInt16(data, 12)
                      TargetIndex = BitConverter.ToUInt16(data, 14)
                      Param1 = BitConverter.ToUInt32(data, 16)
                      Param2 = BitConverter.ToUInt32(data, 20)
                      MessageNum = BitConverter.ToUInt16(data, 24)
                      MessageType = data.[26] }
        with _ ->
            None
