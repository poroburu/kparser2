namespace kparser2.Analytics

open System

type ReportSpan =
    { Text: string
      Bold: bool
      Underline: bool
      Color: string }

type AnalyticsReport = { Spans: ReportSpan list }

module ReportColors =
    let title = "#FF0000"
    let black = "#000000"
    let purple = "#800080"
    let gray = "#808080"
    let orange = "#FFA500"
    let magenta = "#FF00FF"
    let blue = "#0000FF"
    let green = "#008000"
    let indigo = "#4B0082"

module TimeSpanFormat =
    let formatMs (ms: int64) (forceIncludeHours: bool) =
        let totalSeconds = max 0 (int (Math.Round(float ms / 1000.0)))
        let hours = totalSeconds / 3600
        let minutes = (totalSeconds % 3600) / 60
        let seconds = totalSeconds % 60

        if forceIncludeHours || hours > 0 then
            sprintf "%d:%02d:%02d" hours minutes seconds
        else
            sprintf "%d:%02d" minutes seconds

    let formatMsDefault ms = formatMs ms false

module ReportBuilder =
    let empty = { Spans = [] }

    let private span text bold underline color =
        { Text = text; Bold = bold; Underline = underline; Color = color }

    let private append report s = { report with Spans = report.Spans @ [ s ] }

    let appendRaw text report = append report (span text false false ReportColors.black)

    let appendLine text report = appendRaw (text + "\n") report

    let appendTitle text report =
        append report (span (text + "\n") true false ReportColors.title)
        |> appendRaw "\n"

    let appendHeader text report =
        append report (span (text + "\n") true true ReportColors.black)

    let appendStyled text bold underline color report =
        append report (span text bold underline color)

    let appendFormat (fmt: string) (args: obj[]) report =
        appendRaw (String.Format(fmt, args)) report

    let appendFormatLine (fmt: string) (args: obj[]) report =
        appendFormat fmt args report |> appendRaw "\n"

    let blankLine report = appendRaw "\n" report

    let toPlainText (report: AnalyticsReport) =
        report.Spans |> List.map (fun s -> s.Text) |> String.Concat
