namespace kparser2.Protocol

open System
open System.Text

module Hex =
    let format (data: byte[]) =
        let sb = StringBuilder(data.Length * 3)

        for i in 0 .. data.Length - 1 do
            if i > 0 then
                sb.Append(' ') |> ignore

            sb.Append(data.[i].ToString("X2")) |> ignore

        sb.ToString()

    let formatDump (data: byte[]) =
        let hexTable = [| for x in 0x00 .. 0xFF -> sprintf "%02X" x |]

        let asciiTable =
            [| for x in 0x00 .. 0xFF ->
                   if x >= 0x20 && x < 0x7F then
                       string (Char.ConvertFromUtf32 x)
                   else
                       "." |]

        let sb = StringBuilder()
        let topRow = "   |  0  1  2  3  4  5  6  7  8  9  A  B  C  D  E  F | 0123456789ABCDEF"
        let line = String.replicate ((16 + 1) * 3 + 2) "-" + "|-----------------"
        sb.AppendLine(topRow).AppendLine(line) |> ignore

        let rowCount = (data.Length + 15) / 16

        for i in 0 .. rowCount - 1 do
            let from = i * 16
            let upto = min ((i + 1) * 16) data.Length
            let chunk = data.[from .. upto - 1]
            sb.Append(sprintf "%2d |" i) |> ignore

            for j in 0 .. 15 do
                if j < chunk.Length then
                    sb.Append(sprintf " %s" hexTable.[int chunk.[j]]) |> ignore
                else
                    sb.Append(" --") |> ignore

            sb.Append(" | ") |> ignore

            for j in 0 .. 15 do
                if j < chunk.Length then
                    sb.Append(asciiTable.[int chunk.[j]]) |> ignore
                else
                    sb.Append("-") |> ignore

            sb.AppendLine() |> ignore

        sb.ToString()
