namespace Staybnb.Services;

public static class TimeAgo
{
    /// <summary>Friendly relative time for a UTC timestamp, e.g. "5m ago".</summary>
    public static string Format(DateTime utc)
    {
        var span = DateTime.UtcNow - utc;

        if (span.TotalSeconds < 60) return "just now";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
        if (span.TotalDays < 7) return $"{(int)span.TotalDays}d ago";

        return utc.ToLocalTime().ToString("dd MMM yyyy");
    }
}