namespace kparser2.Analytics

module LegacyReportStubs =
    [<Literal>]
    let abysseaMessage =
        "Abyssea is unsupported legacy content on HorizonXI. Implement it only if HorizonXI adds Abyssea content to its server."

    // Keep old report/query identifiers deterministic without presenting
    // experience points or kills as Abyssea light, chest, or cruor data.
    let abyssea (_snap: AnalyticsSnapshot) (_filter: MobFilter) =
        ReportBuilder.empty
        |> ReportBuilder.appendTitle "Abyssea (unsupported)"
        |> ReportBuilder.appendLine abysseaMessage

    let abysseaRows =
        [ { Label = "Abyssea"
            Value = "Unsupported legacy stub"
            Count = 0
            Total = 0 } ]
