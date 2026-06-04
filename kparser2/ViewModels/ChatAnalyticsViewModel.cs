using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using kparser2.Abstractions;

namespace kparser2.ViewModels;

public sealed partial class ChatAnalyticsViewModel : ObservableObject, IDisposable
{
    private const int MaxDisplayedMessages = 1000;
    private readonly IAnalyticsSession _session;
    private readonly IDisposable _subscription;
    private bool _isRefreshing;

    public ObservableCollection<ChatMessageRow> Messages { get; } = [];

    public ObservableCollection<string> ModeOptions { get; } =
    [
        "All", "Say", "Shout", "Tell", "Party", "Linkshell", "Emote", "System", "Yell", "Standard"
    ];

    public ObservableCollection<string> SpeakerOptions { get; } = ["All"];

    [ObservableProperty]
    private string _selectedMode = "All";

    [ObservableProperty]
    private string _selectedSpeaker = "All";

    public ChatAnalyticsViewModel(IAnalyticsSession session)
    {
        _session = session;
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
    }

    partial void OnSelectedSpeakerChanged(string value)
    {
        if (_isRefreshing)
        {
            return;
        }

        Refresh(_session.GetSnapshot());
    }

    private static bool IsAll(string? value) =>
        string.IsNullOrWhiteSpace(value) || value.Equals("All", StringComparison.OrdinalIgnoreCase);

    private static bool Matches(string? filter, string value) =>
        IsAll(filter) || value.Equals(filter, StringComparison.OrdinalIgnoreCase);

    private void Refresh(AnalyticsSnapshotDto snapshot)
    {
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

            SpeakerOptions.Clear();
            SpeakerOptions.Add("All");

            foreach (var speaker in speakers)
            {
                SpeakerOptions.Add(speaker);
            }

            SelectedMode = IsAll(previousMode) || ModeOptions.Contains(previousMode)
                ? (IsAll(previousMode) ? "All" : previousMode)
                : "All";

            SelectedSpeaker = !IsAll(previousSpeaker) && speakers.Contains(previousSpeaker, StringComparer.OrdinalIgnoreCase)
                ? previousSpeaker
                : "All";

            Messages.Clear();

            foreach (var message in snapshot.ChatMessages.OrderByDescending(m => m.TimestampMs).Take(MaxDisplayedMessages))
            {
                if (!Matches(SelectedMode, message.Mode))
                {
                    continue;
                }

                if (!Matches(SelectedSpeaker, message.Speaker))
                {
                    continue;
                }

                Messages.Add(ChatMessageRow.From(message, snapshot.SessionStartMs));
            }
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    public void Dispose() => _subscription.Dispose();
}
