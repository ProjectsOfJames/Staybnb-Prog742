using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;
using Staybnb.ViewModels;

namespace Staybnb.Controllers;

/// <summary>The notification centre. Every signed-in user only ever sees their own notifications.</summary>
[Authorize]
public class NotificationsController : Controller
{
    private const int MaxShown = 100;

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public NotificationsController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    private string UserId => _userManager.GetUserId(User)!;

    public async Task<IActionResult> Index(string? filter = "all", int? selectedId = null)
    {
        var me = UserId;
        filter = NormaliseFilter(filter);

        // Opening a notification marks it as read (saved in the database)
        NotificationVM? selected = null;
        if (selectedId.HasValue)
        {
            var n = await _db.Notifications.FirstOrDefaultAsync(x => x.Id == selectedId.Value && x.UserId == me);
            if (n != null)
            {
                if (!n.IsRead)
                {
                    n.IsRead = true;
                    await _db.SaveChangesAsync();
                }
                selected = ToVM(n);
            }
        }

        var mine = _db.Notifications.AsNoTracking().Where(n => n.UserId == me);
        var todayStart = DateTime.Today.ToUniversalTime();

        var query = filter switch
        {
            "unread" => mine.Where(n => !n.IsRead),
            "read" => mine.Where(n => n.IsRead),
            "today" => mine.Where(n => n.CreatedAt >= todayStart),
            _ => mine
        };

        var items = await query
            .OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id)
            .Take(MaxShown)
            .Select(n => new NotificationVM
            {
                Id = n.Id,
                Title = n.Title,
                Message = n.Message,
                Type = n.Type,
                IsRead = n.IsRead,
                LinkUrl = n.LinkUrl,
                CreatedAt = n.CreatedAt
            })
            .ToListAsync();

        return View(new NotificationsPageVM
        {
            Filter = filter,
            TotalCount = await mine.CountAsync(),
            UnreadCount = await mine.CountAsync(n => !n.IsRead),
            ReadCount = await mine.CountAsync(n => n.IsRead),
            TodayCount = await mine.CountAsync(n => n.CreatedAt >= todayStart),
            Items = items,
            Selected = selected
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead(string? filter = "all")
    {
        var me = UserId;

        await _db.Notifications
            .Where(n => n.UserId == me && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));

        return RedirectToAction(nameof(Index), new { filter });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, string? filter = "all")
    {
        var me = UserId;

        // The UserId check means nobody can delete someone else's notification
        await _db.Notifications
            .Where(n => n.Id == id && n.UserId == me)
            .ExecuteDeleteAsync();

        return RedirectToAction(nameof(Index), new { filter });
    }

    private static string NormaliseFilter(string? filter) => filter?.ToLowerInvariant() switch
    {
        "unread" => "unread",
        "read" => "read",
        "today" => "today",
        _ => "all"
    };

    private static NotificationVM ToVM(Notification n) => new()
    {
        Id = n.Id,
        Title = n.Title,
        Message = n.Message,
        Type = n.Type,
        IsRead = n.IsRead,
        LinkUrl = n.LinkUrl,
        CreatedAt = n.CreatedAt
    };
}