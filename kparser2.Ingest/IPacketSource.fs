namespace kparser2.Ingest

open System
open System.Threading
open System.Threading.Channels

type IPacketSource =
    abstract member Packets: ChannelReader<kparser2.Protocol.PacketEvent>
    inherit IDisposable
