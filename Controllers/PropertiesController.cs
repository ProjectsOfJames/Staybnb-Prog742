using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;
using Staybnb.Services;
using Staybnb.ViewModels;

namespace Staybnb.Controllers;

[Authorize(Roles = "Guest")]
public class PropertiesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public PropertiesController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    // Gallery: only ACTIVE listings, optionally filtered by city and/or property type
    public async Task<IActionResult> Index(string? city, string? propertyType)
    {
        var active = _db.Properties.AsNoTracking().Where(p => p.IsActive);

        var query = active;
        if (!string.IsNullOrWhiteSpace(city))
            query = query.Where(p => p.City == city);
        if (!string.IsNullOrWhiteSpace(propertyType))
            query = query.Where(p => p.PropertyType == propertyType);

        var cards = await query
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new PropertyCardVM
            {
                Id = p.Id,
                Title = p.Title,
                City = p.City,
                PropertyType = p.PropertyType,
                PricePerNight = p.PricePerNight,
                ImageUrls = p.Images.Select(i => i.ImageUrl).ToList(),
                AverageRating = p.Reviews.Any() ? p.Reviews.Average(r => (double)r.Rating) : 0,
                ReviewCount = p.Reviews.Count
            })
            .ToListAsync();

        // Mark the cards this guest has saved (the heart shows filled)
        var userId = _userManager.GetUserId(User);
        var saved = await _db.WishlistItems.AsNoTracking()
            .Where(w => w.UserId == userId).Select(w => w.PropertyId).ToListAsync();
        foreach (var card in cards) card.IsWishlisted = saved.Contains(card.Id);

        var vm = new PropertyGalleryVM
        {
            Properties = cards,
            City = city,
            PropertyType = propertyType,
            Cities = await active.Select(p => p.City).Distinct().OrderBy(c => c).ToListAsync(),
            PropertyTypes = await active.Select(p => p.PropertyType).Distinct().OrderBy(t => t).ToListAsync()
        };

        return View(vm);
    }

    public async Task<IActionResult> Details(int id)
    {
        var p = await _db.Properties.AsNoTracking()
            .Include(x => x.Host)
            .Include(x => x.Images)
            .Include(x => x.Amenities)
            .Include(x => x.Reviews).ThenInclude(r => r.Reviewer)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id && x.IsActive);

        if (p == null) return NotFound();

        var vm = new PropertyDetailsVM
        {
            Id = p.Id,
            Title = p.Title,
            City = p.City,
            Address = p.Address,
            Description = p.Description,
            HostName = p.Host.FullName,
            HostId = p.HostId,
            PricePerNight = p.PricePerNight,
            CleaningFee = p.CleaningFee,
            ServiceFeeRate = PricingService.ServiceFeeRate,
            MaxGuests = p.MaxGuests,
            Bedrooms = p.Bedrooms,
            Beds = p.Beds,
            Bathrooms = p.Bathrooms,
            AverageRating = p.Reviews.Any() ? p.Reviews.Average(r => (double)r.Rating) : 0,
            ReviewCount = p.Reviews.Count,
            ImageUrls = p.Images.Select(i => i.ImageUrl).ToList(),
            Amenities = p.Amenities.OrderBy(a => a.Name)
                .Select(a => new AmenityVM { Id = a.Id, Name = a.Name, IconClass = a.IconClass }).ToList(),
            Reviews = p.Reviews.OrderByDescending(r => r.CreatedAt).Take(10)
                .Select(r => new ReviewVM
                {
                    ReviewerName = r.Reviewer.FullName,
                    Rating = r.Rating,
                    Comment = r.Comment,
                    CreatedAt = r.CreatedAt
                }).ToList(),
            Booking = new BookingVM { PropertyId = p.Id }
        };

        return View(vm);
    }
}