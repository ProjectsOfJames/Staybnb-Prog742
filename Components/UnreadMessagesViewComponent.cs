using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;

namespace Staybnb.Components;

/// <summary>The envelope icon in the top bar, with the number of unread messages.</summary>
public class UnreadMessagesViewComponent : ViewComponent
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public UnreadMessagesViewComponent(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var userId = _userManager.GetUserId(UserClaimsPrincipal);
        var count = userId == null
            ? 0
            : await _db.Messages.CountAsync(m => m.ReceiverId == userId && !m.IsRead);

        return View(count);
    }
}