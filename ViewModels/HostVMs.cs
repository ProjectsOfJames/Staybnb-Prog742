using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Staybnb.Models;

namespace Staybnb.ViewModels;

/// <summary>The "Become a Host" form. A property AND at least one image are required.</summary>
public class HostApplicationVM
{
    public static readonly string[] PropertyTypes =
        { "Apartment", "House", "Villa", "Cabin", "Cottage", "Beachfront" };

    [Required, StringLength(100)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(2000)]
    public string Description { get; set; } = string.Empty;

    [Range(1, 1000000)]
    [Display(Name = "Price per night (R)")]
    public decimal PricePerNight { get; set; }

    [Range(0, 100000)]
    [Display(Name = "Cleaning fee (R)")]
    public decimal CleaningFee { get; set; }

    [Required]
    public string Address { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string City { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Property type")]
    public string PropertyType { get; set; } = "Apartment";

    [Range(1, 50), Display(Name = "Max guests")] public int MaxGuests { get; set; } = 2;
    [Range(0, 50)] public int Bedrooms { get; set; } = 1;
    [Range(1, 50)] public int Beds { get; set; } = 1;
    [Range(1, 50)] public int Bathrooms { get; set; } = 1;

    public List<int> SelectedAmenityIds { get; set; } = new();
    public List<AmenityVM> AvailableAmenities { get; set; } = new();

    [Display(Name = "Photos")]
    public List<IFormFile> Images { get; set; } = new();
}

/// <summary>One row on the applicant's "Host Application Status" page.</summary>
public class HostApplicationStatusRowVM
{
    public int Id { get; set; }
    public string PropertyTitle { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public DateTime AppliedAt { get; set; }
    public ApplicationStatus Status { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? RejectionReason { get; set; }
}

public class HostDashboardVM
{
    public string HostName { get; set; } = string.Empty;
    public int TotalProperties { get; set; }
    public int ActiveProperties { get; set; }
    public int TotalBookings { get; set; }
    public int BookingsThisMonth { get; set; }
    public decimal TotalEarnings { get; set; }
    public double AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public int PendingRequests { get; set; }
}

/// <summary>One property row on the host's "My Properties" page.</summary>
public class HostPropertyRowVM
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string PropertyType { get; set; } = string.Empty;
    public decimal PricePerNight { get; set; }
    public string? ImageUrl { get; set; }
    public int ImageCount { get; set; }
    public int MaxGuests { get; set; }
    public int Bedrooms { get; set; }
    public int Beds { get; set; }
    public int Bathrooms { get; set; }
    public bool IsActive { get; set; }
    public int BookingCount { get; set; }
    public int ReviewCount { get; set; }

    // A listing can only go live once an admin has approved its application
    public ApplicationStatus ApplicationStatus { get; set; } = ApplicationStatus.Approved;
    public bool CanToggle => ApplicationStatus == ApplicationStatus.Approved;
}

public class HostPropertyEditVM
{
    public int Id { get; set; }

    [Required, StringLength(100)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(2000)]
    public string Description { get; set; } = string.Empty;

    [Range(1, 1000000), Display(Name = "Price per night (R)")]
    public decimal PricePerNight { get; set; }

    [Range(0, 100000), Display(Name = "Cleaning fee (R)")]
    public decimal CleaningFee { get; set; }

    [Required]
    public string Address { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string City { get; set; } = string.Empty;

    [Required, Display(Name = "Property type")]
    public string PropertyType { get; set; } = "Apartment";

    [Range(1, 50), Display(Name = "Max guests")] public int MaxGuests { get; set; } = 2;
    [Range(0, 50)] public int Bedrooms { get; set; }
    [Range(1, 50)] public int Beds { get; set; } = 1;
    [Range(1, 50)] public int Bathrooms { get; set; } = 1;

    public List<int> SelectedAmenityIds { get; set; } = new();
    public List<AmenityVM> AvailableAmenities { get; set; } = new();
}

/// <summary>One booking request row on the host's "Booking Requests" page.</summary>
public class HostBookingRowVM
{
    public int Id { get; set; }
    public string GuestId { get; set; } = string.Empty;
    public string PropertyTitle { get; set; } = string.Empty;
    public string GuestName { get; set; } = string.Empty;
    public string GuestEmail { get; set; } = string.Empty;
    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
    public int NumberOfGuests { get; set; }
    public decimal TotalPrice { get; set; }
    public decimal HostEarning { get; set; }
    public BookingStatus Status { get; set; }
    public CheckInStatus? GuestCheckInStatus { get; set; }
}

/// <summary>The host's view of one guest's check-in: documents to verify.</summary>
public class HostCheckInVM
{
    public int BookingId { get; set; }
    public string PropertyTitle { get; set; } = string.Empty;
    public string GuestName { get; set; } = string.Empty;
    public string GuestEmail { get; set; } = string.Empty;
    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
    public int NumberOfGuests { get; set; }
    public BookingStatus Status { get; set; }
    public CheckInStatus? GuestCheckInStatus { get; set; }
    public List<GuestDocumentRowVM> Documents { get; set; } = new();

    public bool CanReview => GuestCheckInStatus == CheckInStatus.DocumentsSubmitted;
    public bool CanMarkCheckedIn => Status == BookingStatus.Approved && GuestCheckInStatus == CheckInStatus.Verified;
}

/// <summary>Host-defined check-in rules for one property.</summary>
public class CheckInConfigVM
{
    public int PropertyId { get; set; }
    public string PropertyTitle { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string Title { get; set; } = "Guest check-in";

    public List<CheckInStepDto> Steps { get; set; } = new();
}