using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;
using Staybnb.Services;

namespace Staybnb.Controllers;

/// <summary>Guests review a property after checking in. One review per guest per property.</summary>
[Authorize(Roles = "Guest")]
public class ReviewsController : Controller
{
    private const int MaxComment = 1000;

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly INotificationService _notifications;
    private readonly IActivityLogger _logger;

    public ReviewsController(ApplicationDbContext db, UserManager<ApplicationUser> userManager,
                             INotificationService notifications, IActivityLogger logger)
    {
        _db = db;
        _userManager = userManager;
        _notifications = notifications;
        _logger = logger;
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int bookingId, int rating, string? comment)
    {
        var userId = _userManager.GetUserId(User)!;

        var booking = await _db.Bookings.Include(b => b.Property)
            .FirstOrDefaultAsync(b => b.Id == bookingId && b.GuestId == userId);
        if (booking == null) return NotFound();

        string? error = null;
        var text = (comment ?? string.Empty).Trim();

        if (booking.Status != BookingStatus.CheckedIn && booking.Status != BookingStatus.Completed)
            error = "You can review a stay once you have checked in.";
        else if (rating < 1 || rating > 5)
            error = "Choose a rating from 1 to 5.";
        else if (text.Length > MaxComment)
            error = $"Reviews can be at most {MaxComment} characters.";
        else if (await _db.Reviews.AnyAsync(r => r.PropertyId == booking.PropertyId && r.ReviewerId == userId))
            error = "You have already reviewed this property.";

        if (error != null)
        {
            TempData["Error"] = error;
            return RedirectToAction("Details", "Bookings", new { id = bookingId });
        }

        _db.Reviews.Add(new Review
        {
            PropertyId = booking.PropertyId,
            ReviewerId = userId,
            Rating = rating,
            Comment = text
        });

        var guest = await _userManager.GetUserAsync(User);
        _notifications.Notify(booking.Property.HostId, "New review",
            $"{guest?.FullName ?? "A guest"} rated \"{booking.Property.Title}\" {rating}/5.",
            NotificationType.Info, "/Host/Reviews");

        _logger.Log(userId, $"ReviewPosted: {booking.Property.Title} ({rating}/5)", ActivityType.Other);

        await _db.SaveChangesAsync();

        TempData["Success"] = "Thanks for your review!";
        return RedirectToAction("Details", "Bookings", new { id = bookingId });
    }
}