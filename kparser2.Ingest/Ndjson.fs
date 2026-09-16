namespace kparser2.Ingest

open System
open System.IO
open System.Text
open System.Text.Json

[<CLIMutable>]
type NdjsonRecord =
    { topic: string
      meta: string
      data_b64: string }

[<CLIMutable>]
type NdjsonSessionHeader =
    { ``type``: string
      player_name: string
      record_start_ms: int64
      boundary_mode: string
      boundary_quality: string
      boundary_reason: string
      boundary_session_uuid: string
      boundary_message_id: uint64 }

module Ndjson =
    let private options = JsonSerializerOptions(PropertyNameCaseInsensitive = true)

    let sessionHeaderType = "kparser2.session"

    let tryReadSessionHeader (path: string) =
        try
            use reader = new StreamReader(path)

            let line = reader.ReadLine()

            if String.IsNullOrWhiteSpace line then
                None
            else
                let header = JsonSerializer.Deserialize<NdjsonSessionHeader>(line, options)

                if header.``type`` = sessionHeaderType then
                    Some header
                else
                    None
        with _ ->
            None

    let tryPlayerName (path: string) =
        match tryReadSessionHeader path with
        | Some header when not (String.IsNullOrWhiteSpace header.player_name) ->
            Some header.player_name
        | _ -> None

    let writeSessionHeader (writer: TextWriter) (playerName: string option) (recordStartMs: int64) =
        let header =
            { ``type`` = sessionHeaderType
              player_name = playerName |> Option.defaultValue ""
              record_start_ms = recordStartMs
              boundary_mode = "none"
              boundary_quality = "unavailable"
              boundary_reason = "no reset boundary"
              boundary_session_uuid = ""
              boundary_message_id = 0UL }

        writer.WriteLine(JsonSerializer.Serialize(header, options))

    let writeSessionHeaderWithBoundary
        (writer: TextWriter)
        (playerName: string option)
        (recordStartMs: int64)
        (boundarySessionUuid: string)
        (boundaryMessageId: uint64)
        (boundaryMode: string)
        (boundaryQuality: string)
        (boundaryReason: string) =
        let header =
            { ``type`` = sessionHeaderType
              player_name = playerName |> Option.defaultValue ""
              record_start_ms = recordStartMs
              boundary_mode = boundaryMode
              boundary_quality = boundaryQuality
              boundary_reason = boundaryReason
              boundary_session_uuid = boundarySessionUuid
              boundary_message_id = boundaryMessageId }

        writer.WriteLine(JsonSerializer.Serialize(header, options))

    let encode (topic: string) (metaJson: string) (data: byte[]) =
        let record =
            { topic = topic
              meta = metaJson
              data_b64 = Convert.ToBase64String(data) }

        JsonSerializer.Serialize(record, options)

    let decode (line: string) =
        let record = JsonSerializer.Deserialize<NdjsonRecord>(line, options)
        record.topic, record.meta, Convert.FromBase64String(record.data_b64)

    let writeLine (writer: TextWriter) topic metaJson (data: byte[]) =
        writer.WriteLine(encode topic metaJson data)

    let readAll (path: string) =
        File.ReadLines(path)
        |> Seq.choose (fun line ->
            if String.IsNullOrWhiteSpace line then
                None
            else
                try
                    let doc = JsonDocument.Parse(line)
                    let root = doc.RootElement
                    let mutable headerType = Unchecked.defaultof<JsonElement>

                    if
                        root.TryGetProperty("type", &headerType)
                        && headerType.GetString() = sessionHeaderType
                    then
                        None
                    else
                        Some(decode line)
                with _ ->
                    None)
