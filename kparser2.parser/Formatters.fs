module kparser2.Parser.Formatters

open System
open MessagePack
open MessagePack.Formatters

[<MessagePackObject>]
type PacketFrame =
    { [<Key("id")>]
      id: int
      [<Key("size")>]
      size: int
      [<Key("data")>]
      data:string}

type PacketFrameFormatter() =
    interface IMessagePackFormatter<PacketFrame> with
        member this.Serialize(writer: byref<MessagePackWriter>, value: PacketFrame, options: MessagePackSerializerOptions) =
            // Throw an exception since serialization is not supported
            raise (NotSupportedException("Serialization is not supported."))
        member this.Deserialize(reader: byref<MessagePackReader>, options: MessagePackSerializerOptions) =
            let mapCount = reader.ReadMapHeader() // Read map header
            let mutable id = 0
            let mutable size = 0
            let mutable data = Array.empty
            for _ in 0 .. mapCount - 1 do
                match reader.ReadString() with
                | "id" -> id <- reader.ReadInt32()
                | "size" -> size <- reader.ReadInt32()
                | "data" ->
                    let intArray = reader.ReadArrayHeader()
                    data <- Array.zeroCreate intArray
                    for i in 0 .. (intArray - 1) do
                        data.[i] <- byte (reader.ReadInt32()) // Read as int and convert to byte
                | _ -> ()
                
            { id = id; size = size; data = BitConverter.ToString(data).Replace("-"," ")  }

BitConverter.ToString
type PacketFrameFormatterResolver private() =
    static let instance = PacketFrameFormatterResolver()

    static member Instance = instance

    interface IFormatterResolver with
        member this.GetFormatter<'T>() =
            if typeof<'T> = typeof<PacketFrame> then
                (PacketFrameFormatter() :> obj) :?> IMessagePackFormatter<'T>
            else
                null
