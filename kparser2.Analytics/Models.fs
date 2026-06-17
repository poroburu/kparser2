namespace kparser2.Analytics

type InteractionType =
    | Harm
    | Aid
    | Death
    | Unknown

type HarmType =
    | Melee
    | Ranged
    | Spell
    | Weaponskill
    | Enfeeble
    | Ability
    | Other

type AidType =
    | Recovery
    | Enhance
    | Item
    | RemoveEnmity
    | Unknown

type EntityKind =
    | Player
    | Mob
    | Pet
    | Fellow
    | Unknown

type DamageModifier =
    | Normal
    | Critical
    | MagicBurst
    | Unknown

type InteractionCategory =
    | Melee
    | MeleeCrit
    | Ranged
    | RangedCrit
    | Spell
    | Ability
    | Weaponskill
    | Skillchain
    | Enfeeble
    | OtherPhysical
    | OtherMagical
    | Recovery
    | Enhance
    | Death
    | Other

type Combatant =
    { Id: uint32
      Name: string
      Kind: EntityKind
      Job: string
      PlayerInfo: string option }

type Battle =
    { Id: int
      EnemyName: string
      EnemyId: uint32 option
      StartMs: int64
      EndMs: int64 option
      Killed: bool
      KillerId: uint32 option
      ExperiencePoints: int
      ExperienceChain: int }

type Interaction =
    { Id: int
      BattleId: int option
      TimestampMs: int64
      InteractionType: InteractionType
      HarmType: HarmType option
      AidType: AidType option
      Category: InteractionCategory
      DamageModifier: DamageModifier
      ActorId: uint32
      TargetId: uint32
      ActorName: string
      TargetName: string
      ActionName: string
      Value: int
      Success: string
      CommandNo: int
      MessageId: int
      IsProc: bool
      ProcValue: int
      IsLocalPlayerActor: bool
      IsLocalPlayerTarget: bool }

type ChatMessageRecord =
    { TimestampMs: int64
      Mode: string
      ModeId: int
      IsGm: bool
      Speaker: string
      Message: string
      PacketId: int
      Direction: string
      IsLocalPlayer: bool
      TargetName: string option }

type LootRecord =
    { TimestampMs: int64
      EventType: string
      ItemId: int
      ItemName: string
      Quantity: int
      Gil: int
      PoolSlot: int
      ActorName: string
      Detail: string }

type ItemUseRecord =
    { TimestampMs: int64
      ActorId: uint32
      ActorName: string
      ItemId: int
      ItemName: string
      Quantity: int }

type ExperienceRecord =
    { TimestampMs: int64
      ActorId: uint32
      ActorName: string
      ExperiencePoints: int
      Chain: int
      BattleId: int option }

type AnalyticsSnapshot =
    { SessionStartMs: int64
      ZoneName: string
      Combatants: Combatant list
      Battles: Battle list
      Interactions: Interaction list
      ChatMessages: ChatMessageRecord list
      LootRecords: LootRecord list
      ItemUses: ItemUseRecord list
      ExperienceRecords: ExperienceRecord list }

module AnalyticsSnapshot =
    let empty =
        { SessionStartMs = 0L
          ZoneName = ""
          Combatants = []
          Battles = []
          Interactions = []
          ChatMessages = []
          LootRecords = []
          ItemUses = []
          ExperienceRecords = [] }
