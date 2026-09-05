using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using kparser2.Abstractions;
using kparser2.Core;
using kparser2.Services;
using Microsoft.Win32;

namespace kparser2;

public partial class MainWindow : Window
{
    private readonly SessionCoordinator _coordinator = new();
    private readonly ViewSettingsService _settings = ViewSettingsService.Shared;
    private readonly List<ReportEntry> _reports = [];
    private bool _ready, _closing, _busy;
    private sealed record ReportEntry(string Id, string Title, string Group, int GroupOrder, Func<IAnalyticsSession, UserControl> Create);
    public MainWindow()
    {
        InitializeComponent();
        var s = _settings.State;
        Width = double.IsFinite(s.Width) ? Math.Clamp(s.Width, 760, Math.Max(760, SystemParameters.VirtualScreenWidth)) : 1200;
        Height = double.IsFinite(s.Height) ? Math.Clamp(s.Height, 480, Math.Max(480, SystemParameters.VirtualScreenHeight)) : 800;
        if (s.Left is double left && s.Top is double top && left + Width > SystemParameters.VirtualScreenLeft && left < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth && top >= SystemParameters.VirtualScreenTop && top < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 40)
        { WindowStartupLocation = WindowStartupLocation.Manual; Left = left; Top = top; }
        if (s.Maximized) WindowState = WindowState.Maximized;
        SidebarColumn.Width = new GridLength(Math.Clamp(s.SidebarWidth, 150, 360));
        foreach (var view in Views.AnalyticsViewCatalog.All)
        {
            var group = GroupFor(view.Id);
            _reports.Add(new(view.Id, view.Title, group, GroupOrderFor(group), view.CreateView));
        }
        foreach (var view in ViewRegistryFactory.CreateDefault().PacketViews)
            _reports.Add(new("packet-" + view.Id, view.Title, "Diagnostics", GroupOrderFor("Diagnostics"), session => view.CreateView(session)));
        var collection = CollectionViewSource.GetDefaultView(_reports);
        collection.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ReportEntry.Group)));
        collection.SortDescriptions.Add(new SortDescription(nameof(ReportEntry.GroupOrder), ListSortDirection.Ascending));
        collection.SortDescriptions.Add(new SortDescription(nameof(ReportEntry.Title), ListSortDirection.Ascending));
        collection.Filter = value => value is ReportEntry entry && (entry.Title.Contains(SearchBox.Text, StringComparison.OrdinalIgnoreCase) || entry.Group.Contains(SearchBox.Text, StringComparison.OrdinalIgnoreCase));
        ReportList.ItemsSource = collection;
        _coordinator.Changed += UpdateStatus;
        _coordinator.SessionChanged += ShowSelectedReport;
        _ready = true;
        SearchBox.Text = s.Search;
        ReportList.SelectedItem = _reports.FirstOrDefault(r => r.Id == s.SelectedReport) ?? _reports.First(r => r.Id == "offense");
        RefreshRecent();
        Loaded += async (_, _) => await RunAsync(async () =>
        {
            var args = Environment.GetCommandLineArgs().Skip(1).ToArray();
            if (args.Length == 2 && args[0] == "--replay") await OpenCaptureAsync(args[1]);
            else if (args.Length == 2 && args[0] == "--report") await _coordinator.OpenReportAsync(args[1]);
            else if (args.Length == 0 || args.SequenceEqual(new[] { "--live" })) await _coordinator.StartLiveAsync();
            else throw new ArgumentException("Usage: kparser2 [--live | --replay <path> | --report <path>]");
        });
        Closing += OnClosing;
    }
    private static string GroupFor(string id) => id switch
    {
        "thief" or "corsair" or "abyssea" => "Jobs",
        "recovery" or "buffs" or "debuffs" or "enfeebling" or "buffs-by-time" or "def-by-time" => "Support",
        "chat" or "chat-summary" or "loot" or "items" or "player-info" or "experience" => "Session",
        "raw-data" => "Diagnostics", _ => "Combat"
    };
    private static int GroupOrderFor(string group) => group switch
    {
        "Combat" => 0,
        "Support" => 1,
        "Jobs" => 2,
        "Session" => 3,
        "Diagnostics" => 4,
        _ => 5
    };
    private void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        if (!_ready) return;
        CollectionViewSource.GetDefaultView(_reports).Refresh();
        _settings.State.Search = SearchBox.Text; _settings.Save();
    }
    private void OnReportSelected(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || ReportList.SelectedItem is not ReportEntry entry) return;
        _settings.State.SelectedReport = entry.Id; _settings.Save(); ShowSelectedReport();
    }
    private void ShowSelectedReport()
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(ShowSelectedReport); return; }
        if (_closing) return;
        var entry = ReportList.SelectedItem as ReportEntry ?? _reports.FirstOrDefault(r => r.Id == _settings.State.SelectedReport) ?? _reports.First(r => r.Id == "offense");
        ReportContent.Content = null;
        ReportTitle.Text = entry.Title;
        ReportContent.Content = entry.Create(_coordinator.Session);
    }
    private void UpdateStatus()
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(UpdateStatus); return; }
        if (_closing) return;
        StatusText.Text = _coordinator.Status;
        CaptureText.Text = _coordinator.Mode == "Live" && _coordinator.CapturePath is string path ? "Saving to " + path : "";
    }
    private async Task RunAsync(Func<Task> action)
    {
        if (_busy) return;
        _busy = true; SourceToolbar.IsEnabled = false;
        try { await action(); }
        catch (Exception ex) { StatusText.Text = ex.Message; }
        finally { _busy = false; SourceToolbar.IsEnabled = true; }
    }
    private async void OnUseLiveFeed(object sender, RoutedEventArgs e) => await RunAsync(() => _coordinator.StartLiveAsync());
    private async void OnNewSession(object sender, RoutedEventArgs e) => await RunAsync(() => _coordinator.StartLiveAsync(fresh: true));
    private async Task OpenCaptureAsync(string path)
    {
        await _coordinator.OpenCaptureAsync(path);
        _settings.State.RecentCaptures.Remove(path); _settings.State.RecentCaptures.Insert(0, path);
        _settings.State.RecentCaptures = _settings.State.RecentCaptures.Take(20).ToList();
        _settings.Save(); RefreshRecent();
    }
    private void RefreshRecent()
    {
        var automatic = Directory.Exists(_coordinator.CaptureDirectory) ? Directory.GetFiles(_coordinator.CaptureDirectory, "*.ndjson").OrderByDescending(File.GetLastWriteTimeUtc).Take(20) : [];
        RecentCaptures.ItemsSource = _settings.State.RecentCaptures.Concat(automatic).Distinct().ToArray();
        RecentCaptures.SelectedIndex = -1;
    }
    private async void OnRecentCapture(object sender, SelectionChangedEventArgs e)
    { if (RecentCaptures.SelectedItem is string path) await RunAsync(() => OpenCaptureAsync(path)); }
    private async void OnOpenCapture(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Packet captures (*.ndjson)|*.ndjson" };
        if (dialog.ShowDialog() == true) await RunAsync(() => OpenCaptureAsync(dialog.FileName));
    }
    private async void OnOpenReport(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "KParser2 report (*.kparse2.json)|*.kparse2.json" };
        if (dialog.ShowDialog() == true) await RunAsync(() => _coordinator.OpenReportAsync(dialog.FileName));
    }
    private async void OnExportReport(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "KParser2 report (*.kparse2.json)|*.kparse2.json", FileName = "session.kparse2.json" };
        if (dialog.ShowDialog() == true) await RunAsync(async () =>
        {
            IReportExporter exporter = new FileReportExporter();
            await exporter.ExportAsync(dialog.FileName, _coordinator.Session.GetSnapshot(), "kparser2 session");
            StatusText.Text = "Exported " + dialog.FileName;
        });
    }
    private async void OnImportPacketViewer(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "PacketViewer full log (*.log)|*.log", Title = "Import PacketViewer full.log" };
        if (dialog.ShowDialog() != true) return;
        await RunAsync(async () =>
        {
            Directory.CreateDirectory(_coordinator.CaptureDirectory);
            var output = Path.Combine(_coordinator.CaptureDirectory, $"import-{Guid.NewGuid():N}.ndjson");
            await Task.Run(() => new PacketViewerImportService().ConvertToNdjson(output, fullLog: dialog.FileName, sessionId: Path.GetFileNameWithoutExtension(dialog.FileName)));
            await OpenCaptureAsync(output);
        });
    }
    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_closing) return;
        e.Cancel = true; _closing = true;
        _coordinator.Changed -= UpdateStatus; _coordinator.SessionChanged -= ShowSelectedReport;
        ReportContent.Content = null;
        var bounds = RestoreBounds; var s = _settings.State;
        s.Width = bounds.Width; s.Height = bounds.Height; s.Left = bounds.Left; s.Top = bounds.Top;
        s.Maximized = WindowState == WindowState.Maximized; s.SidebarWidth = SidebarColumn.ActualWidth;
        _settings.Save();
        await _coordinator.DisposeAsync(); Close();
    }
}
