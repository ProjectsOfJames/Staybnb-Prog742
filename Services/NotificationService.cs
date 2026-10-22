using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;

namespace Staybnb.Services;

public interface INotificationService
{
    /// <summary>
    /// Queues a notification. It is saved by the caller's next SaveChangesAsync, so the notification
    /// and the change that caused it are stored together (or not at all).
    /// </summary>
    void Notify(string userId, string title, string message, NotificationType type, string? linkUrl = null);

    /// <summary>Queues the same notification for every Admin and SuperAdmin (saved by the caller's next SaveChangesAsync).</summary>
    Task NotifyAdminsAsync(string title, string message, NotificationType type, string? linkUrl = null, string? exceptUserId = null);
}

public class NotificationService : INotificationService
{
    private const int TitleMax = 100;

    private readonly ApplicationDbContext _db;

    public NotificationService(ApplicationDbContext db) => _db = db;

    public void Notify(string userId, string title, string message, NotificationType type, string? linkUrl = null)
    {
        _db.Notifications.Add(new Notification
        {
            UserId = userId,
            Title = title.Length > TitleMax ? title[..TitleMax] : title,
            Message = message,
            Type = type,
            LinkUrl = linkUrl,
            IsRead = false
        });
    }

    public async Task NotifyAdminsAsync(string title, string message, NotificationType type, string? linkUrl = null, string? exceptUserId = null)
    {
        // SuperAdmin also holds the Admin role, so one query covers both
        var adminIds = await (from ur in _db.UserRoles
                              join r in _db.Roles on ur.RoleId equals r.Id
                              where r.Name == RoleSeeder.Admin
                              select ur.UserId).Distinct().ToListAsync();

        foreach (var id in adminIds.Where(id => id != exceptUserId))
            Notify(id, title, message, type, linkUrl);
    }
}