using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;
using Staybnb.Services;
using Staybnb.ViewModels;

namespace Staybnb.Controllers;

[Authorize(Roles = "Host")]
public class HostController : Controller
{
    private const string DocFolder = StorageFolders.GuestDocuments;

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IFileStorage _files;
    private readonly INotificationService _notifications;
    private readonly IActivityLogger _logger;
    private readonly IBookingAvailability _availability;

    public HostController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, IFileStorage files,
                          INotificationService notifications, IActivityLogger logger, IBookingAvailability availability)
    {
        _db = db;
        _userManager = userManager;
        _files = files;
        _notifications = notifications;
        _logger = logger;
        _availability = availability;
    }

    private string UserId => _userManager.GetUserId(User)!;

    // Dashboard
    public async Task<IActionResult> Dashboard()
    {
        var uid = UserId;
        var user = await _userManager.GetUserAsync(User);

        var props = _db.Properties.AsNoTracking().Where(p => p.HostId == uid);
        var bookings = _db.Bookings.AsNoTracking().Where(b => b.Property.HostId == uid);
        var reviews = _db.Reviews.AsNoTracking().Where(r => r.Property.HostId == uid);
        var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

        var vm = new HostDashboardVM
        {
            HostName = user?.FullName ?? string.Empty,
            TotalProperties = await props.CountAsync(),
            ActiveProperties = await props.CountAsync(p => p.IsActive),
            TotalBookings = await bookings.CountAsync(),
            BookingsThisMonth = await bookings.CountAsync(b => b.CreatedAt >= monthStart),
            PendingRequests = await bookings.CountAsync(b => b.Status == BookingStatus.Pending),
            // Earnings = what the host receives (the guest's total minus the platform's service fee), once PAID
            TotalEarnings = await bookings
                .Where(b => b.Payment != null && b.Payment.Status == PaymentStatus.Paid)
                .SumAsync(b => (decimal?)(b.TotalPrice - b.ServiceFee)) ?? 0,
            ReviewCount = await reviews.CountAsync()
        };
        vm.AverageRating = vm.ReviewCount > 0 ? await reviews.AverageAsync(r => (double)r.Rating) : 0;

        return View(vm);
    }

    // Properties
    public async Task<IActionResult> Properties()
    {
        var uid = UserId;

        var rows = await _db.Properties.AsNoTracking()
            .Where(p => p.HostId == uid)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new HostPropertyRowVM
            {
                Id = p.Id,
                Title = p.Title,
                City = p.City,
                PropertyType = p.PropertyType,
                PricePerNight = p.PricePerNight,
                ImageUrl = p.Images.Select(i => i.ImageUrl).FirstOrDefault(),
                ImageCount = p.Images.Count,
                MaxGuests = p.MaxGuests,
                Bedrooms = p.Bedrooms,
                Beds = p.Beds,
                Bathrooms = p.Bathrooms,
                IsActive = p.IsActive,
                BookingCount = p.Bookings.Count,
                ReviewCount = p.Reviews.Count,
                ApplicationStatus = _db.HostApplications
                    .Where(a => a.PropertyId == p.Id)
                    .Select(a => a.Status)
                    .FirstOrDefault()
            })
            .ToListAsync();

        return View(rows);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var property = await _db.Properties.FirstOrDefaultAsync(p => p.Id == id && p.HostId == UserId);
        if (property == null) return NotFound();

        // Without this check a host could publish an unapproved listing just by toggling it
        var approved = await _db.HostApplications
            .AnyAsync(a => a.PropertyId == id && a.Status == ApplicationStatus.Approved);
        if (!approved)
        {
            TempData["Error"] = "A listing can only be activated after an admin has approved it.";
            return RedirectToAction(nameof(Properties));
        }

        property.IsActive = !property.IsActive;

        var verb = property.IsActive ? "PropertyActivated" : "PropertyDeactivated";
        _logger.Log(UserId, $"{verb}: {property.Title}", ActivityType.PropertyUpdate);

        await _db.SaveChangesAsync();

        TempData["Success"] = property.IsActive
            ? $"\"{property.Title}\" is now visible to guests."
            : $"\"{property.Title}\" is now hidden from guests.";
        return RedirectToAction(nameof(Properties));
    }

    [HttpGet]
    public async Task<IActionResult> EditProperty(int id)
    {
        var p = await _db.Properties.AsNoTracking()
            .Include(x => x.Amenities)
            .FirstOrDefaultAsync(x => x.Id == id && x.HostId == UserId);
        if (p == null) return NotFound();

        var vm = new HostPropertyEditVM
        {
            Id = p.Id,
            Title = p.Title,
            Description = p.Description,
            PricePerNight = p.PricePerNight,
            CleaningFee = p.CleaningFee,
            Address = p.Address,
            City = p.City,
            PropertyType = p.PropertyType,
            MaxGuests = p.MaxGuests,
            Bedrooms = p.Bedrooms,
            Beds = p.Beds,
            Bathrooms = p.Bathrooms,
            SelectedAmenityIds = p.Amenities.Select(a => a.Id).ToList()
        };
        await FillAmenitiesAsync(vm);
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditProperty(int id, HostPropertyEditVM model)
    {
        var property = await _db.Properties
            .Include(x => x.Amenities)
            .FirstOrDefaultAsync(x => x.Id == id && x.HostId == UserId);
        if (property == null) return NotFound();

        if (!ModelState.IsValid)
        {
            model.Id = id;
            await FillAmenitiesAsync(model);
            return View(model);
        }

        property.Title = model.Title.Trim();
        property.Description = model.Description.Trim();
        property.PricePerNight = model.PricePerNight;
        property.CleaningFee = model.CleaningFee;
        property.Address = model.Address.Trim();
        property.City = model.City.Trim();
        property.PropertyType = model.PropertyType;
        property.MaxGuests = model.MaxGuests;
        property.Bedrooms = model.Bedrooms;
        property.Beds = model.Beds;
        property.Bathrooms = model.Bathrooms;

        property.Amenities.Clear();
        var chosen = await _db.Amenities.Where(a => model.SelectedAmenityIds.Contains(a.Id)).ToListAsync();
        foreach (var amenity in chosen) property.Amenities.Add(amenity);

        _logger.Log(UserId, $"PropertyEdited: {property.Title}", ActivityType.PropertyUpdate);

        await _db.SaveChangesAsync();

        TempData["Success"] = "Property updated.";
        return RedirectToAction(nameof(Properties));
    }

    // Booking requests
    public async Task<IActionResult> Bookings()
    {
        var uid = UserId;

        var rows = await _db.Bookings.AsNoTracking()
            .Where(b => b.Property.HostId == uid)
            .OrderBy(b => b.Status == BookingStatus.Pending ? 0 : 1)
            .ThenByDescending(b => b.CreatedAt)
            .Select(b => new HostBookingRowVM
            {
                Id = b.Id,
                PropertyTitle = b.Property.Title,
                GuestId = b.GuestId,
                GuestName = b.Guest.FirstName + " " + b.Guest.LastName,
                GuestEmail = b.Guest.Email ?? string.Empty,
                CheckInDate = b.CheckInDate,
                CheckOutDate = b.CheckOutDate,
                NumberOfGuests = b.NumberOfGuests,
                TotalPrice = b.TotalPrice,
                HostEarning = b.TotalPrice - b.ServiceFee,
                Status = b.Status,
                GuestCheckInStatus = b.GuestCheckIn == null ? (CheckInStatus?)null : b.GuestCheckIn.Status
            })
            .ToListAsync();

        return View(rows);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveBooking(int id)
    {
        var booking = await OwnBookingAsync(id);
        if (booking == null) return NotFound();

        if (booking.Status != BookingStatus.Pending)
        {
            TempData["Error"] = "Only pending requests can be approved.";
            return RedirectToAction(nameof(Bookings));
        }

        // Never approve two stays that overlap (same-day turnover is fine)
        var conflict = !await _availability.IsAvailableAsync(booking.PropertyId, booking.CheckInDate, booking.CheckOutDate, booking.Id);

        if (conflict)
        {
            TempData["Error"] = "These dates overlap another approved booking, so this request can't be approved.";
            return RedirectToAction(nameof(Bookings));
        }

        booking.Status = BookingStatus.Approved;

        // Approval raises the invoice; the guest pays from their booking page
        if (!await _db.Payments.AnyAsync(p => p.BookingId == booking.Id))
            _db.Payments.Add(new Payment { BookingId = booking.Id, Amount = booking.TotalPrice, Status = PaymentStatus.Pending });

        _logger.Log(UserId, $"BookingApproved: #{booking.Id} {booking.Property.Title}", ActivityType.BookingStatusChange);

        _notifications.Notify(booking.GuestId, "Booking Approved",
            $"Your booking for \"{booking.Property.Title}\" ({booking.CheckInDate:dd MMM} - {booking.CheckOutDate:dd MMM yyyy}) was approved. You can now complete check-in.",
            NotificationType.Success, $"/Bookings/Details/{booking.Id}");

        await _db.SaveChangesAsync();

        TempData["Success"] = "Booking approved.";
        return RedirectToAction(nameof(Bookings));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectBooking(int id)
    {
        var booking = await OwnBookingAsync(id);
        if (booking == null) return NotFound();

        if (booking.Status != BookingStatus.Pending)
        {
            TempData["Error"] = "Only pending requests can be rejected.";
            return RedirectToAction(nameof(Bookings));
        }

        booking.Status = BookingStatus.Rejected;

        _logger.Log(UserId, $"BookingRejected: #{booking.Id} {booking.Property.Title}", ActivityType.BookingStatusChange);

        _notifications.Notify(booking.GuestId, "Booking Rejected",
            $"Unfortunately your request for \"{booking.Property.Title}\" was not accepted.",
            NotificationType.Warning, $"/Bookings/Details/{booking.Id}");

        await _db.SaveChangesAsync();

        TempData["Success"] = "Booking rejected.";
        return RedirectToAction(nameof(Bookings));
    }

    // Guest check-in verification
    [HttpGet]
    public async Task<IActionResult> VerifyCheckIn(int id)
    {
        var booking = await _db.Bookings.AsNoTracking()
            .Include(b => b.Property)
            .Include(b => b.Guest)
            .Include(b => b.GuestCheckIn).ThenInclude(c => c!.Documents)
            .FirstOrDefaultAsync(b => b.Id == id && b.Property.HostId == UserId);
        if (booking == null) return NotFound();

        return View(new HostCheckInVM
        {
            BookingId = booking.Id,
            PropertyTitle = booking.Property.Title,
            GuestName = booking.Guest.FullName,
            GuestEmail = booking.Guest.Email ?? string.Empty,
            CheckInDate = booking.CheckInDate,
            CheckOutDate = booking.CheckOutDate,
            NumberOfGuests = booking.NumberOfGuests,
            Status = booking.Status,
            GuestCheckInStatus = booking.GuestCheckIn?.Status,
            Documents = booking.GuestCheckIn == null
                ? new List<GuestDocumentRowVM>()
                : booking.GuestCheckIn.Documents.OrderBy(d => d.Id)
                    .Select(d => new GuestDocumentRowVM { Id = d.Id, DocumentType = d.DocumentType, Status = d.Status })
                    .ToList()
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyDocument(int id, bool approve)
    {
        var doc = await _db.GuestDocuments
            .Include(d => d.GuestCheckIn).ThenInclude(c => c.Documents)
            .Include(d => d.GuestCheckIn).ThenInclude(c => c.Booking).ThenInclude(b => b.Property)
            .FirstOrDefaultAsync(d => d.Id == id && d.GuestCheckIn.Booking.Property.HostId == UserId);
        if (doc == null) return NotFound();

        var checkIn = doc.GuestCheckIn;
        var bookingId = checkIn.BookingId;

        if (checkIn.Status != CheckInStatus.DocumentsSubmitted)
        {
            TempData["Error"] = "The guest hasn't submitted their documents for review.";
            return RedirectToAction(nameof(VerifyCheckIn), new { id = bookingId });
        }

        doc.Status = approve ? DocumentStatus.Verified : DocumentStatus.Rejected;

        // Still waiting on other documents -> stay submitted.
        // Everything reviewed -> verified if at least one was accepted, otherwise rejected.
        if (checkIn.Documents.Any(d => d.Status == DocumentStatus.Pending))
            checkIn.Status = CheckInStatus.DocumentsSubmitted;
        else
            checkIn.Status = checkIn.Documents.Any(d => d.Status == DocumentStatus.Verified)
                ? CheckInStatus.Verified
                : CheckInStatus.Rejected;

        var title = checkIn.Booking.Property.Title;
        var docAction = approve ? "DocumentVerified" : "DocumentRejected";
        _logger.Log(UserId, $"{docAction}: booking #{bookingId} {title}", ActivityType.CheckIn);

        if (approve)
            _notifications.Notify(checkIn.Booking.GuestId, "Document verified",
                $"Your document for \"{title}\" was verified.",
                NotificationType.Success, $"/CheckIn/Confirm/{bookingId}");
        else
            _notifications.Notify(checkIn.Booking.GuestId, "Document rejected",
                $"Your document for \"{title}\" could not be verified. Please upload a clearer one.",
                NotificationType.Warning, $"/CheckIn/Upload/{bookingId}");

        await _db.SaveChangesAsync();

        TempData["Success"] = approve ? "Document verified." : "Document rejected. The guest can upload a new one.";
        return RedirectToAction(nameof(VerifyCheckIn), new { id = bookingId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkCheckedIn(int id)
    {
        var booking = await _db.Bookings
            .Include(b => b.Property)
            .Include(b => b.GuestCheckIn)
            .FirstOrDefaultAsync(b => b.Id == id && b.Property.HostId == UserId);
        if (booking == null) return NotFound();

        if (booking.Status != BookingStatus.Approved || booking.GuestCheckIn?.Status != CheckInStatus.Verified)
        {
            TempData["Error"] = "The guest can only be checked in once their documents are verified.";
            return RedirectToAction(nameof(VerifyCheckIn), new { id });
        }

        booking.Status = BookingStatus.CheckedIn;

        _logger.Log(UserId, $"GuestCheckedIn: #{booking.Id} {booking.Property.Title}", ActivityType.BookingStatusChange);

        _notifications.Notify(booking.GuestId, "You're checked in",
            $"You're checked in at \"{booking.Property.Title}\". Enjoy your stay!",
            NotificationType.Success, $"/Bookings/Details/{booking.Id}");

        await _db.SaveChangesAsync();

        TempData["Success"] = "Guest checked in.";
        return RedirectToAction(nameof(VerifyCheckIn), new { id });
    }

    // Hosts can open the documents guests uploaded for THEIR properties only
    [HttpGet]
    public async Task<IActionResult> Document(int id)
    {
        var doc = await _db.GuestDocuments.AsNoTracking()
            .Include(d => d.GuestCheckIn).ThenInclude(c => c.Booking).ThenInclude(b => b.Property)
            .FirstOrDefaultAsync(d => d.Id == id && d.GuestCheckIn.Booking.Property.HostId == UserId);
        if (doc == null) return NotFound();

        var path = _files.GetPhysicalPath(DocFolder, doc.FileName);
        if (!System.IO.File.Exists(path)) return NotFound();

        new FileExtensionContentTypeProvider().TryGetContentType(path, out var contentType);
        return PhysicalFile(path, contentType ?? "application/octet-stream");
    }

    // Check-in process configuration
    [HttpGet]
    public async Task<IActionResult> ConfigureCheckIn(int id)
    {
        var property = await _db.Properties.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id && p.HostId == UserId);
        if (property == null) return NotFound();

        var process = await _db.CheckInProcesses.AsNoTracking().FirstOrDefaultAsync(c => c.PropertyId == id);

        var steps = process != null
            ? CheckInStepsSerializer.Parse(process.StepsJson)
            : CheckInStepsSerializer.DefaultSteps.ToList();

        return View(new CheckInConfigVM
        {
            PropertyId = property.Id,
            PropertyTitle = property.Title,
            Title = process?.Title ?? "Guest check-in",
            Steps = steps
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfigureCheckIn(int id, CheckInConfigVM model)
    {
        var property = await _db.Properties.FirstOrDefaultAsync(p => p.Id == id && p.HostId == UserId);
        if (property == null) return NotFound();

        model.PropertyId = property.Id;
        model.PropertyTitle = property.Title;

        // Blank rows are ignored, so they must not cause validation errors
        foreach (var key in ModelState.Keys.Where(k => k.StartsWith("Steps")).ToList())
            ModelState.Remove(key);

        var steps = (model.Steps ?? new List<CheckInStepDto>())
            .Where(s => !string.IsNullOrWhiteSpace(s.Title))
            .Select(s => new CheckInStepDto { Title = s.Title.Trim(), Description = (s.Description ?? string.Empty).Trim() })
            .ToList();

        if (steps.Count == 0)
            ModelState.AddModelError(string.Empty, "Add at least one rule or step.");

        if (!ModelState.IsValid)
        {
            if (steps.Count > 0) model.Steps = steps;
            return View(model);
        }

        var process = await _db.CheckInProcesses.FirstOrDefaultAsync(c => c.PropertyId == id);
        if (process == null)
        {
            process = new CheckInProcess { PropertyId = id };
            _db.CheckInProcesses.Add(process);
        }

        process.Title = model.Title.Trim();
        process.StepsJson = CheckInStepsSerializer.Serialize(steps);

        _logger.Log(UserId, $"CheckInRulesUpdated: {property.Title}", ActivityType.PropertyUpdate);

        await _db.SaveChangesAsync();

        TempData["Success"] = "Check-in process saved.";
        return RedirectToAction(nameof(Properties));
    }

    // Earnings
    public async Task<IActionResult> Earnings()
    {
        var uid = UserId;

        var rows = await _db.Bookings.AsNoTracking()
            .Where(b => b.Property.HostId == uid &&
                        (b.Status == BookingStatus.Approved || b.Status == BookingStatus.CheckedIn || b.Status == BookingStatus.Completed))
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new HostEarningRowVM
            {
                BookingId = b.Id,
                PropertyTitle = b.Property.Title,
                GuestName = b.Guest.FirstName + " " + b.Guest.LastName,
                CheckInDate = b.CheckInDate,
                CheckOutDate = b.CheckOutDate,
                Earning = b.TotalPrice - b.ServiceFee,
                BookingStatus = b.Status,
                PaymentState = b.Payment == null ? (PaymentStatus?)null : b.Payment.Status
            })
            .ToListAsync();

        var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var paid = rows.Where(r => r.PaymentState == PaymentStatus.Paid).ToList();

        return View(new HostEarningsVM
        {
            TotalEarned = paid.Sum(r => r.Earning),
            EarnedThisMonth = paid.Where(r => r.CheckInDate >= monthStart).Sum(r => r.Earning),
            AwaitingPayment = rows.Where(r => r.PaymentState != PaymentStatus.Paid).Sum(r => r.Earning),
            Rows = rows
        });
    }

    // Reviews
    public async Task<IActionResult> Reviews()
    {
        var uid = UserId;

        var rows = await _db.Reviews.AsNoTracking()
            .Where(r => r.Property.HostId == uid)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new HostReviewRowVM
            {
                PropertyTitle = r.Property.Title,
                ReviewerName = r.Reviewer.FirstName + " " + r.Reviewer.LastName,
                Rating = r.Rating,
                Comment = r.Comment,
                CreatedAt = r.CreatedAt
            })
            .ToListAsync();

        return View(new HostReviewsVM
        {
            Average = rows.Any() ? rows.Average(r => r.Rating) : 0,
            Rows = rows
        });
    }

    // Helpers
    private async Task<Booking?> OwnBookingAsync(int id) =>
        await _db.Bookings.Include(b => b.Property)
            .FirstOrDefaultAsync(b => b.Id == id && b.Property.HostId == UserId);

    private async Task FillAmenitiesAsync(HostPropertyEditVM model)
    {
        model.AvailableAmenities = await _db.Amenities.AsNoTracking()
            .OrderBy(a => a.Name)
            .Select(a => new AmenityVM { Id = a.Id, Name = a.Name, IconClass = a.IconClass })
            .ToListAsync();
    }
}