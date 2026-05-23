namespace kparser2.Decoders

open System
open System.Text

[<Struct>]
type BinaryReader =
    { Data: byte[]
      Offset: int }

module BinaryReader =
    let create (data: byte[]) = { Data = data; Offset = 0 }

    let payload (reader: BinaryReader) =
        if reader.Data.Length >= 4 then
            { reader with Offset = 4 }
        else
            reader

    let remaining (reader: BinaryReader) =
        max 0 (reader.Data.Length - reader.Offset)

    let ensure (reader: BinaryReader) (size: int) =
        if reader.Offset + size > reader.Data.Length then
            failwith $"Packet too short at offset {reader.Offset}, need {size} bytes"
        else
            reader

    let skip (reader: BinaryReader) (count: int) =
        ensure reader count |> fun r -> { r with Offset = r.Offset + count }

    let u8 (reader: BinaryReader) =
        let r = ensure reader 1
        (int r.Data.[r.Offset], { r with Offset = r.Offset + 1 })

    let u16 (reader: BinaryReader) =
        let r = ensure reader 2
        (BitConverter.ToUInt16(r.Data, r.Offset), { r with Offset = r.Offset + 2 })

    let i16 (reader: BinaryReader) =
        let r = ensure reader 2
        (BitConverter.ToInt16(r.Data, r.Offset), { r with Offset = r.Offset + 2 })

    let u32 (reader: BinaryReader) =
        let r = ensure reader 4
        (BitConverter.ToUInt32(r.Data, r.Offset), { r with Offset = r.Offset + 4 })

    let fixedString (reader: BinaryReader) (length: int) =
        let r = ensure reader length
        let bytes = r.Data.[r.Offset .. r.Offset + length - 1]

        let text =
            bytes
            |> Array.takeWhile (fun b -> b <> 0uy)
            |> fun trimmed -> Encoding.UTF8.GetString(trimmed)

        (text.Trim(), { r with Offset = r.Offset + length })

    let nullTerminatedString (reader: BinaryReader) =
        let start = reader.Offset

        let idx =
            reader.Data
            |> Array.skip reader.Offset
            |> Array.tryFindIndex ((=) 0uy)
            |> function
                | Some i -> reader.Offset + i
                | None -> reader.Data.Length

        let bytes =
            if idx > start then
                reader.Data.[start .. idx - 1]
            else
                Array.empty

        let text = Encoding.UTF8.GetString bytes

        let nextOffset =
            if idx < reader.Data.Length then
                idx + 1
            else
                reader.Data.Length

        (text.Trim(), { reader with Offset = nextOffset })

    let readAt (data: byte[]) (offset: int) (size: int) =
        if offset + size > data.Length then
            Array.empty
        else
            data.[offset .. offset + size - 1]
