using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Windows.Documents;
using CommunityToolkit.Mvvm.ComponentModel;
using kparser2.Abstractions;
using kparser2.Core;
using kparser2.Services;

namespace kparser2.ViewModels;

public sealed partial class ChatAnalyticsViewModel : ObservableObject, IDisposable
{
    private readonly IAnalyticsSession _session;
    private readonly IDisposable _subscription;
    private readonly bool _summary;
    private bool _isRefreshing;
    private bool _disposed;
    private readonly ViewSettingsService.ReportPreferences _preferences;

    public ObservableCollection<string> ModeOptions { get; } =
    [
        "All", "Say", "Shout", "Tell", "Party", "Linkshell", "Emote", "System", "Yell", "Standard"
    ];

    public ObservableCollection<string> SpeakerOptions { get; } = ["All"];

    [ObservableProperty]
    private string _selectedMode = "All";

    [ObservableProperty]
    private string _selectedSpeaker = "All";

    [ObservableProperty]
    private FlowDocument _reportDocument = new();

    public ChatAnalyticsViewModel(IAnalyticsSession session, bool summary = false)
    {
        _session = session;
        _summary = summary;
        _preferences = ViewSettingsService.Shared.Report(summary ? "chat-summary" : "chat");
        _selectedMode = _preferences.Mode;
        _selectedSpeaker = _preferences.Speaker ?? "All";
        Refresh(session.GetSnapshot());

        _subscription = session.Analytics.Subscribe(snapshot =>
            UiThread.RunBackground(() => Refresh(snapshot)));
    }

    partial void OnSelectedModeChanged(string value)
    {
        if (_isRefreshing)
        {
            return;
        }

        Refresh(_session.GetSnapshot());
        _preferences.Mode = value;
        ViewSettingsService.Shared.Save();
    }

    partial void OnSelectedSpeakerChanged(string value)
    {
        if (_isRefreshing)
        {
            return;
        }

        Refresh(_session.GetSnapshot());
        _preferences.Speaker = value;
        ViewSettingsService.Shared.Save();
    }

    private static bool IsAll(string? value) =>
        string.IsNullOrWhiteSpace(value) || value.Equals("All", StringComparison.OrdinalIgnoreCase);

    private static string? ToFilter(string? value) => IsAll(value) ? null : value;

    private void Refresh(AnalyticsSnapshotDto snapshot)
    {
        if (_disposed) return;
        _isRefreshing = true;
        try
        {
            var previousMode = SelectedMode;
            var previousSpeaker = SelectedSpeaker;

            var speakers = snapshot.ChatMessages
                .Select(c => c.Speaker)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s)
                .ToList();

            foreach (var mode in snapshot.ChatMessages.Select(c => c.Mode).Distinct())
                if (!ModeOptions.Contains(mode)) ModeOptions.Add(mode);
            foreach (var speaker in speakers)
                if (!SpeakerOptions.Contains(speaker)) SpeakerOptions.Add(speaker);

            SelectedMode = IsAll(previousMode) || ModeOptions.Contains(previousMode)
                ? (IsAll(previousMode) ? "All" : previousMode)
                : "All";

            SelectedSpeaker = IsAll(previousSpeaker)
                ? "All"
                : speakers.FirstOrDefault(s => s.Equals(previousSpeaker, StringComparison.OrdinalIgnoreCase)) ?? "All";

            var modeFilter = ToFilter(SelectedMode);
            var speakerFilter = ToFilter(SelectedSpeaker);
            var report = _summary
                ? AnalyticsReportService.formatChatSummary(snapshot, modeFilter, speakerFilter)
                : AnalyticsReportService.formatChat(snapshot, modeFilter, speakerFilter);

            ReportDocument = AnalyticsReportRenderer.ToFlowDocument(report);
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    public void Dispose() { _disposed = true; _subscription.Dispose(); }
}
