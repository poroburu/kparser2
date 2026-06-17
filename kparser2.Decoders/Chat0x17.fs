namespace kparser2.Decoders

open System

module Chat0x17 =
    let decode (data: byte[]) =
        try
            let reader = BinaryReader.create data |> BinaryReader.payload

            if BinaryReader.remaining reader < 4 then
                None
            else
                let kind, reader = BinaryReader.u8 reader
                let attr, reader = BinaryReader.u8 reader
                let zoneId, reader = BinaryReader.u16 reader
                let speaker, reader = BinaryReader.fixedString reader 15
                let message, reader = BinaryReader.nullTerminatedString reader

                let message =
                    if String.IsNullOrWhiteSpace message && BinaryReader.remaining reader > 0 then
                        let start = reader.Offset

                        data.[start .. data.Length - 1]
                        |> Array.takeWhile (fun b -> b <> 0uy)
                        |> fun bytes -> System.Text.Encoding.UTF8.GetString bytes
                        |> fun text -> text.Trim()
                    else
                        message

                if String.IsNullOrWhiteSpace message && String.IsNullOrWhiteSpace speaker then
                    None
                else
                    let speakerLabel =
                        if String.IsNullOrWhiteSpace speaker then
                            ""
                        else
                            speaker

                    Some
                        { Mode = ChatCommon.modeLabel kind
                          ModeId = kind
                          IsGm = attr <> 0
                          Speaker = speakerLabel
                          Message = message
                          ZoneId =
                              if zoneId = 0us then
                                  None
                              else
                                  Some(int zoneId) }
        with _ ->
            None
