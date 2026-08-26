namespace kparser2.Decoders

open System
open System.Text
open System.Text.RegularExpressions

module ChatCommon =
    let modeNames =
        Map.ofList
            [ 0x00, "Say"
              0x01, "Shout"
              0x03, "Tell"
              0x04, "Party"
              0x05, "Linkshell"
              0x06, "System"
              0x07, "System"
              0x08, "Emote"
              0x0C, "GM Prompt"
              0x0D, "Say"
              0x0E, "Shout"
              0x0F, "Party"
              0x10, "Linkshell"
              0x11, "Standard"
              0x12, "Standard"
              0x13, "Standard"
              0x14, "Standard"
              0x15, "Standard"
              0x16, "Standard"
              0x17, "Standard"
              0x18, "Say"
              0x19, "Say"
              0x1A, "Yell"
              0x1B, "Linkshell2"
              0x1C, "Linkshell2"
              0x1D, "Standard"
              0x1E, "Linkshell3"
              0x1F, "Linkshell3"
              0x20, "Standard"
              0x21, "Unity"
              0x22, "Assist"
              0x23, "Assist" ]

    let modeLabel kind =
        modeNames |> Map.tryFind kind |> Option.defaultValue $"Mode 0x{kind:X2}"

    /// s2c kinds displayed without a sender name (self echo) — XiPackets 0x0017.
    let namelessSelfKinds =
        Set.ofList [ 0x0D; 0x0E; 0x0F; 0x10; 0x1C; 0x1F ]

    /// s2c Say copy kinds that may carry the local player name.
    let sayCopyKinds = Set.ofList [ 0x18; 0x19 ]

    let isNamelessSelfKind kind = namelessSelfKinds.Contains kind

    let isSayCopyKind kind = sayCopyKinds.Contains kind

    let isGmAttr attr = (attr &&& 0x01) <> 0

    /// Expand FFXI Mes bytes: 0xFD auto-translate tokens and 0xEF 0x27/0x28 markers.
    /// Live HorizonXI yells use 6-byte FD … FD resource wrappers (VieweD FFXIEncoding).
    let decodeChatText (bytes: byte[]) =
        if bytes = null || bytes.Length = 0 then
            ""
        else
            let literal = ResizeArray<byte>()
            let sb = StringBuilder()

            let flush () =
                if literal.Count > 0 then
                    sb.Append(Encoding.UTF8.GetString(literal.ToArray())) |> ignore
                    literal.Clear()

            let mutable i = 0

            while i < bytes.Length do
                if bytes.[i] = 0xFDuy && i + 5 < bytes.Length && bytes.[i + 5] = 0xFDuy then
                    flush ()

                    let resourceId =
                        (uint32 bytes.[i + 1] <<< 24)
                        ||| (uint32 bytes.[i + 2] <<< 16)
                        ||| (uint32 bytes.[i + 3] <<< 8)
                        ||| uint32 bytes.[i + 4]

                    sb.Append(sprintf "[%08X]" resourceId) |> ignore
                    i <- i + 6
                elif
                    bytes.[i] = 0xEFuy
                    && i + 1 < bytes.Length
                    && bytes.[i + 1] >= 0x27uy
                    && bytes.[i + 1] <= 0x28uy
                then
                    flush ()
                    i <- i + 2
                else
                    literal.Add(bytes.[i])
                    i <- i + 1

            flush ()
            sb.ToString().Trim()

    let tryParseTellTarget (message: string) =
        let m = Regex("^>>(?<name>[A-Za-z0-9_]{3,16})\\b").Match(message)

        if m.Success then
            Some m.Groups.["name"].Value
        else
            None
