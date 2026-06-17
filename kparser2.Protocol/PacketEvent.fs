namespace kparser2.Protocol

type PacketDirection =
    | Incoming
    | Outgoing

type PacketEvent =
    { Topic: string
      Timestamp: uint64
      Direction: PacketDirection
      PacketType: string
      PacketId: uint16
      PacketName: string
      Size: uint32
      Injected: bool
      Blocked: bool
      MessageId: uint64
      SessionUuid: string
      Version: string
      Data: byte[] }

module PacketEvent =
    let directionToString =
        function
        | Incoming -> "incoming"
        | Outgoing -> "outgoing"

    let directionFromString (value: string) =
        if value = "incoming" then Incoming else Outgoing
