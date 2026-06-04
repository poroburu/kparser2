namespace kparser2.Services;

public static class SessionTimeFormat
{
    public static string FormatOffset(long sessionStartMs, long offsetMs)
    {
        if (sessionStartMs > 0)
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(sessionStartMs + offsetMs)
                .ToLocalTime()
                .ToString("HH:mm:ss");
        }

        return FormatElapsed(offsetMs);
    }

    public static string FormatElapsed(long offsetMs)
    {
        var span = TimeSpan.FromMilliseconds(offsetMs);

        if (span.TotalHours >= 1)
        {
            return span.ToString(@"h\:mm\:ss");
        }

        if (span.TotalMinutes >= 1)
        {
            return span.ToString(@"m\:ss");
        }

        return $"{span.Seconds}.{span.Milliseconds / 100}s";
    }
}
