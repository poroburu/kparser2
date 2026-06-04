namespace kparser2.Abstractions;

public interface IReportExporter
{
    Task ExportAsync(string path, AnalyticsSnapshotDto snapshot, string title, CancellationToken cancellationToken = default);
}

public interface IReportImporter
{
    Task<AnalyticsSnapshotDto> ImportAsync(string path, CancellationToken cancellationToken = default);
    Task<bool> ValidateAsync(string path, CancellationToken cancellationToken = default);
}

/// <summary>
/// Optional future plugin for kparser .sdf/.kps import. Not shipped in Phase 7.
/// </summary>
public interface ILegacyParseImporter
{
    bool CanImport(string path);
    Task<AnalyticsSnapshotDto> ImportAsync(string path, CancellationToken cancellationToken = default);
}

/// <summary>
/// Stub for future horizonxilogs upload.
/// </summary>
public interface IReportPublisher
{
    Task<string?> PublishAsync(ReportBundleDto bundle, CancellationToken cancellationToken = default);
}
