using System.Collections.ObjectModel;
using System.Reactive.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using kparser2.Abstractions;
using kparser2.Core;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace kparser2.ViewModels;

public sealed partial class DamageGraphViewModel : ObservableObject, IDisposable
{
    private readonly IAnalyticsSession _session;
    private readonly IDisposable _subscription;

    public ObservableCollection<ISeries> Series { get; } = [];

    [ObservableProperty]
    private string[] _labels = [];

    public DamageGraphViewModel(IAnalyticsSession session)
    {
        _session = session;
        Refresh(session.GetSnapshot());
        _subscription = session.Analytics.Subscribe(snapshot => UiThread.RunBackground(() => Refresh(snapshot)));
    }

    private void Refresh(AnalyticsSnapshotDto snapshot)
    {
        var filter = new MobFilterDto();
        var rows = AnalyticsQueryService.query("offense", snapshot, filter);
        Labels = rows.Select(r => r.Label).ToArray();

        Series.Clear();
        Series.Add(new ColumnSeries<int>
        {
            Values = rows.Select(r => r.Total).ToArray(),
            Fill = new SolidColorPaint(SKColors.SteelBlue)
        });
    }

    public void Dispose() => _subscription.Dispose();
}
