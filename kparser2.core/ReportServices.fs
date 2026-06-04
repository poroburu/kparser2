namespace kparser2.Core

open System.Threading
open System.Threading.Tasks
open kparser2.Abstractions
open kparser2.Analytics

type FileReportExporter() =
    interface IReportExporter with
        member _.ExportAsync(path, snapshot, title, _ct) =
            task {
                let snap = AnalyticsDtoMapping.fromSnapshotDto snapshot
                let bundle = ReportInterchange.fromSnapshot title snap
                ReportInterchange.exportToFile path bundle
            }

type FileReportImporter() =
    interface IReportImporter with
        member _.ImportAsync(path, _ct) =
            task {
                let bundle = ReportInterchange.importFromFile path
                let snap = ReportInterchange.toSnapshot bundle
                return AnalyticsDtoMapping.toSnapshotDto snap
            }

        member _.ValidateAsync(path, _ct) =
            task {
                try
                    ReportInterchange.importFromFile path |> ignore
                    return true
                with _ ->
                    return false
            }
