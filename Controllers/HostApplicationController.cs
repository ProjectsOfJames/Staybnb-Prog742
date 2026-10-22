using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;
using Staybnb.Services;
using Staybnb.ViewModels;

namespace Staybnb.Controllers;

// Guests apply to become hosts; existing hosts use the same form to list an additional property.
[Authorize(Roles = "Guest,Host")]
public class HostApplicationController : Controller
{
    private const int MaxImages = 10;
    private const long MaxImageBytes = 5 * 1024 * 1024;
    private static readonly string[] ImageExtensions = { ".jpg", ".jpeg", ".png", ".webp" };

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IFileStorage _files;
    private readonly INotificationService _notifications;
    private readonly IActivityLogger _logger;

    public HostApplicationController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, IFileStorage files, INotificationService notifications, IActivityLogger logger)
    {
        _db = db;
        _userManager = userManager;
        _files = files;
        _notifications = notifications;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new HostApplicationVM();
        await FillAmenitiesAsync(model);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [RequestSizeLimit(60_000_000), RequestFormLimits(MultipartBodyLengthLimit = 60_000_000)]
    public async Task<IActionResult> Create(HostApplicationVM model)
    {
        var userId = _userManager.GetUserId(User)!;

        if (await _db.HostApplications.AnyAsync(a => a.ApplicationUserId == userId && a.Status == ApplicationStatus.Pending))
            ModelState.AddModelError(string.Empty,
                "You already have an application waiting for review. Please wait for the decision before applying again.");

        // An application is incomplete (and cannot be submitted) without at least one image
        var files = model.Images?.Where(f => f.Length > 0).ToList() ?? new List<IFormFile>();
        if (files.Count == 0)
        {
            ModelState.AddModelError(nameof(model.Images), "Please upload at least one property image.");
        }
        else if (files.Count > MaxImages)
        {
            ModelState.AddModelError(nameof(model.Images), $"You can upload at most {MaxImages} images.");
        }
        else
        {
            foreach (var file in files)
            {
                var error = _files.Validate(file, ImageExtensions, MaxImageBytes);
                if (error != null)
                {
                    ModelState.AddModelError(nameof(model.Images), $"{file.FileName}: {error}");
                    break;
                }
            }
        }

        if (!ModelState.IsValid)
        {
            await FillAmenitiesAsync(model);
            return View(model);
        }

        // Save files first, then the database rows. If the database save fails, remove the files again.
        var savedUrls = new List<string>();
        try
        {
            foreach (var file in files)
                savedUrls.Add(await _files.SavePublicAsync(file, StorageFolders.PropertyImages, ImageExtensions, MaxImageBytes));

            var amenities = await _db.Amenities
                .Where(a => model.SelectedAmenityIds.Contains(a.Id))
                .ToListAsync();

            var property = new HostProperty
            {
                Title = model.Title.Trim(),
                Description = model.Description.Trim(),
                PricePerNight = model.PricePerNight,
                CleaningFee = model.CleaningFee,
                Address = model.Address.Trim(),
                City = model.City.Trim(),
                PropertyType = model.PropertyType,
                MaxGuests = model.MaxGuests,
                Bedrooms = model.Bedrooms,
                Beds = model.Beds,
                Bathrooms = model.Bathrooms,
                IsActive = false,                       // stays hidden until an admin approves it
                HostId = userId,
                Amenities = amenities,
                Images = savedUrls.Select(u => new PropertyImage { ImageUrl = u }).ToList()
            };

            _db.HostApplications.Add(new HostApplication
            {
                ApplicationUserId = userId,
                Property = property,
                Status = ApplicationStatus.Pending
            });

            _logger.Log(userId, $"HostApplicationSubmitted: {property.Title}", ActivityType.ApplicationSubmitted);

            var applicant = await _userManager.GetUserAsync(User);
            var applicantName = applicant?.FullName ?? "A user";

            _notifications.Notify(userId, "Application submitted",
                $"Your application for \"{property.Title}\" was received and is under review.",
                NotificationType.ApplicationUpdate, "/HostApplication/Status");

            await _notifications.NotifyAdminsAsync("New host application",
                $"{applicantName} applied with \"{property.Title}\".",
                NotificationType.ApplicationUpdate, "/Admin/HostRequests");

            await _db.SaveChangesAsync();
        }
        catch
        {
            foreach (var url in savedUrls) _files.DeletePublic(url);
            throw;
        }

        TempData["Success"] = "Application submitted! An admin will review it. You can follow its progress here.";
        return RedirectToAction(nameof(Status));
    }

    [HttpGet]
    public async Task<IActionResult> Status()
    {
        var userId = _userManager.GetUserId(User);

        var rows = await _db.HostApplications.AsNoTracking()
            .Where(a => a.ApplicationUserId == userId)
            .OrderByDescending(a => a.AppliedAt)
            .Select(a => new HostApplicationStatusRowVM
            {
                Id = a.Id,
                PropertyTitle = a.Property.Title,
                City = a.Property.City,
                AppliedAt = a.AppliedAt,
                Status = a.Status,
                ReviewedAt = a.ReviewedAt,
                RejectionReason = a.RejectionReason
            })
            .ToListAsync();

        return View(rows);
    }

    private async Task FillAmenitiesAsync(HostApplicationVM model)
    {
        model.AvailableAmenities = await _db.Amenities.AsNoTracking()
            .OrderBy(a => a.Name)
            .Select(a => new AmenityVM { Id = a.Id, Name = a.Name, IconClass = a.IconClass })
            .ToListAsync();
    }
}