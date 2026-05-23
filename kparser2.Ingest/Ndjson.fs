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

module Ndjson =
    let private options = JsonSerializerOptions(PropertyNameCaseInsensitive = true)

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
                Some(decode line))
