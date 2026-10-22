using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;
using Staybnb.ViewModels;

namespace Staybnb.Components;

/// <summary>The bell icon in the top bar: unread count plus a dropdown with the latest notifications.</summary>
public class NotificationBellViewComponent : ViewComponent
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public NotificationBellViewComponent(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var userId = _userManager.GetUserId(UserClaimsPrincipal);
        if (userId == null) return View(new NotificationBellVM());

        var unread = await _db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);

        var latest = await _db.Notifications.AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id)
            .Take(5)
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

        return View(new NotificationBellVM { UnreadCount = unread, Latest = latest });
    }
}