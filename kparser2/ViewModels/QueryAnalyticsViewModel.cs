using System.Reactive.Disposables;
using System.Windows.Documents;
using CommunityToolkit.Mvvm.ComponentModel;
using kparser2.Abstractions;
using kparser2.Core;
using kparser2.Services;

namespace kparser2.ViewModels;

public sealed partial class QueryAnalyticsViewModel : ObservableObject, IDisposable
{
    private readonly IAnalyticsSession _session;
    private readonly string _queryId;
    private readonly MobFilterService? _mobFilter;
    private readonly IDisposable _analyticsSubscription;
    private readonly IDisposable? _filterSubscription;
    private int _refreshGeneration;
    private bool _disposed;
    private AnalyticsReportRequest? _request;

    public void SetRequest(AnalyticsReportRequest request)
    {
        _request = request;
        Refresh(_session.GetSnapshot());
    }

    [ObservableProperty]
    private FlowDocument _reportDocument = new();

    [ObservableProperty]
    private string _statusText = "";

    public QueryAnalyticsViewModel(
        IAnalyticsSession session,
        string queryId,
        MobFilterService? mobFilter = null)
    {
        _session = session;
        _queryId = queryId;
        _mobFilter = mobFilter;

        Refresh(_session.GetSnapshot());

        _analyticsSubscription = session.Analytics.Subscribe(snapshot =>
            UiThread.RunBackground(() => Refresh(snapshot)));

        if (_mobFilter is not null)
        {
            void OnFilterChanged() => UiThread.Run(() => Refresh(_session.GetSnapshot()));
            _mobFilter.FilterChanged += OnFilterChanged;
            _filterSubscription = Disposable.Create(() => _mobFilter.FilterChanged -= OnFilterChanged);
        }
    }

    private void Refresh(AnalyticsSnapshotDto snapshot)
    {
        if (_disposed) return;
        var generation = Interlocked.Increment(ref _refreshGeneration);
        var filter = _mobFilter?.Current ?? new MobFilterDto();
        var queryId = _queryId;

        var request = _request ?? new AnalyticsReportRequest { QueryId = queryId, Filter = filter };
        Task.Run(() => AnalyticsReportService.formatRequest(request, snapshot))
            .ContinueWith(
                t =>
                {
                    if (_disposed || generation != Volatile.Read(ref _refreshGeneration))
                    {
                        return;
                    }

                    if (t.IsFaulted)
                    {
                        UiThread.RunBackground(() => { if (!_disposed) StatusText = "Report failed: " + t.Exception?.GetBaseException().Message; });
                        return;
                    }
                    var report = t.Result;
                    UiThread.RunBackground(() => ApplyReport(generation, report, snapshot));
                },
                TaskScheduler.Default);
    }

    private void ApplyReport(int generation, AnalyticsReportDto report, AnalyticsSnapshotDto snapshot)
    {
        if (_disposed || generation != Volatile.Read(ref _refreshGeneration))
        {
            return;
        }

        if (string.Concat(report.Spans.Select(s => s.Text)) != new TextRange(ReportDocument.ContentStart, ReportDocument.ContentEnd).Text.TrimEnd('\r', '\n'))
            ReportDocument = AnalyticsReportRenderer.ToFlowDocument(report);
        StatusText = report.Spans.Count == 0 ? "No matching data in this capture." : "";
    }

    public void Dispose()
    {
        _disposed = true;
        Interlocked.Increment(ref _refreshGeneration);
        _analyticsSubscription.Dispose();
        _filterSubscription?.Dispose();
    }
}
