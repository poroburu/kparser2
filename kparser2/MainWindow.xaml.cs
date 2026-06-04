using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using kparser2.Abstractions;
using kparser2.Core;
using kparser2.Services;
using Microsoft.Win32;

namespace kparser2;

public partial class MainWindow : Window
{
    private readonly ViewRegistry _viewRegistry = ViewRegistryFactory.CreateDefault();
    private readonly ViewSettingsService _viewSettings = new();
    private readonly Dictionary<string, IAnalyticsView> _analyticsViewMap;
    private IAnalyticsSession? _session;
    private DispatcherTimer? _liveDiagnosticsTimer;
    private readonly HashSet<string> _activeViewIds = [];

    public MainWindow()
    {
        InitializeComponent();
        _analyticsViewMap = _viewRegistry.AnalyticsViews.ToDictionary(v => v.Id, StringComparer.OrdinalIgnoreCase);

        foreach (var id in _viewSettings.LoadActiveViewIds(_viewRegistry.AnalyticsViews.Select(v => v.Id).ToList()))
        {
            _activeViewIds.Add(id);
        }

        const int maxDefaultTabs = 8;
        if (_activeViewIds.Count > maxDefaultTabs)
        {
            _activeViewIds.Clear();
        }

        if (_activeViewIds.Count == 0)
        {
            foreach (var id in new[] { "chat", "fights", "offense", "loot", "experience" })
            {
                if (_analyticsViewMap.ContainsKey(id))
                {
                    _activeViewIds.Add(id);
                }
            }

            if (_activeViewIds.Count == 0 && _analyticsViewMap.Count > 0)
            {
                _activeViewIds.Add(_analyticsViewMap.Keys.First());
            }

            _viewSettings.SaveActiveViewIds(_activeViewIds);
        }

        BuildViewMenu();

        Loaded += (_, _) => Dispatcher.BeginInvoke(() => StartReplay(FindFixture("sample.ndjson")));
        Closed += (_, _) =>
        {
            _liveDiagnosticsTimer?.Stop();
            _session?.Dispose();
        };
    }

    private void BuildViewMenu()
    {
        var viewMenu = new MenuItem { Header = "Views" };

        foreach (var view in _viewRegistry.AnalyticsViews.OrderBy(v => v.Title))
        {
            var item = new MenuItem
            {
                Header = view.Title,
                IsCheckable = true,
                IsChecked = _activeViewIds.Contains(view.Id),
                Tag = view.Id
            };

            item.Click += OnToggleView;
            viewMenu.Items.Add(item);
        }

        MainMenu.Items.Add(viewMenu);
        MainMenu.Items.Add(new Separator());

        var exportItem = new MenuItem { Header = "Export Report..." };
        exportItem.Click += OnExportReport;
        MainMenu.Items.Add(exportItem);

        var importItem = new MenuItem { Header = "Open Report..." };
        importItem.Click += OnOpenReport;
        MainMenu.Items.Add(importItem);
    }

