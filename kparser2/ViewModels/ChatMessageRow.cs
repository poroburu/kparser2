using System.Windows;
using System.Windows.Media;
using kparser2.Abstractions;
using kparser2.Services;

namespace kparser2.ViewModels;

public sealed class ChatMessageRow
{
    public string Time { get; init; } = "";
    public string Mode { get; init; } = "";
    public string Speaker { get; init; } = "";
    public string Message { get; init; } = "";
    public bool IsLocalPlayer { get; init; }
    public Brush ModeForeground { get; init; } = Brushes.Gray;
    public Brush SpeakerForeground { get; init; } = SystemColors.ControlTextBrush;

    public static ChatMessageRow From(ChatMessageDto message, long sessionStartMs)
    {
        var speaker = string.IsNullOrWhiteSpace(message.Speaker) ? "Unknown" : message.Speaker;
        var displaySpeaker = message.IsLocalPlayer ? $"{speaker} (you)" : speaker;

        return new()
        {
            Time = SessionTimeFormat.FormatOffset(sessionStartMs, message.TimestampMs),
            Mode = message.Mode,
            Speaker = displaySpeaker,
            Message = message.Message,
            IsLocalPlayer = message.IsLocalPlayer,
            ModeForeground = ModeBrush(message.Mode),
            SpeakerForeground = message.IsLocalPlayer
                ? new SolidColorBrush(Color.FromRgb(0, 128, 0))
                : SystemColors.ControlTextBrush
        };
    }
    private static Brush ModeBrush(string mode) =>
        mode.ToLowerInvariant() switch
        {
            "tell" => Brushes.MediumOrchid,
            "party" => Brushes.DeepSkyBlue,
            "linkshell" or "linkshell2" => Brushes.Orange,
            "shout" or "yell" => Brushes.Goldenrod,
            "system" or "standard" => Brushes.Khaki,
            "emote" => Brushes.LightPink,
            _ => Brushes.Gray
        };
}
