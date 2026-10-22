using Staybnb.Models;

namespace Staybnb.ViewModels;

public class NotificationVM
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public bool IsRead { get; set; }
    public string? LinkUrl { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class NotificationsPageVM
{
    public string Filter { get; set; } = "all";
    public int TotalCount { get; set; }
    public int UnreadCount { get; set; }
    public int ReadCount { get; set; }
    public int TodayCount { get; set; }
    public List<NotificationVM> Items { get; set; } = new();
    public NotificationVM? Selected { get; set; }
}

public class NotificationBellVM
{
    public int UnreadCount { get; set; }
    public List<NotificationVM> Latest { get; set; } = new();
}