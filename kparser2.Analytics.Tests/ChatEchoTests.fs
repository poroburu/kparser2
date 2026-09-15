namespace kparser2.Analytics.Tests

open kparser2.Analytics
open Xunit

module ChatEchoTests =
    [<Fact>]
    let ``FoV gil is a recipient distribution without an invented source`` () =
        let record = { TimestampMs = 0L; EventType = "Won"; ItemId = 0; ItemName = "Gil"
                       Quantity = 0; Gil = 91; PoolSlot = -1; ActorName = "Alice"; Detail = "message=565" }
        Assert.True(LootResolution.isDistribution record)
        let resolved = LootResolution.resolve [record] |> List.head
        Assert.True(resolved.SourceName.IsNone)
        Assert.Equal("Alice", resolved.Loot.ActorName)

    let private row timestamp direction speaker local =
        { TimestampMs = timestamp; Mode = "Say"; ModeId = 0; IsGm = false
          Speaker = speaker; Message = "hello\u00fd world"; IsLocalPlayer = local
          PacketId = if direction = "outgoing" then 0xB5 else 0x17
          Direction = direction; TargetName = None }

    [<Fact>]
    let ``repeated incoming and outgoing messages are preserved`` () =
        for direction in ["incoming"; "outgoing"] do
            let a = row 100L direction "Alice" true
            let b = row 110L direction "Alice" true
            Assert.Equal(2, (ChatIngest.appendChat [a] b).Length)

    [<Fact>]
    let ``echo consumes one outgoing record and preserves ordering and repeated incoming`` () =
        let sent = row 100L "outgoing" "Alice" true
        let other = row 110L "incoming" "Bob" false
        let echo = row 120L "incoming" "Alice" true
        let merged = ChatIngest.appendChat [other; sent] echo
        Assert.Equal<ChatMessageRecord list>([echo; other], merged)
        Assert.Equal(3, (ChatIngest.appendChat merged {echo with TimestampMs = 130L}).Length)

    [<Fact>]
    let ``different speakers channels late or reverse echoes do not merge`` () =
        let sent = row 100L "outgoing" "Alice" true
        for incoming in [row 110L "incoming" "Bob" false
                         row 601L "incoming" "Alice" true
                         row 99L "incoming" "Alice" true
                         {row 110L "incoming" "Alice" true with Mode = "Party"}] do
            Assert.Equal(2, (ChatIngest.appendChat [sent] incoming).Length)

    [<Theory>]
    [<InlineData(1,31,1,1,"Harm","Melee")>]
    [<InlineData(6,102,0,100,"Aid","")>]
    [<InlineData(11,186,0,92,"Aid","")>]
    [<InlineData(11,242,0,148,"Harm","Enfeeble")>]
    [<InlineData(11,277,0,5,"Harm","Enfeeble")>]
    [<InlineData(11,264,0,73,"Harm","Ability")>]
    [<InlineData(4,106,0,0,"Unknown","")>]
    let ``captured status parameters are not damage`` (command: int, message: int, miss: int, value: int, kind: string, harm: string) =
        let interaction, damage, _ = BattleMessageCatalog.classifyActionEffect command message miss value
        Assert.Equal(kind, string interaction)
        Assert.Equal(harm, damage |> Option.map string |> Option.defaultValue "")

    [<Fact>]
    let ``action namespaces cannot resolve to unrelated abilities`` () =
        Assert.Equal("Rampage", BattleMessageCatalog.actionName 3 69 185)
        Assert.Equal("Lamb Chop", BattleMessageCatalog.actionName 11 3857 185)
        Assert.Equal("weaponskill-9999", BattleMessageCatalog.actionName 3 9999 185)
        Assert.Equal("monster-skill-9999", BattleMessageCatalog.actionName 11 9999 185)
