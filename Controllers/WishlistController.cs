using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;
using Staybnb.ViewModels;

namespace Staybnb.Controllers;

[Authorize(Roles = "Guest")]
public class WishlistController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public WishlistController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var me = _userManager.GetUserId(User);

        var cards = await _db.WishlistItems.AsNoTracking()
            .Where(w => w.UserId == me && w.Property.IsActive)
            .OrderByDescending(w => w.CreatedAt)
            .Select(w => new PropertyCardVM
            {
                Id = w.PropertyId,
                Title = w.Property.Title,
                City = w.Property.City,
                PropertyType = w.Property.PropertyType,
                PricePerNight = w.Property.PricePerNight,
                ImageUrls = w.Property.Images.Select(i => i.ImageUrl).ToList(),
                AverageRating = w.Property.Reviews.Any() ? w.Property.Reviews.Average(r => (double)r.Rating) : 0,
                ReviewCount = w.Property.Reviews.Count,
                IsWishlisted = true
            })
            .ToListAsync();

        return View(cards);
    }

    // Called by the heart buttons (fetch). Adds the property if it isn't saved, removes it if it is.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int propertyId)
    {
        var me = _userManager.GetUserId(User)!;

        var existing = await _db.WishlistItems.FirstOrDefaultAsync(w => w.UserId == me && w.PropertyId == propertyId);
        if (existing != null)
        {
            _db.WishlistItems.Remove(existing);
            await _db.SaveChangesAsync();
            return Json(new { wishlisted = false });
        }

        if (!await _db.Properties.AnyAsync(p => p.Id == propertyId && p.IsActive))
            return NotFound();

        _db.WishlistItems.Add(new WishlistItem { UserId = me, PropertyId = propertyId });
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Double click: the unique (user, property) index already has it, which is the result we wanted
        }

        return Json(new { wishlisted = true });
    }
}