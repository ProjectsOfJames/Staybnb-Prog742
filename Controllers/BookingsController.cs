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
    private readonly IBookingAvailability _availability;
    private readonly INotificationService _notifications;
    private readonly IActivityLogger _logger;

    public BookingsController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, IPricingService pricing,
                              IBookingAvailability availability, INotificationService notifications, IActivityLogger logger)
    {
        _db = db;
        _userManager = userManager;
        _pricing = pricing;
        _availability = availability;
        _notifications = notifications;
        _logger = logger;
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

        var vm = ToVM(booking);

        // A guest can review a stay once they've checked in, and only once per property
        var review = await _db.Reviews.AsNoTracking()
            .FirstOrDefaultAsync(r => r.PropertyId == booking.PropertyId && r.ReviewerId == userId);

        vm.HasReviewed = review != null;
        vm.MyRating = review?.Rating;
        vm.MyComment = review?.Comment;
        vm.CanReview = review == null &&
                       (booking.Status == BookingStatus.CheckedIn || booking.Status == BookingStatus.Completed);

        return View(vm);
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
        else if (!await _availability.IsAvailableAsync(property.Id, checkIn, checkOut))
            error = "Those dates are no longer available. Please choose different dates.";

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

        var guest = await _userManager.GetUserAsync(User);
        var guestName = guest?.FullName ?? "A guest";
        _notifications.Notify(property.HostId, "New booking request",
            $"{guestName} requested \"{property.Title}\" for {checkIn:dd MMM} - {checkOut:dd MMM yyyy}.",
            NotificationType.BookingUpdate, "/Host/Bookings");

        await _db.SaveChangesAsync();

        await _logger.LogAsync(booking.GuestId, $"BookingRequested: #{booking.Id} {property.Title}", ActivityType.BookingStatusChange);

        TempData["Success"] = "Booking request sent! The host will review it shortly.";
        return RedirectToAction(nameof(Details), new { id = booking.Id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var userId = _userManager.GetUserId(User);
        var booking = await _db.Bookings
            .Include(b => b.Property)
            .Include(b => b.Payment)
            .FirstOrDefaultAsync(b => b.Id == id && b.GuestId == userId);
        if (booking == null) return NotFound();

        var cancellable = (booking.Status == BookingStatus.Pending || booking.Status == BookingStatus.Approved)
                          && booking.CheckInDate.Date > DateTime.Today;
        if (!cancellable)
        {
            TempData["Error"] = "This booking can no longer be cancelled.";
            return RedirectToAction(nameof(Details), new { id });
        }

        booking.Status = BookingStatus.Cancelled;

        // Money already paid is refunded; an unpaid invoice is simply removed
        if (booking.Payment != null)
        {
            if (booking.Payment.Status == PaymentStatus.Paid)
                booking.Payment.Status = PaymentStatus.Refunded;
            else
                _db.Payments.Remove(booking.Payment);
        }

        var guest = await _userManager.GetUserAsync(User);
        var guestName = guest?.FullName ?? "A guest";
        _logger.Log(booking.GuestId, $"BookingCancelled: #{booking.Id} {booking.Property.Title}", ActivityType.BookingStatusChange);

        _notifications.Notify(booking.Property.HostId, "Booking cancelled",
            $"{guestName} cancelled their booking for \"{booking.Property.Title}\".",
            NotificationType.Warning, "/Host/Bookings");

        await _db.SaveChangesAsync();

        TempData["Success"] = "Your booking was cancelled.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // Simulated payment: records the payment as Paid. No real card is charged.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Pay(int id)
    {
        var userId = _userManager.GetUserId(User);
        var booking = await _db.Bookings
            .Include(b => b.Property)
            .Include(b => b.Payment)
            .FirstOrDefaultAsync(b => b.Id == id && b.GuestId == userId);
        if (booking == null) return NotFound();

        var payable = (booking.Status == BookingStatus.Approved || booking.Status == BookingStatus.CheckedIn)
                      && booking.Payment != null && booking.Payment.Status == PaymentStatus.Pending;
        if (!payable)
        {
            TempData["Error"] = "There is nothing to pay for this booking.";
            return RedirectToAction(nameof(Details), new { id });
        }

        booking.Payment!.Status = PaymentStatus.Paid;

        _logger.Log(booking.GuestId, $"PaymentReceived: #{booking.Id} R {booking.Payment.Amount:0.00}", ActivityType.BookingStatusChange);

        _notifications.Notify(booking.Property.HostId, "Payment received",
            $"Payment of R {booking.Payment.Amount:0.00} for \"{booking.Property.Title}\" was received.",
            NotificationType.Success, "/Host/Earnings");

        await _db.SaveChangesAsync();

        TempData["Success"] = "Payment received. Thank you!";
        return RedirectToAction(nameof(Details), new { id });
    }

    private IQueryable<Booking> BookingQuery() =>
        _db.Bookings.AsNoTracking()
            .Include(b => b.Property).ThenInclude(p => p.Images)
            .Include(b => b.Property).ThenInclude(p => p.Host)
            .Include(b => b.GuestCheckIn)
            .Include(b => b.Payment)
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
        PaymentState = b.Payment?.Status,
        CreatedAt = b.CreatedAt
    };
}