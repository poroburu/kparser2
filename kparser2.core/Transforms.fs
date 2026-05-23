namespace kparser2.Core

open System
open System.Text
open kparser2.Protocol

type ChatExtract = { Speaker: string; Message: string }

type LootExtract = { ItemName: string; Source: string; Detail: string }

type TransformResult =
    { ChatEvents: ChatExtract list
      LootEvents: LootExtract list }

module Transforms =
    let private readAscii (data: byte[]) (offset: int) (maxLen: int) =
        if offset >= data.Length then
            ""
        else
            let len = min maxLen (data.Length - offset)

            data.[offset .. offset + len - 1]
            |> Array.filter (fun b -> b >= 32uy && b < 127uy)
            |> Array.map char
            |> String

    let extractChat (evt: PacketEvent) =
        // Placeholder heuristics until opcode-specific decoders land.
        if evt.PacketName.Contains("CHAT", StringComparison.OrdinalIgnoreCase)
           || evt.PacketId = 0x0017us then
            let text = readAscii evt.Data 4 120

            if String.IsNullOrWhiteSpace text then
                []
            else
                [ { Speaker = "unknown"
                    Message = text.Trim() } ]
        else
            []

    let extractLoot (evt: PacketEvent) =
        if evt.PacketName.Contains("ITEM", StringComparison.OrdinalIgnoreCase)
           || evt.PacketName.Contains("LOOT", StringComparison.OrdinalIgnoreCase)
           || evt.PacketId = 0x00D2us
           || evt.PacketId = 0x00D3us then
            let detail = readAscii evt.Data 0 120

            if String.IsNullOrWhiteSpace detail then
                []
            else
                [ { ItemName = detail.Trim()
                    Source = evt.PacketName
                    Detail = Hex.format evt.Data } ]
        else
            []

    let run (evt: PacketEvent) =
        { ChatEvents = extractChat evt
          LootEvents = extractLoot evt }
