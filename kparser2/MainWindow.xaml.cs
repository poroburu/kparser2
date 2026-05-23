using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using kparser2.Abstractions;
using kparser2.Core;
using kparser2.Services;

namespace kparser2;

public partial class MainWindow : Window
{
    private readonly ViewRegistry _viewRegistry = ViewRegistryFactory.CreateDefault();
    private IPacketSession? _session;
    private DispatcherTimer? _liveDiagnosticsTimer;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => Dispatcher.BeginInvoke(() => StartReplay(FindFixture("sample.ndjson")));
        Closed += (_, _) =>
        {
            _liveDiagnosticsTimer?.Stop();
            _session?.Dispose();
        };
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

    private void StartLive()
    {
        var pluginOk = ConnectionProbe.isPluginReachable();

        if (!pluginOk)
        {
            var result = MessageBox.Show(
                "Cannot reach the kpacket command socket on tcp://localhost:5556.\n\n" +
                "Common causes:\n" +
                "• The Lua kpacket addon is loaded instead of the C++ kpacket2 plugin (Lua uses port 6666).\n" +
                "• kpacket2.dll is not in Ashita's plugins folder.\n" +
                "• Plugin failed to initialize (check Ashita logs).\n\n" +
                "Continue anyway and wait for packets?",
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
            (IPacketSession)PacketSessionFactory.fromLiveDefault(),
            pluginOk
                ? "Live feed on tcp://localhost:5555 (plugin OK on :5556)"
                : "Live feed on tcp://localhost:5555 (plugin NOT detected on :5556 — zone in-game to generate packets)");

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
            var pluginPublished = ConnectionProbe.publishedCount();

            if (stats.TotalPackets == 0)
            {
                var zmqReceived = stats.SubscriberPackets ?? 0;
                var parseErrors = stats.SubscriberParseErrors ?? 0;

                if (pluginPublished > 0 && zmqReceived == 0)
                {
                    StatusText.Text =
                        "Plugin is publishing but kparser2 receives nothing on :5555 — " +
                        "use Session → Use Live Feed again after /load kpacket.";
                }
                else if (zmqReceived > 0 && stats.TotalPackets == 0 && parseErrors > 0)
                {
                    StatusText.Text =
                        $"Live feed parse errors ({parseErrors}) — last: {stats.SubscriberLastError ?? "unknown"}";
                }
                else
                {
                    StatusText.Text =
                        "Live feed waiting for packets — zone or take an action in-game. " +
                        $"Plugin published: {pluginPublished}, ZMQ received: {zmqReceived}.";
                }
            }
            else
            {
                StatusText.Text =
                    $"Live feed | packets: {stats.TotalPackets} | chat: {stats.ChatEvents} | loot: {stats.LootEvents} | combat: {stats.CombatEvents} | " +
                    $"zmq: {stats.SubscriberPackets ?? 0} | reconnects: {stats.SubscriberReconnects ?? 0}";
            }
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

        StartSession((IPacketSession)PacketSessionFactory.fromReplayDefault(path), $"Replay {Path.GetFileName(path)}");
    }

    private void StartSession(IPacketSession session, string status)
    {
        _session?.Dispose();
        _session = session;
        ViewTabs.Items.Clear();

        foreach (var view in _viewRegistry.Views)
        {
            ViewTabs.Items.Add(new TabItem
            {
                Header = view.Title,
                Content = view.CreateView(session)
            });
        }

        StatusText.Text = status;
    }

    private void OnUseLiveFeed(object sender, RoutedEventArgs e) => StartLive();

    private void OnReplaySample(object sender, RoutedEventArgs e) =>
        StartReplay(FindFixture("sample.ndjson"));

    private void OnReplayLogin(object sender, RoutedEventArgs e) =>
        StartReplay(FindFixture("login.ndjson"));

    private void OnReplayItemDrop(object sender, RoutedEventArgs e) =>
        StartReplay(FindFixture("item_drop.ndjson"));
}
