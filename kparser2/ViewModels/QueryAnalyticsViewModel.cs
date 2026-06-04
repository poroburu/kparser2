using System.Collections.ObjectModel;
using System.Reactive.Disposables;
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

    public ObservableCollection<AnalyticsRowDto> Rows { get; } = [];

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
        var generation = Interlocked.Increment(ref _refreshGeneration);
        var filter = _mobFilter?.Current ?? new MobFilterDto();
        var queryId = _queryId;

        Task.Run(() => AnalyticsQueryService.query(queryId, snapshot, filter))
            .ContinueWith(
                t =>
                {
                    if (t.IsFaulted || generation != Volatile.Read(ref _refreshGeneration))
                    {
                        return;
                    }

                    var rows = t.Result;
                    UiThread.RunBackground(() => ApplyRows(generation, rows, snapshot));
                },
                TaskScheduler.Default);
    }

    private void ApplyRows(int generation, IReadOnlyList<AnalyticsRowDto> rows, AnalyticsSnapshotDto snapshot)
    {
        if (generation != Volatile.Read(ref _refreshGeneration))
        {
            return;
        }

        Rows.Clear();

        foreach (var row in rows)
        {
            Rows.Add(row);
        }

        StatusText = $"{Rows.Count} rows | {snapshot.Interactions.Count} interactions | {snapshot.Battles.Count} fights";
    }

    public void Dispose()
    {
        _analyticsSubscription.Dispose();
        _filterSubscription?.Dispose();
    }
}
