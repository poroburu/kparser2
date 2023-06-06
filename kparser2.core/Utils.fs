module kparser2.Core.Utils

let hexPairToChar hexPair =
    System.Convert.ToChar(System.Convert.ToInt32(hexPair, 16))
let hexToAscii (hexString: string) =
    hexString.Split(' ')
    |> Array.map hexPairToChar
    |> System.String.Concat

open System
open System.Text

let hexformatFile (hexString: string, length: int) =
    let hexTable = [| for x in 0x00 .. 0xFF -> sprintf "%02X" x |]
    let asciiTable = [| for x in 0x00 .. 0xFF -> 
                            if x >= 0x20 && x < 0x7F then string(Char.ConvertFromUtf32(x)) else "." |]

    let data = hexString.Split(' ') 
                        |> Array.map (fun byteStr -> System.Convert.ToByte(byteStr, 16))
                        |> Array.truncate length

    let stringBuilder = new StringBuilder()

    let topRow = "   |  0  1  2  3  4  5  6  7  8  9  A  B  C  D  E  F | 0123456789ABCDEF"
    let line = String.replicate ((16 + 1) * 3 + 2) "-" + "|-----------------"
    stringBuilder.AppendLine(topRow).AppendLine(line)

    for i in 0..((data.Length + 15) / 16) - 1 do
        let from = i * 16
        let upto = min ((i + 1) * 16) data.Length
        let chunk = data.[from .. upto - 1]

        stringBuilder.AppendFormat("{0,2} |", i) 
        for j in 0..15 do
            if j < chunk.Length then
                stringBuilder.AppendFormat(" {0}", hexTable.[int chunk.[j]])
            else
                stringBuilder.Append(" --") // Add '--' for missing bytes in hex

        stringBuilder.Append(" | ")

        for j in 0..15 do
            if j < chunk.Length then
                stringBuilder.Append(asciiTable.[int chunk.[j]])
            else
                stringBuilder.Append("-") // Add '--' for missing bytes in ascii
        stringBuilder.AppendLine()

    stringBuilder.ToString()
