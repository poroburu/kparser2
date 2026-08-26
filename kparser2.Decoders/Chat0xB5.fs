namespace kparser2.Decoders

open System

module Chat0xB5 =
    let decode (data: byte[]) =
        try
            let reader = BinaryReader.create data |> BinaryReader.payload

            if BinaryReader.remaining reader < 2 then
                None
            else
                let kind, reader = BinaryReader.u8 reader
                let _, reader = BinaryReader.u8 reader
                let mesBytes, reader = BinaryReader.nullTerminatedBytes reader

                let message =
                    let decoded = ChatCommon.decodeChatText mesBytes

                    if String.IsNullOrWhiteSpace decoded && BinaryReader.remaining reader > 0 then
                        let start = reader.Offset

                        data.[start .. data.Length - 1]
                        |> Array.takeWhile (fun b -> b <> 0uy)
                        |> ChatCommon.decodeChatText
                    else
                        decoded

                if String.IsNullOrWhiteSpace message then
                    None
                else
                    Some
                        { Mode = ChatCommon.modeLabel kind
                          ModeId = kind
                          IsGm = false
                          Speaker = ""
                          Message = message
                          ZoneId = None }
        with _ ->
            None
