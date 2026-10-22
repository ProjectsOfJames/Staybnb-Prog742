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

/// <summary>
/// Guest check-in wizard for an APPROVED booking:
///   1. Rules    - read and accept the property's rules
///   2. Upload   - upload an ID or passport (GuestDocument)
///   3. Confirm  - review and submit for the host to verify
/// </summary>
[Authorize(Roles = "Guest")]
public class CheckInController : Controller
{
    private const string DocFolder = StorageFolders.GuestDocuments;
    private const long MaxBytes = 5 * 1024 * 1024;
    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".pdf" };
    private static readonly string[] DocumentTypes = { "ID", "Passport" };

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IFileStorage _files;
    private readonly INotificationService _notifications;
    private readonly IActivityLogger _logger;

    public CheckInController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, IFileStorage files, INotificationService notifications, IActivityLogger logger)
    {
        _db = db;
        _userManager = userManager;
        _files = files;
        _notifications = notifications;
        _logger = logger;
    }

    // Step 1: Rules
    [HttpGet]
    public async Task<IActionResult> Rules(int id)
    {
        var booking = await LoadBookingAsync(id);
        if (booking == null) return NotFound();

        var blocked = GuardApproved(booking);
        if (blocked != null) return blocked;

        if (IsLocked(booking)) return RedirectToAction(nameof(Confirm), new { id });

        var process = await GetOrCreateProcessAsync(booking.PropertyId);
        return View(new CheckInRulesVM
        {
            BookingId = booking.Id,
            PropertyTitle = booking.Property.Title,
            ProcessTitle = process.Title,
            Steps = CheckInStepsSerializer.Parse(process.StepsJson)
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Rules(int id, CheckInRulesVM model)
    {
        var booking = await LoadBookingAsync(id);
        if (booking == null) return NotFound();

        var blocked = GuardApproved(booking);
        if (blocked != null) return blocked;

        if (IsLocked(booking)) return RedirectToAction(nameof(Confirm), new { id });

        var process = await GetOrCreateProcessAsync(booking.PropertyId);

        if (!model.Agree)
        {
            ModelState.AddModelError(nameof(model.Agree), "You must accept the property rules to continue.");
            model.BookingId = booking.Id;
            model.PropertyTitle = booking.Property.Title;
            model.ProcessTitle = process.Title;
            model.Steps = CheckInStepsSerializer.Parse(process.StepsJson);
            return View(model);
        }

        if (booking.GuestCheckIn == null)
        {
            _db.GuestCheckIns.Add(new GuestCheckIn
            {
                BookingId = booking.Id,
                CheckInProcessId = process.Id,
                Status = CheckInStatus.InProgress
            });

            _logger.Log(booking.GuestId, $"CheckInStarted: booking #{booking.Id}", ActivityType.CheckIn);

            await _db.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Upload), new { id });
    }

    // Step 2: Upload document
    [HttpGet]
    public async Task<IActionResult> Upload(int id)
    {
        var booking = await LoadBookingAsync(id);
        if (booking == null) return NotFound();

        var blocked = GuardApproved(booking);
        if (blocked != null) return blocked;

        if (booking.GuestCheckIn == null) return RedirectToAction(nameof(Rules), new { id });
        if (IsLocked(booking)) return RedirectToAction(nameof(Confirm), new { id });

        return View(BuildUploadVM(booking));
    }

    [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> Upload(int id, CheckInUploadVM model)
    {
        var booking = await LoadBookingAsync(id);
        if (booking == null) return NotFound();

        var blocked = GuardApproved(booking);
        if (blocked != null) return blocked;

        if (booking.GuestCheckIn == null) return RedirectToAction(nameof(Rules), new { id });
        if (IsLocked(booking)) return RedirectToAction(nameof(Confirm), new { id });

        if (!DocumentTypes.Contains(model.DocumentType))
            ModelState.AddModelError(nameof(model.DocumentType), "Choose either ID or Passport.");

        string? storedName = null;
        if (ModelState.IsValid)
        {
            try
            {
                storedName = await _files.SaveAsync(model.File!, DocFolder, AllowedExtensions, MaxBytes);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(nameof(model.File), ex.Message);
            }
        }

        if (storedName == null)
        {
            var vm = BuildUploadVM(booking);
            vm.DocumentType = model.DocumentType;
            return View(vm);
        }

        _db.GuestDocuments.Add(new GuestDocument
        {
            GuestCheckInId = booking.GuestCheckIn.Id,
            DocumentType = model.DocumentType,
            FileName = storedName,
            Status = DocumentStatus.Pending
        });

        _logger.Log(booking.GuestId, $"DocumentUploaded: booking #{booking.Id} ({model.DocumentType})", ActivityType.CheckIn);

        // A re-upload after a rejection puts the check-in back in progress
        if (booking.GuestCheckIn.Status == CheckInStatus.Rejected)
            booking.GuestCheckIn.Status = CheckInStatus.InProgress;

        await _db.SaveChangesAsync();

        TempData["Success"] = "Document uploaded.";
        return RedirectToAction(nameof(Confirm), new { id });
    }

    // Step 3: Review and submit
    [HttpGet]
    public async Task<IActionResult> Confirm(int id)
    {
        var booking = await LoadBookingAsync(id);
        if (booking == null) return NotFound();

        if (booking.Status != BookingStatus.Approved && booking.Status != BookingStatus.CheckedIn)
        {
            TempData["Error"] = "Check-in opens once the host approves your booking.";
            return RedirectToAction("Details", "Bookings", new { id });
        }

        if (booking.GuestCheckIn == null) return RedirectToAction(nameof(Rules), new { id });

        return View(new CheckInConfirmVM
        {
            BookingId = booking.Id,
            PropertyTitle = booking.Property.Title,
            Status = booking.GuestCheckIn.Status,
            Documents = DocumentRows(booking.GuestCheckIn)
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(int id)
    {
        var booking = await LoadBookingAsync(id);
        if (booking == null) return NotFound();

        var blocked = GuardApproved(booking);
        if (blocked != null) return blocked;

        var checkIn = booking.GuestCheckIn;
        if (checkIn == null) return RedirectToAction(nameof(Rules), new { id });

        var canSubmit = checkIn.Documents.Count > 0 &&
                        (checkIn.Status == CheckInStatus.InProgress || checkIn.Status == CheckInStatus.Rejected);
        if (!canSubmit)
        {
            TempData["Error"] = "Upload at least one document before submitting.";
            return RedirectToAction(nameof(Confirm), new { id });
        }

        checkIn.Status = CheckInStatus.DocumentsSubmitted;

        _logger.Log(booking.GuestId, $"CheckInSubmitted: booking #{booking.Id}", ActivityType.CheckIn);

        var guest = await _userManager.GetUserAsync(User);
        var guestName = guest?.FullName ?? "A guest";
        _notifications.Notify(booking.Property.HostId, "Check-in submitted",
            $"{guestName} submitted documents for \"{booking.Property.Title}\". Please verify them.",
            NotificationType.BookingUpdate, $"/Host/VerifyCheckIn/{booking.Id}");

        await _db.SaveChangesAsync();

        TempData["Success"] = "Check-in submitted. The host will verify your document.";
        return RedirectToAction(nameof(Confirm), new { id });
    }

    // Guests can view their own uploaded documents
    [HttpGet]
    public async Task<IActionResult> Download(int id)
    {
        var userId = _userManager.GetUserId(User);

        var doc = await _db.GuestDocuments.AsNoTracking()
            .Include(d => d.GuestCheckIn).ThenInclude(c => c.Booking)
            .FirstOrDefaultAsync(d => d.Id == id && d.GuestCheckIn.Booking.GuestId == userId);
        if (doc == null) return NotFound();

        var path = _files.GetPhysicalPath(DocFolder, doc.FileName);
        if (!System.IO.File.Exists(path)) return NotFound();

        new FileExtensionContentTypeProvider().TryGetContentType(path, out var contentType);
        return PhysicalFile(path, contentType ?? "application/octet-stream");
    }

    // Helpers
    private async Task<Booking?> LoadBookingAsync(int bookingId)
    {
        var userId = _userManager.GetUserId(User);

        return await _db.Bookings
            .Include(b => b.Property)
            .Include(b => b.GuestCheckIn).ThenInclude(c => c!.Documents)
            .FirstOrDefaultAsync(b => b.Id == bookingId && b.GuestId == userId);
    }

    private IActionResult? GuardApproved(Booking booking)
    {
        if (booking.Status == BookingStatus.Approved) return null;

        TempData["Error"] = "Check-in opens once the host approves your booking.";
        return RedirectToAction("Details", "Bookings", new { id = booking.Id });
    }

    // Once submitted or verified, the guest can no longer edit the earlier steps
    private static bool IsLocked(Booking booking) =>
        booking.GuestCheckIn?.Status is CheckInStatus.DocumentsSubmitted or CheckInStatus.Verified;

    private async Task<CheckInProcess> GetOrCreateProcessAsync(int propertyId)
    {
        var process = await _db.CheckInProcesses.FirstOrDefaultAsync(c => c.PropertyId == propertyId);
        if (process != null) return process;

        process = new CheckInProcess
        {
            PropertyId = propertyId,
            Title = "Guest check-in",
            StepsJson = CheckInStepsSerializer.SerializeDefault()
        };
        _db.CheckInProcesses.Add(process);
        await _db.SaveChangesAsync();
        return process;
    }

    private static List<GuestDocumentRowVM> DocumentRows(GuestCheckIn checkIn) =>
        checkIn.Documents.OrderBy(d => d.Id)
            .Select(d => new GuestDocumentRowVM { Id = d.Id, DocumentType = d.DocumentType, Status = d.Status })
            .ToList();

    private static CheckInUploadVM BuildUploadVM(Booking booking) => new()
    {
        BookingId = booking.Id,
        PropertyTitle = booking.Property.Title,
        Documents = DocumentRows(booking.GuestCheckIn!)
    };
}