namespace kparser2.Protocol

open System
open System.Text.Json
open System.Text.Json.Serialization

[<CLIMutable>]
type PacketMetadata =
    { injected: bool
      blocked: bool
      chunk_size: uint32
      session_id: string
      sync_count: uint16 }

[<CLIMutable>]
type PacketMetaJson =
    { timestamp: uint64
      direction: string
      packet_type: string
      packet_id: uint16
      packet_name: string
      size: uint32
      metadata: PacketMetadata
      version: string option
      session_uuid: string option
      message_id: uint64 option }

module PacketMeta =
    let private options =
        JsonSerializerOptions(PropertyNameCaseInsensitive = true)

    let parse (json: ReadOnlySpan<byte>) =
        JsonSerializer.Deserialize<PacketMetaJson>(json, options)

    let parseString (json: string) =
        parse (ReadOnlySpan(System.Text.Encoding.UTF8.GetBytes(json)))

    let toEvent (topic: string) (meta: PacketMetaJson) (data: byte[]) =
        { Topic = topic
          Timestamp = meta.timestamp
          Direction = PacketEvent.directionFromString meta.direction
          PacketType = meta.packet_type
          PacketId = meta.packet_id
          PacketName = meta.packet_name
          Size = meta.size
          Injected = meta.metadata.injected
          Blocked = meta.metadata.blocked
          MessageId = defaultArg meta.message_id 0UL
          SessionUuid = defaultArg meta.session_uuid ""
          Version = defaultArg meta.version ""
          Data = data }
