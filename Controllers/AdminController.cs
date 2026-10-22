using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;
using Staybnb.Services;
using Staybnb.ViewModels;

namespace Staybnb.Controllers;

// SuperAdmin is also seeded into the Admin role, so it passes this class-level check too.
[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private const int PageSize = 15;

    private static readonly string[] FilterRoles =
        { RoleSeeder.Guest, RoleSeeder.Host, RoleSeeder.Admin, RoleSeeder.SuperAdmin };

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly INotificationService _notifications;
    private readonly IActivityLogger _logger;

    public AdminController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, INotificationService notifications, IActivityLogger logger)
    {
        _db = db;
        _userManager = userManager;
        _notifications = notifications;
        _logger = logger;
    }

    // Dashboard
    public async Task<IActionResult> Dashboard()
    {
        // Revenue only counts bookings that have actually been paid
        var confirmed = _db.Bookings.AsNoTracking()
            .Where(b => b.Payment != null && b.Payment.Status == PaymentStatus.Paid);

        var vm = new AdminDashboardVM
        {
            TotalUsers = await _userManager.Users.CountAsync(),
            PendingHostApps = await _db.HostApplications.CountAsync(a => a.Status == ApplicationStatus.Pending),
            ActiveProperties = await _db.Properties.CountAsync(p => p.IsActive),
            PlatformRevenue = await confirmed.SumAsync(b => (decimal?)b.ServiceFee) ?? 0,
            BookingVolume = await confirmed.SumAsync(b => (decimal?)b.TotalPrice) ?? 0,
            TotalBookings = await _db.Bookings.CountAsync(),
            PendingBookings = await _db.Bookings.CountAsync(b => b.Status == BookingStatus.Pending),
            ApprovedHosts = await (from ur in _db.UserRoles
                                   join r in _db.Roles on ur.RoleId equals r.Id
                                   where r.Name == RoleSeeder.Host
                                   select ur.UserId).CountAsync(),

            RecentActivity = await _db.ActivityLogs.AsNoTracking()
                .OrderByDescending(l => l.CreatedAt)
                .Take(5)
                .Select(l => new ActivityRowVM
                {
                    UserName = l.User.FirstName + " " + l.User.LastName,
                    Email = l.User.Email ?? string.Empty,
                    Action = l.Action,
                    Type = l.ActivityType,
                    Date = l.CreatedAt
                })
                .ToListAsync(),

            RecentApplications = await _db.HostApplications.AsNoTracking()
                .OrderByDescending(a => a.AppliedAt)
                .Take(5)
                .Select(ToApplicationRow)
                .ToListAsync(),

            RecentBookings = await _db.Bookings.AsNoTracking()
                .OrderByDescending(b => b.CreatedAt)
                .Take(5)
                .Select(ToBookingRow)
                .ToListAsync()
        };

        return View(vm);
    }

    // Host application review
    public async Task<IActionResult> HostRequests(string? status = "Pending")
    {
        var query = _db.HostApplications.AsNoTracking().AsQueryable();

        if (Enum.TryParse<ApplicationStatus>(status, true, out var parsed))
        {
            query = query.Where(a => a.Status == parsed);
            status = parsed.ToString();
        }
        else
        {
            status = "All";
        }

        // Oldest pending first (fair queue); otherwise newest first
        var ordered = status == "Pending"
            ? query.OrderBy(a => a.AppliedAt)
            : query.OrderByDescending(a => a.AppliedAt);

        return View(new HostRequestsVM
        {
            Status = status,
            Rows = await ordered.Select(ToApplicationRow).ToListAsync()
        });
    }

    public async Task<IActionResult> HostRequestDetails(int id)
    {
        var a = await _db.HostApplications.AsNoTracking()
            .Include(x => x.ApplicationUser)
            .Include(x => x.Property).ThenInclude(p => p.Images)
            .Include(x => x.Property).ThenInclude(p => p.Amenities)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id);
        if (a == null) return NotFound();

        var roles = await _userManager.GetRolesAsync(a.ApplicationUser);

        string? reviewer = null;
        if (a.ReviewedById != null)
            reviewer = (await _userManager.FindByIdAsync(a.ReviewedById))?.FullName;

        var p = a.Property;
        return View(new ApplicationDetailsVM
        {
            Id = a.Id,
            Status = a.Status,
            AppliedAt = a.AppliedAt,
            ReviewedAt = a.ReviewedAt,
            ReviewedByName = reviewer,
            RejectionReason = a.RejectionReason,
            ApplicantName = a.ApplicationUser.FullName,
            ApplicantEmail = a.ApplicationUser.Email ?? string.Empty,
            ApplicantJoined = a.ApplicationUser.CreatedAt,
            ApplicantRoles = string.Join(", ", roles),
            IsExistingHost = roles.Contains(RoleSeeder.Host),
            Title = p.Title,
            Description = p.Description,
            Address = p.Address,
            City = p.City,
            PropertyType = p.PropertyType,
            PricePerNight = p.PricePerNight,
            CleaningFee = p.CleaningFee,
            MaxGuests = p.MaxGuests,
            Bedrooms = p.Bedrooms,
            Beds = p.Beds,
            Bathrooms = p.Bathrooms,
            ImageUrls = p.Images.Select(i => i.ImageUrl).ToList(),
            Amenities = p.Amenities.OrderBy(x => x.Name)
                .Select(x => new AmenityVM { Id = x.Id, Name = x.Name, IconClass = x.IconClass }).ToList()
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveApplication(int id)
    {
        var application = await _db.HostApplications
            .Include(a => a.Property)
            .Include(a => a.ApplicationUser)
            .FirstOrDefaultAsync(a => a.Id == id);
        if (application == null) return NotFound();

        if (application.Status != ApplicationStatus.Pending)
        {
            TempData["Error"] = "This application has already been reviewed.";
            return RedirectToAction(nameof(HostRequestDetails), new { id });
        }

        var user = application.ApplicationUser;

        // One transaction: the role swap and the application update succeed or fail together
        await using var tx = await _db.Database.BeginTransactionAsync();

        // The Transition: add Host, remove Guest (a user is either a Guest or a Host, never both).
        // An existing host adding another property is already a Host, so only the application changes.
        var becameHost = false;
        if (!await _userManager.IsInRoleAsync(user, RoleSeeder.Host))
        {
            var added = await _userManager.AddToRoleAsync(user, RoleSeeder.Host);
            if (!added.Succeeded) return RoleError(added, id);
            becameHost = true;
        }

        if (await _userManager.IsInRoleAsync(user, RoleSeeder.Guest))
        {
            var removed = await _userManager.RemoveFromRoleAsync(user, RoleSeeder.Guest);
            if (!removed.Succeeded) return RoleError(removed, id);
        }

        application.Status = ApplicationStatus.Approved;
        application.ReviewedAt = DateTime.UtcNow;
        application.ReviewedById = _userManager.GetUserId(User);
        application.RejectionReason = null;
        application.Property.IsActive = true;   // approved listings go live

        var adminId = _userManager.GetUserId(User)!;
        if (becameHost)
            _logger.Log(adminId, $"RoleChanged: {user.Email} Guest -> Host", ActivityType.RoleChange);
        _logger.Log(adminId, $"HostApplicationApproved: {application.Property.Title} ({user.Email})", ActivityType.ApplicationDecision);

        _notifications.Notify(user.Id, "Host Application Approved",
            $"Congratulations! Your application for \"{application.Property.Title}\" was approved and is now live.",
            NotificationType.Success, "/Host/Dashboard");

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        TempData["Success"] = $"Approved. {user.FullName} is now a Host and \"{application.Property.Title}\" is live.";
        return RedirectToAction(nameof(HostRequests));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectApplication(int id, RejectApplicationVM model)
    {
        var application = await _db.HostApplications.FirstOrDefaultAsync(a => a.Id == id);
        if (application == null) return NotFound();

        if (application.Status != ApplicationStatus.Pending)
        {
            TempData["Error"] = "This application has already been reviewed.";
            return RedirectToAction(nameof(HostRequestDetails), new { id });
        }

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Please give a reason of at least 5 characters so the applicant knows why.";
            return RedirectToAction(nameof(HostRequestDetails), new { id });
        }

        application.Status = ApplicationStatus.Rejected;
        application.RejectionReason = model.Reason.Trim();
        application.ReviewedAt = DateTime.UtcNow;
        application.ReviewedById = _userManager.GetUserId(User);

        _logger.Log(_userManager.GetUserId(User)!, $"HostApplicationRejected: application #{application.Id}", ActivityType.ApplicationDecision);

        _notifications.Notify(application.ApplicationUserId, "Host Application Rejected",
            $"Your application was not approved. Reason: {application.RejectionReason}",
            NotificationType.Warning, "/HostApplication/Status");

        await _db.SaveChangesAsync();

        TempData["Success"] = "Application rejected.";
        return RedirectToAction(nameof(HostRequests));
    }

    // User management (SuperAdmin only)
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Users(string? q, string? role, int page = 1)
    {
        var query = _userManager.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(u => (u.Email ?? "").Contains(term) || u.FirstName.Contains(term) || u.LastName.Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(role) && FilterRoles.Contains(role))
        {
            query = query.Where(u => (from ur in _db.UserRoles
                                      join r in _db.Roles on ur.RoleId equals r.Id
                                      where ur.UserId == u.Id && r.Name == role
                                      select ur).Any());
        }

        var total = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        page = Math.Clamp(page, 1, totalPages);

        var users = await query
            .OrderByDescending(u => u.CreatedAt).ThenBy(u => u.Id)
            .Skip((page - 1) * PageSize).Take(PageSize)
            .ToListAsync();

        var ids = users.Select(u => u.Id).ToList();
        var pairs = await (from ur in _db.UserRoles
                           join r in _db.Roles on ur.RoleId equals r.Id
                           where ids.Contains(ur.UserId)
                           select new { ur.UserId, r.Name }).ToListAsync();

        string Highest(string userId)
        {
            var names = pairs.Where(p => p.UserId == userId).Select(p => p.Name).ToList();
            if (names.Contains(RoleSeeder.SuperAdmin)) return RoleSeeder.SuperAdmin;
            if (names.Contains(RoleSeeder.Admin)) return RoleSeeder.Admin;
            if (names.Contains(RoleSeeder.Host)) return RoleSeeder.Host;
            if (names.Contains(RoleSeeder.Guest)) return RoleSeeder.Guest;
            return "None";
        }

        return View(new UserListVM
        {
            Users = users.Select(u => new UserRowVM
            {
                Id = u.Id,
                FullName = u.FullName,
                Email = u.Email ?? string.Empty,
                Role = Highest(u.Id),
                CreatedAt = u.CreatedAt
            }).ToList(),
            Search = q,
            Role = role,
            Page = page,
            TotalPages = totalPages,
            TotalCount = total
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> PromoteToAdmin(string id, string? q, string? role, int page = 1)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        // Only Guests can be promoted: never Hosts, existing Admins or the SuperAdmin
        if (!await _userManager.IsInRoleAsync(user, RoleSeeder.Guest))
        {
            TempData["Error"] = "Only guests can be promoted to Admin.";
            return RedirectToAction(nameof(Users), new { q, role, page });
        }

        await using var tx = await _db.Database.BeginTransactionAsync();

        var added = await _userManager.AddToRoleAsync(user, RoleSeeder.Admin);
        if (!added.Succeeded)
        {
            TempData["Error"] = string.Join(" ", added.Errors.Select(e => e.Description));
            return RedirectToAction(nameof(Users), new { q, role, page });
        }

        var removed = await _userManager.RemoveFromRoleAsync(user, RoleSeeder.Guest);
        if (!removed.Succeeded)
        {
            TempData["Error"] = string.Join(" ", removed.Errors.Select(e => e.Description));
            return RedirectToAction(nameof(Users), new { q, role, page });
        }

        _logger.Log(_userManager.GetUserId(User)!, $"RoleChanged: {user.Email} Guest -> Admin", ActivityType.RoleChange);

        _notifications.Notify(user.Id, "You're now an Admin",
            "A SuperAdmin promoted your account. You now have access to the admin dashboard.",
            NotificationType.Success, "/Admin/Dashboard");
        await _db.SaveChangesAsync();

        await tx.CommitAsync();

        TempData["Success"] = $"{user.FullName} is now an Admin.";
        return RedirectToAction(nameof(Users), new { q, role, page });
    }

    // System audit
    public async Task<IActionResult> ActivityLogs(string? q, string? type, DateTime? from, DateTime? to, int page = 1)
    {
        const int logPageSize = 25;

        var query = _db.ActivityLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(l => l.Action.Contains(term)
                                  || (l.User.Email ?? "").Contains(term)
                                  || l.User.FirstName.Contains(term)
                                  || l.User.LastName.Contains(term));
        }

        if (Enum.TryParse<ActivityType>(type, true, out var parsedType))
        {
            query = query.Where(l => l.ActivityType == parsedType);
            type = parsedType.ToString();
        }
        else
        {
            type = null;
        }

        // Dates arrive as local dates; stored timestamps are UTC
        if (from.HasValue)
        {
            var start = from.Value.Date.ToUniversalTime();
            query = query.Where(l => l.CreatedAt >= start);
        }
        if (to.HasValue)
        {
            var end = to.Value.Date.AddDays(1).ToUniversalTime();
            query = query.Where(l => l.CreatedAt < end);
        }

        var total = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)logPageSize));
        page = Math.Clamp(page, 1, totalPages);

        var rows = await query
            .OrderByDescending(l => l.CreatedAt).ThenByDescending(l => l.Id)
            .Skip((page - 1) * logPageSize).Take(logPageSize)
            .Select(l => new ActivityRowVM
            {
                UserName = l.User.FirstName + " " + l.User.LastName,
                Email = l.User.Email ?? string.Empty,
                Action = l.Action,
                Type = l.ActivityType,
                Date = l.CreatedAt
            })
            .ToListAsync();

        return View(new ActivityLogPageVM
        {
            Rows = rows,
            Search = q,
            Type = type,
            From = from,
            To = to,
            Page = page,
            TotalPages = totalPages,
            TotalCount = total
        });
    }

    // Read-only marketplace overview
    public async Task<IActionResult> Bookings()
    {
        var rows = await _db.Bookings.AsNoTracking()
            .OrderByDescending(b => b.CreatedAt)
            .Take(100)
            .Select(ToBookingRow)
            .ToListAsync();

        return View(rows);
    }

    public async Task<IActionResult> Properties()
    {
        var rows = await _db.Properties.AsNoTracking()
            .OrderByDescending(p => p.CreatedAt)
            .Take(100)
            .Select(p => new AdminPropertyRowVM
            {
                Id = p.Id,
                Title = p.Title,
                City = p.City,
                HostName = p.Host.FirstName + " " + p.Host.LastName,
                PricePerNight = p.PricePerNight,
                IsActive = p.IsActive,
                BookingCount = p.Bookings.Count,
                ImageUrl = p.Images.Select(i => i.ImageUrl).FirstOrDefault(),
                CreatedAt = p.CreatedAt,
                ApplicationStatus = _db.HostApplications
                    .Where(a => a.PropertyId == p.Id)
                    .Select(a => a.Status)
                    .FirstOrDefault()
            })
            .ToListAsync();

        return View(rows);
    }

    // Helpers
    private IActionResult RoleError(IdentityResult result, int applicationId)
    {
        TempData["Error"] = "Could not update the user's roles: " + string.Join(" ", result.Errors.Select(e => e.Description));
        return RedirectToAction(nameof(HostRequestDetails), new { id = applicationId });
    }

    private static readonly System.Linq.Expressions.Expression<Func<HostApplication, ApplicationRowVM>> ToApplicationRow =
        a => new ApplicationRowVM
        {
            Id = a.Id,
            Applicant = a.ApplicationUser.FirstName + " " + a.ApplicationUser.LastName,
            Email = a.ApplicationUser.Email ?? string.Empty,
            PropertyTitle = a.Property.Title,
            City = a.Property.City,
            PricePerNight = a.Property.PricePerNight,
            Date = a.AppliedAt,
            Status = a.Status
        };

    private static readonly System.Linq.Expressions.Expression<Func<Booking, BookingRowVM>> ToBookingRow =
        b => new BookingRowVM
        {
            Id = b.Id,
            PropertyTitle = b.Property.Title,
            GuestName = b.Guest.FirstName + " " + b.Guest.LastName,
            GuestEmail = b.Guest.Email ?? string.Empty,
            CheckInDate = b.CheckInDate,
            CheckOutDate = b.CheckOutDate,
            NumberOfGuests = b.NumberOfGuests,
            TotalPrice = b.TotalPrice,
            Status = b.Status
        };
}