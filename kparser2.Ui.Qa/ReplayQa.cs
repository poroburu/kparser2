using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using kparser2.Abstractions;
using kparser2.Core;
using kparser2.Services;
using kparser2.Views;

namespace kparser2.Ui.Qa;

/// <summary>Exercises the real WPF controls against a frozen replay; does not drive the live desktop.</summary>
internal sealed partial class ReplayQa
{
    private readonly List<object> _cases = [];
    private readonly List<object> _skipped = [];
    private int _failures;
    private string _output = "";
    private Window _window = null!;
    private readonly Dictionary<string, string> _coverage = [];
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public async Task<int> RunAsync(string capture, string output)
    {
        _output = output;
        var frozen = Path.Combine(output, "capture.ndjson");
        // Refuse a writer rather than claim a stable hash for a growing live capture.
        using (var input = new FileStream(capture, FileMode.Open, FileAccess.Read, FileShare.Read))
        using (var copy = File.Create(frozen)) await input.CopyToAsync(copy);
        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(frozen)));
        using var replay = await Task.Run(() =>
        {
            var source = PacketSessionFactory.fromReplayDefault(frozen);
            source.WaitForReplayComplete();
            return source;
        });
        var snapshot = ((IAnalyticsSession)replay).GetSnapshot();
        File.WriteAllText(Path.Combine(output, "state.json"), JsonSerializer.Serialize(snapshot, Json));
        using IAnalyticsSession session = PacketSessionFactory.fromSnapshot(snapshot);
        _window = new Window
        {
            Title = "kparser2 offline UI QA", Width = 1200, Height = 800,
            Left = -20000, Top = -20000, WindowStartupLocation = WindowStartupLocation.Manual,
            ShowActivated = false, ShowInTaskbar = false, WindowStyle = WindowStyle.None,
            Background = Brushes.White
        };
        _window.Show();
        try
        {
            foreach (var view in AnalyticsViewCatalog.All)
            {
                try
                {
                    var control = view.CreateView(view is RawDataAnalyticsView ? (IAnalyticsSession)replay : session);
                    _window.Content = control;
                    await PumpAsync();
                    if (view is QueryAnalyticsView) await CheckQueryAsync(view.Id, control, session, snapshot);
                    else if (view is DamageGraphAnalyticsView) await CheckGraphAsync(control, session, snapshot);
                    else if (view is RawDataAnalyticsView) await CheckDiagnosticsAsync(control, (IPacketSession)replay);
                    else await CheckChatAsync(view.Id, control, session, snapshot);
                }
                catch (Exception ex)
                {
                    _failures++;
                    CaptureSettings($"{view.Id}-lifecycle");
                    _cases.Add(new
                    {
                        surface = view.Id, scenario = "control-lifecycle", status = "failed",
                        error = ex.ToString(),
                        sharedError = ViewSettingsService.Shared.Error,
                        settings = "view-settings.json"
                    });
                }
                finally
                {
                    _window.Content = null;
                    await PumpAsync();
                    session.LoadSnapshot(snapshot);
                }
            }
        }
        finally { _window.Close(); }
        var manifest = new
        {
            schema_version = 1, evidence_source = "wpf-replay", generated_at_utc = DateTimeOffset.UtcNow,
            capture_path = capture, capture_sha256 = hash, frozen_capture = "capture.ndjson", state_path = "state.json",
            renderer_sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(CombatAnalyticsViewControl).Assembly.Location))),
            runner_sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(ReplayQa).Assembly.Location))),
            status = _failures == 0 ? "passed" : "failed", case_count = _cases.Count, failure_count = _failures,
            cases = _cases, skipped = _skipped,
            default_content = _coverage,
            human = new { status = "unobserved" },
            limitations = new[]
            {
                "Text checks use the report formatter as the state oracle; they do not independently prove its calculations.",
                "Screenshots cover the first viewport; text artifacts include the full document.",
                "Offscreen replay does not prove live connection health, paired capture coverage, or human acceptance."
            }
        };
        File.WriteAllText(Path.Combine(output, "ui-run.json"), JsonSerializer.Serialize(manifest, Json));
        Console.WriteLine($"UI replay: {_cases.Count} cases, {_failures} failures; {Path.Combine(output, "ui-run.json")}");
        return _failures == 0 ? 0 : 1;
    }

    private async Task CheckQueryAsync(string id, UserControl control, IAnalyticsSession session, AnalyticsSnapshotDto snapshot)
    {
        var query = id == "player-info" ? "players" : id;
        var baseline = CombatAnalyticsViewControl.Modes(query)[0];
        AnalyticsReportRequest Request(ReportMode? mode = null, string? player = null, string? enemy = null,
            bool excludeZero = false, bool detail = false, bool crystals = false, int attacks = 1) => new()
        {
            QueryId = query, Mode = mode ?? baseline, ShowDetails = detail, ExcludeCrystals = crystals, BaseAttacks = attacks,
            Filter = new MobFilterDto { GroupMobs = true, SelectedPlayerName = player, SelectedMobName = enemy, ExcludeZeroXp = excludeZero }
        };
        Task Check(string scenario, AnalyticsReportRequest request, AnalyticsSnapshotDto? state = null) =>
            CheckTextAsync(id, scenario, control, AnalyticsReportService.formatRequest(request, state ?? snapshot), request);
        void Reset() => Find<Button>(control, "reset-filters")!.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

        await Check("default", Request());
        _window.Width = 760; _window.Height = 480;
        await Check("compact", Request());
        _window.Width = 1200; _window.Height = 800;
        var modes = Find<ComboBox>(control, "report-mode");
        if (modes is not null)
        {
            var options = CombatAnalyticsViewControl.Modes(query);
            for (var i = 1; i < options.Length; i++)
            {
                modes.SelectedIndex = i;
                await Check("mode-" + options[i], Request(mode: options[i]));
            }
            Reset();
        }
        foreach (var (filter, isPlayer) in new[] { ("player-filter", true), ("enemy-filter", false) })
        {
            var selector = Find<ComboBox>(control, filter);
            if (selector is null || selector.Items.Count < 2) continue;
            selector.SelectedIndex = 1;
            var selected = selector.SelectedItem.ToString();
            await Check(filter, Request(player: isPlayer ? selected : null, enemy: isPlayer ? null : selected));
            var loaded = new ViewSettingsService();
            var persisted = loaded.Report(query);
            if ((isPlayer ? persisted.Player : persisted.Mob) != selected)
            {
                CaptureSettings($"{id}-{filter}-unpersisted");
                throw new InvalidOperationException(
                    "Selected filter was not persisted to the isolated settings file."
                    + $" selected={selected}; diskPlayer={persisted.Player}; diskMob={persisted.Mob}"
                    + $"; sharedPlayer={ViewSettingsService.Shared.Report(query).Player}; sharedMob={ViewSettingsService.Shared.Report(query).Mob}"
                    + $"; sharedError={ViewSettingsService.Shared.Error}; loadError={loaded.Error}");
            }
            // Recreating the view must restore the same filter from isolated saved settings.
            _window.Content = null;
            await PumpAsync();
            control = AnalyticsViewCatalog.All.Single(v => v.Id == id).CreateView(session);
            _window.Content = control;
            await PumpAsync();
            await Check(filter + "-restored", Request(player: isPlayer ? selected : null, enemy: isPlayer ? null : selected));
            Reset();
            await Check(filter + "-reset", Request());
        }
        foreach (var toggle in new[] { "exclude-zero-xp", "show-details", "exclude-crystals" })
        {
            var checkbox = Find<CheckBox>(control, toggle);
            if (checkbox is null) continue;
            checkbox.IsChecked = true;
            checkbox.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            await Check(toggle, Request(excludeZero: toggle == "exclude-zero-xp", detail: toggle == "show-details", crystals: toggle == "exclude-crystals"));
            Reset();
        }
        var attacks = Find<ComboBox>(control, "base-attacks");
        if (attacks is not null)
        {
            attacks.SelectedItem = 2;
            await Check("two-base-attacks", Request(attacks: 2));
            Reset();
            if (!Equals(attacks.SelectedItem, 1)) throw new InvalidOperationException("Reset filters did not restore one base attack.");
            await Check("base-attacks-reset", Request());
        }
        var fightButton = Find<Button>(control, "select-fights");
        if (fightButton is not null)
        {
            foreach (var take in new[] { 0, 1 })
            {
                if (take == 1 && snapshot.Battles.Count == 0) continue;
                Exception? dialogError = null;
                // Schedule interaction inside the real modal dialog's dispatcher loop.
                _ = _window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
                {
                    var dialog = Application.Current.Windows.Cast<Window>().Single(w => w != _window);
                    try
                    {
                        var list = Find<ListBox>(dialog, "fight-selection")!;
                        list.UnselectAll();
                        if (take == 1) list.SelectedIndex = 0;
                        Find<Button>(dialog, "apply-fights")!.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                    }
                    catch (Exception ex) { dialogError = ex; dialog.Close(); }
                }));
                fightButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                if (dialogError is not null) throw dialogError;
                await Check(take == 0 ? "no-fights" : "one-fight", new AnalyticsReportRequest
                {
                    QueryId = query, Mode = baseline, Filter = new MobFilterDto { GroupMobs = true },
                    BattleIds = snapshot.Battles.Take(take).Select(b => b.Id).ToArray()
                });
                Reset();
                await Check(take == 0 ? "no-fights-reset" : "one-fight-reset", Request());
            }
        }
        // Exercise the subscription and last-update behavior, not just first render.
        var empty = new AnalyticsSnapshotDto();
        session.LoadSnapshot(empty);
        await Check("empty-refresh", Request(), empty);
        session.LoadSnapshot(snapshot);
        session.LoadSnapshot(snapshot);
        await Check("repeat-refresh", Request());
    }

    private async Task CheckChatAsync(string id, UserControl control, IAnalyticsSession session, AnalyticsSnapshotDto snapshot)
    {
        Task Check(string name, string? mode = null, string? speaker = null, AnalyticsSnapshotDto? state = null) =>
            CheckTextAsync(id, name, control, id == "chat-summary"
                ? AnalyticsReportService.formatChatSummary(state ?? snapshot, mode, speaker)
                : AnalyticsReportService.formatChat(state ?? snapshot, mode, speaker), new { mode, speaker });
        await Check("default");
        _window.Width = 760; _window.Height = 480;
        await Check("compact");
        _window.Width = 1200; _window.Height = 800;
        var modes = Find<ComboBox>(control, "chat-mode")!;
        foreach (var mode in snapshot.ChatMessages.Select(c => c.Mode).Distinct())
        {
            modes.SelectedItem = mode;
            await Check("mode-" + modes.SelectedIndex, mode);
        }
        modes.SelectedItem = "All";
        var speakers = Find<ComboBox>(control, "chat-speaker")!;
        if (speakers.Items.Count > 1)
        {
            speakers.SelectedIndex = 1;
            await Check("speaker", speaker: speakers.SelectedItem as string);
            speakers.SelectedItem = "All";
        }
        var empty = new AnalyticsSnapshotDto();
        session.LoadSnapshot(empty);
        await Check("empty-refresh", state: empty);
        session.LoadSnapshot(snapshot);
        session.LoadSnapshot(snapshot);
        await Check("repeat-refresh");
    }

    private async Task CheckTextAsync(string surface, string scenario, UserControl control, AnalyticsReportDto expectedReport, object filters)
    {
        var expected = expectedReport.Spans.Count == 0 ? "(no data)" : string.Concat(expectedReport.Spans.Select(s => s.Text));
        var timer = Stopwatch.StartNew();
        string actual = "";
        FlowDocumentScrollViewer? viewer = null;
        do
        {
            await PumpAsync();
            viewer = Descendants(control).OfType<FlowDocumentScrollViewer>().FirstOrDefault();
            if (viewer?.Document is { } document) actual = new TextRange(document.ContentStart, document.ContentEnd).Text;
            if (Normalize(actual) == Normalize(expected)) break;
            await Task.Delay(20);
        } while (timer.Elapsed < TimeSpan.FromSeconds(5));
        control.UpdateLayout();
        var unwrapped = surface is "chat" or "chat-summary" || (viewer?.Document is { } doc && LinesDoNotWrap(doc));
        var equal = viewer is not null && Normalize(actual) == Normalize(expected) && control.ActualWidth > 0 && control.ActualHeight > 0 && unwrapped;
        var kind = ContentKind(expected);
        SaveCase(surface, scenario, control, expected, actual, filters, kind, timer.ElapsedMilliseconds, equal,
            unwrapped ? null : "Fixed-width report rows wrap across visual lines.");
    }

    private static string ContentKind(string text)
    {
        if (text.TrimStart().StartsWith("Unavailable:", StringComparison.Ordinal)) return "unavailable";
        if (string.IsNullOrWhiteSpace(text) || text.Contains("(no data)", StringComparison.Ordinal) ||
            text.Contains("No matching", StringComparison.Ordinal)) return "empty-or-partial";
        return "populated";
    }

    private void CaptureSettings(string label)
    {
        var source = Path.Combine(_output, "view-settings.json");
        if (!File.Exists(source)) return;
        File.Copy(source, Path.Combine(_output, $"{label}-view-settings.json"), overwrite: true);
    }

    private void SaveCase(string surface, string scenario, UserControl control, string expected, string actual,
        object filters, string kind, long elapsed, bool equal, string? error = null)
    {
        if (!equal) _failures++;
        if (scenario == "default") _coverage[surface] = kind;
        var name = $"{surface}-{scenario}";
        File.WriteAllText(Path.Combine(_output, name + ".actual.txt"), actual);
        File.WriteAllText(Path.Combine(_output, name + ".expected.txt"), expected);
        var image = new RenderTargetBitmap((int)Math.Ceiling(_window.ActualWidth), (int)Math.Ceiling(_window.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        // Include the host background; a transparent PNG makes black report text
        // disappear in dark screenshot viewers even though the live host is white.
        image.Render(_window);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using (var file = File.Create(Path.Combine(_output, name + ".png"))) encoder.Save(file);
        _cases.Add(new
        {
            surface, scenario, status = equal ? "passed" : "failed", filters, error,
            screenshot = name + ".png", actual_text = name + ".actual.txt", expected_text = name + ".expected.txt",
            content_kind = kind,
            viewport = new { width = _window.ActualWidth, height = _window.ActualHeight },
            elapsed_ms = elapsed
        });
        Console.WriteLine($"{surface}/{scenario}: {(equal ? "passed" : "FAILED")}");
    }

    private static bool LinesDoNotWrap(FlowDocument document)
    {
        foreach (var run in document.Blocks.OfType<Paragraph>().SelectMany(p => p.Inlines.OfType<Run>()))
        {
            var offset = 0;
            foreach (var line in run.Text.Split('\n'))
            {
                var length = line.TrimEnd('\r').Length;
                if (length > 1)
                {
                    var first = run.ContentStart.GetPositionAtOffset(offset)?.GetCharacterRect(LogicalDirection.Forward) ?? Rect.Empty;
                    var last = run.ContentStart.GetPositionAtOffset(offset + length - 1)?.GetCharacterRect(LogicalDirection.Forward) ?? Rect.Empty;
                    if (!first.IsEmpty && !last.IsEmpty && Math.Abs(first.Top - last.Top) > 1) return false;
                }
                offset += line.Length + 1;
            }
        }
        return true;
    }

    private static string Normalize(string text) => text.Replace("\r\n", "\n").TrimEnd('\r', '\n');
    private static async Task PumpAsync() => await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
    private static T? Find<T>(DependencyObject root, string id) where T : DependencyObject =>
        Descendants(root).OfType<T>().FirstOrDefault(x => AutomationProperties.GetAutomationId(x) == id);
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        yield return root;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var child in Descendants(VisualTreeHelper.GetChild(root, i))) yield return child;
    }
}
