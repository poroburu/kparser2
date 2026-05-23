namespace kparser2.Decoders

type ChatDecoded =
    { Mode: string
      ModeId: int
      IsGm: bool
      Speaker: string
      Message: string
      ZoneId: int option }

type LootEventType =
    | Found
    | Lot
    | Pass
    | Won
    | Floor
    | Lost

type LootDecoded =
    { EventType: LootEventType
      ItemId: int
      ItemName: string
      Quantity: int
      Gil: int
      PoolSlot: int
      ActorName: string
      ActorId: int option
      LotValue: int option
      Detail: string }

type CombatMessageDecoded =
    { CasterId: uint32
      TargetId: uint32
      CasterIndex: uint16
      TargetIndex: uint16
      MessageNum: uint16
      MessageType: byte
      Param1: uint32
      Param2: uint32 }

type CombatEffectDecoded =
    { Miss: int
      Kind: int
      SubKind: int
      Param: int
      MessageId: int
      Value: int
      HasProc: bool
      ProcValue: int
      HasReact: bool
      ReactValue: int }

type CombatTargetDecoded =
    { TargetId: uint32
      Effects: CombatEffectDecoded list }

type CombatActionDecoded =
    { ActorId: uint32
      CommandNo: int
      CommandArg: uint32
      Info: uint32
      Targets: CombatTargetDecoded list }

type DecoderEvent =
    | Chat of ChatDecoded
    | Loot of LootDecoded
    | CombatMessage of CombatMessageDecoded
    | CombatAction of CombatActionDecoded

type DecoderResult = { Events: DecoderEvent list }

module DecoderResult =
    let empty = { Events = [] }

    let singleton event = { Events = [ event ] }

    let merge left right =
        { Events = left.Events @ right.Events }
