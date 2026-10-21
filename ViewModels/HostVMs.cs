using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

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
}