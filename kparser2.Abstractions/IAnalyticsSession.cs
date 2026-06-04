namespace kparser2.Abstractions;

public interface IAnalyticsSession : IPacketSession
{
    IObservable<AnalyticsSnapshotDto> Analytics { get; }
    AnalyticsSnapshotDto GetSnapshot();
    void LoadSnapshot(AnalyticsSnapshotDto snapshot);
}
