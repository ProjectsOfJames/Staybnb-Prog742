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
public class BookingsController : Controller
{
    private const int MaxNights = 90;

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IPricingService _pricing;

    public BookingsController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, IPricingService pricing)
    {
        _db = db;
        _userManager = userManager;
        _pricing = pricing;
    }

    // My Bookings
    public async Task<IActionResult> Index()
    {
        var userId = _userManager.GetUserId(User);

        var bookings = await BookingQuery()
            .Where(b => b.GuestId == userId)
            .OrderByDescending(b => b.CheckInDate)
            .ToListAsync();

        return View(bookings.Select(ToVM).ToList());
    }

    public async Task<IActionResult> Details(int id)
    {
        var userId = _userManager.GetUserId(User);

        var booking = await BookingQuery().FirstOrDefaultAsync(b => b.Id == id && b.GuestId == userId);
        if (booking == null) return NotFound();

        return View(ToVM(booking));
    }

    // Booking engine: validates, checks availability, and prices the stay ON THE SERVER
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BookingVM model)
    {
        var property = await _db.Properties.FirstOrDefaultAsync(p => p.Id == model.PropertyId && p.IsActive);
        if (property == null) return NotFound();

        var checkIn = model.CheckInDate.Date;
        var checkOut = model.CheckOutDate.Date;
        string? error = null;

        if (!ModelState.IsValid)
            error = "Please check your dates and number of guests.";
        else if (checkIn < DateTime.Today)
            error = "Check-in cannot be in the past.";
        else if (checkOut <= checkIn)
            error = "Check-out must be after check-in.";
        else if ((checkOut - checkIn).Days > MaxNights)
            error = $"Stays are limited to {MaxNights} nights.";
        else if (model.NumberOfGuests < 1 || model.NumberOfGuests > property.MaxGuests)
            error = $"This property sleeps up to {property.MaxGuests} guests.";
        else
        {
            // Two stays overlap when each starts before the other ends.
            // Same-day turnover is allowed (check-out day == next check-in day).
            var taken = await _db.Bookings.AnyAsync(b =>
                b.PropertyId == property.Id &&
                (b.Status == BookingStatus.Approved || b.Status == BookingStatus.CheckedIn) &&
                b.CheckInDate < checkOut && b.CheckOutDate > checkIn);

            if (taken) error = "Those dates are no longer available. Please choose different dates.";
        }

        if (error != null)
        {
            TempData["Error"] = error;
            return RedirectToAction("Details", "Properties", new { id = property.Id });
        }

        var price = _pricing.Calculate(property.PricePerNight, property.CleaningFee, checkIn, checkOut);

        var booking = new Booking
        {
            PropertyId = property.Id,
            GuestId = _userManager.GetUserId(User)!,
            CheckInDate = checkIn,
            CheckOutDate = checkOut,
            NumberOfGuests = model.NumberOfGuests,
            CleaningFee = price.CleaningFee,
            ServiceFee = price.ServiceFee,
            TotalPrice = price.TotalPrice,
            Status = BookingStatus.Pending
        };

        _db.Bookings.Add(booking);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Booking request sent! The host will review it shortly.";
        return RedirectToAction(nameof(Details), new { id = booking.Id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var userId = _userManager.GetUserId(User);
        var booking = await _db.Bookings.FirstOrDefaultAsync(b => b.Id == id && b.GuestId == userId);
        if (booking == null) return NotFound();

        var cancellable = (booking.Status == BookingStatus.Pending || booking.Status == BookingStatus.Approved)
                          && booking.CheckInDate.Date > DateTime.Today;
        if (!cancellable)
        {
            TempData["Error"] = "This booking can no longer be cancelled.";
            return RedirectToAction(nameof(Details), new { id });
        }

        booking.Status = BookingStatus.Cancelled;
        await _db.SaveChangesAsync();

        TempData["Success"] = "Your booking was cancelled.";
        return RedirectToAction(nameof(Details), new { id });
    }

    private IQueryable<Booking> BookingQuery() =>
        _db.Bookings.AsNoTracking()
            .Include(b => b.Property).ThenInclude(p => p.Images)
            .Include(b => b.Property).ThenInclude(p => p.Host)
            .Include(b => b.GuestCheckIn)
            .AsSplitQuery();

    private static GuestBookingVM ToVM(Booking b) => new()
    {
        Id = b.Id,
        PropertyId = b.PropertyId,
        PropertyTitle = b.Property.Title,
        City = b.Property.City,
        Address = b.Property.Address,
        HostName = b.Property.Host.FullName,
        HostId = b.Property.HostId,
        ImageUrl = b.Property.Images.Select(i => i.ImageUrl).FirstOrDefault(),
        CheckInDate = b.CheckInDate,
        CheckOutDate = b.CheckOutDate,
        NumberOfGuests = b.NumberOfGuests,
        CleaningFee = b.CleaningFee,
        ServiceFee = b.ServiceFee,
        TotalPrice = b.TotalPrice,
        Status = b.Status,
        CheckInStatus = b.GuestCheckIn?.Status,
        CreatedAt = b.CreatedAt
    };
}