    private static string FindFixture(string fileName)
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "fixtures", "sessions", fileName),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "fixtures", "sessions", fileName))
        };

        return candidates.FirstOrDefault(File.Exists)
            ?? Path.Combine(AppContext.BaseDirectory, "fixtures", "sessions", fileName);
    }

    private void OnToggleView(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem item || item.Tag is not string id)
        {
            return;
        }

        if (item.IsChecked)
        {
            _activeViewIds.Add(id);
        }
        else
        {
            _activeViewIds.Remove(id);
        }

        _viewSettings.SaveActiveViewIds(_activeViewIds);

        if (_session is not null)
        {
            RebuildTabs(_session);
        }
    }

    private void StartLive()
    {
        var pluginOk = ConnectionProbe.isPluginReachable();

        if (!pluginOk)
        {
            var result = MessageBox.Show(
                "Cannot reach the kpacket command socket on tcp://localhost:5556.\n\nContinue anyway and wait for packets?",
                "Plugin not detected",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                StatusText.Text = "Live feed cancelled — plugin not reachable on :5556";
                return;
            }
        }

        StartSession(
            (IAnalyticsSession)PacketSessionFactory.fromLiveDefault(),
            pluginOk
                ? "Live feed on tcp://localhost:5555 (plugin OK on :5556)"
                : "Live feed on tcp://localhost:5555 (plugin NOT detected on :5556)");

        StartLiveDiagnostics();
    }

    private void StartLiveDiagnostics()
    {
        _liveDiagnosticsTimer?.Stop();
        _liveDiagnosticsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };

        _liveDiagnosticsTimer.Tick += (_, _) =>
        {
            if (_session is null)
            {
                return;
            }

            var stats = _session.GetStatsAsync().GetAwaiter().GetResult();
            var snap = _session.GetSnapshot();

            StatusText.Text =
                $"Live | packets: {stats.TotalPackets} | fights: {snap.Battles.Count} | interactions: {snap.Interactions.Count}";
        };

        _liveDiagnosticsTimer.Start();
    }

    private void StartReplay(string path)
    {
        _liveDiagnosticsTimer?.Stop();

        if (!File.Exists(path))
        {
            StatusText.Text = $"Fixture not found: {path}";
            return;
        }

        StartSession((IAnalyticsSession)PacketSessionFactory.fromReplayDefault(path), $"Replay {Path.GetFileName(path)}");
    }

    private void StartSession(IAnalyticsSession session, string status)
    {
        _session?.Dispose();
        _session = session;
        RebuildTabs(session);
        StatusText.Text = status;
    }

    private void RebuildTabs(IAnalyticsSession session)
    {
        ViewTabs.Items.Clear();

        foreach (var view in _viewRegistry.PacketViews)
        {
            ViewTabs.Items.Add(new TabItem
            {
                Header = view.Title,
                Content = view.CreateView(session)
            });
        }

        foreach (var view in _viewRegistry.AnalyticsViews.Where(v => _activeViewIds.Contains(v.Id)))
        {
            ViewTabs.Items.Add(new TabItem
            {
                Header = view.Title,
                Content = view.CreateView(session)
            });
        }
    }

    private async void OnExportReport(object sender, RoutedEventArgs e)
    {
        if (_session is null)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "KParser2 report (*.kparse2.json)|*.kparse2.json",
            FileName = "session.kparse2.json"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        IReportExporter exporter = new FileReportExporter();
        await exporter.ExportAsync(dialog.FileName, _session.GetSnapshot(), "kparser2 session");
        StatusText.Text = $"Exported report to {dialog.FileName}";
    }

    private async void OnOpenReport(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "KParser2 report (*.kparse2.json)|*.kparse2.json"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        IReportImporter importer = new FileReportImporter();
        var snapshot = await importer.ImportAsync(dialog.FileName);

        if (_session is null)
        {
            StartSession((IAnalyticsSession)PacketSessionFactory.fromReplayDefault(FindFixture("sample.ndjson")), "Imported report");
        }

        _session?.LoadSnapshot(snapshot);
        StatusText.Text = $"Imported report from {dialog.FileName}";
    }

    private void OnUseLiveFeed(object sender, RoutedEventArgs e) => StartLive();

    private void OnReplaySample(object sender, RoutedEventArgs e) =>
        StartReplay(FindFixture("sample.ndjson"));

    private void OnReplayLogin(object sender, RoutedEventArgs e) =>
        StartReplay(FindFixture("login.ndjson"));

    private void OnReplayItemDrop(object sender, RoutedEventArgs e) =>
        StartReplay(FindFixture("item_drop.ndjson"));

    private void OnReplayCombat(object sender, RoutedEventArgs e) =>
        StartReplay(FindFixture("combat_basic.ndjson"));

    private void OnImportPacketViewer(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "PacketViewer logs (*.log)|*.log",
            Multiselect = true,
            Title = "Import PacketViewer capture"
        };

        if (dialog.ShowDialog() != true || dialog.FileNames.Length == 0)
        {
            return;
        }

        try
        {
            var importService = new PacketViewerImportService();
            var outputPath = Path.Combine(
                Path.GetTempPath(),
                $"kparser2-pv-{DateTime.UtcNow:yyyyMMddHHmmss}.ndjson");

            string? fullLog = null;
            string? incomingLog = null;
            string? outgoingLog = null;

            foreach (var file in dialog.FileNames)
            {
                var name = Path.GetFileName(file).ToLowerInvariant();

                if (name == "full.log")
                {
                    fullLog = file;
                }
                else if (name == "incoming.log" || file.Contains($"{Path.DirectorySeparatorChar}incoming{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                {
                    incomingLog = file;
                }
                else if (name == "outgoing.log" || file.Contains($"{Path.DirectorySeparatorChar}outgoing{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                {
                    outgoingLog = file;
                }
                else if (fullLog is null && incomingLog is null && outgoingLog is null)
                {
                    fullLog = file;
                }
            }

            importService.ConvertToNdjson(
                outputPath,
                fullLog: fullLog,
                incomingLog: incomingLog,
                outgoingLog: outgoingLog,
                sessionId: Path.GetFileNameWithoutExtension(fullLog ?? incomingLog ?? dialog.FileNames[0]));

            StartReplay(outputPath);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "PacketViewer import failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
