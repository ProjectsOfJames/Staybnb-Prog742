using System.ComponentModel.DataAnnotations;
using Staybnb.Models;

namespace Staybnb.ViewModels;

public class ActivityRowVM
{
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public ActivityType Type { get; set; }
    public DateTime Date { get; set; }
}

public class ApplicationRowVM
{
    public int Id { get; set; }
    public string Applicant { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PropertyTitle { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public decimal PricePerNight { get; set; }
    public DateTime Date { get; set; }
    public ApplicationStatus Status { get; set; }
}

public class AdminDashboardVM
{
    public int TotalUsers { get; set; }
    public int PendingHostApps { get; set; }
    public int ActiveProperties { get; set; }
    public decimal PlatformRevenue { get; set; }   // service fees on confirmed bookings
    public decimal BookingVolume { get; set; }     // total value of confirmed bookings
    public int TotalBookings { get; set; }
    public int PendingBookings { get; set; }
    public int ApprovedHosts { get; set; }

    public List<ActivityRowVM> RecentActivity { get; set; } = new();
    public List<ApplicationRowVM> RecentApplications { get; set; } = new();
    public List<BookingRowVM> RecentBookings { get; set; } = new();
}

public class HostRequestsVM
{
    public string Status { get; set; } = "Pending";
    public List<ApplicationRowVM> Rows { get; set; } = new();
}

public class ApplicationDetailsVM
{
    public int Id { get; set; }
    public ApplicationStatus Status { get; set; }
    public DateTime AppliedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewedByName { get; set; }
    public string? RejectionReason { get; set; }

    public string ApplicantName { get; set; } = string.Empty;
    public string ApplicantEmail { get; set; } = string.Empty;
    public DateTime ApplicantJoined { get; set; }
    public string ApplicantRoles { get; set; } = string.Empty;
    public bool IsExistingHost { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string PropertyType { get; set; } = string.Empty;
    public decimal PricePerNight { get; set; }
    public decimal CleaningFee { get; set; }
    public int MaxGuests { get; set; }
    public int Bedrooms { get; set; }
    public int Beds { get; set; }
    public int Bathrooms { get; set; }

    public List<string> ImageUrls { get; set; } = new();
    public List<AmenityVM> Amenities { get; set; } = new();
}

public class RejectApplicationVM
{
    [Required, StringLength(500, MinimumLength = 5)]
    public string Reason { get; set; } = string.Empty;
}

public class UserRowVM
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    // Only plain Guests can be promoted
    public bool CanPromote => Role == "Guest";
}

public class UserListVM
{
    public List<UserRowVM> Users { get; set; } = new();
    public string? Search { get; set; }
    public string? Role { get; set; }
    public int Page { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public int TotalCount { get; set; }
}

public class AdminPropertyRowVM
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string HostName { get; set; } = string.Empty;
    public decimal PricePerNight { get; set; }
    public bool IsActive { get; set; }
    public ApplicationStatus ApplicationStatus { get; set; }
    public int BookingCount { get; set; }
    public string? ImageUrl { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ActivityLogPageVM
{
    public List<ActivityRowVM> Rows { get; set; } = new();
    public string? Search { get; set; }
    public string? Type { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int Page { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public int TotalCount { get; set; }
}