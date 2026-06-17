namespace kparser2.Decoders

open System

module Battle0x29 =
    let decode (data: byte[]) =
        try
            if data.Length < 28 then
                None
            else
                Some
                    { CasterId = BitConverter.ToUInt32(data, 4)
                      TargetId = BitConverter.ToUInt32(data, 8)
                      Param1 = BitConverter.ToUInt32(data, 12)
                      Param2 = BitConverter.ToUInt32(data, 16)
                      CasterIndex = BitConverter.ToUInt16(data, 20)
                      TargetIndex = BitConverter.ToUInt16(data, 22)
                      MessageNum = BitConverter.ToUInt16(data, 24)
                      MessageType = data.[26] }
        with _ ->
            None
