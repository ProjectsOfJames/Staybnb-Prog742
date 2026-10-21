using Staybnb.Models;

namespace Staybnb.ViewModels;

public class AmenityVM
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string IconClass { get; set; } = string.Empty;
}

/// <summary>Guest gallery page: the cards plus the current filter values.</summary>
public class PropertyGalleryVM
{
    public List<PropertyCardVM> Properties { get; set; } = new();
    public string? City { get; set; }
    public string? PropertyType { get; set; }
    public List<string> Cities { get; set; } = new();
    public List<string> PropertyTypes { get; set; } = new();
}

/// <summary>Property details page, including the booking widget.</summary>
public class PropertyDetailsVM
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string HostName { get; set; } = string.Empty;

    public decimal PricePerNight { get; set; }
    public decimal CleaningFee { get; set; }
    public decimal ServiceFeeRate { get; set; } = 0.12m;   // matched screenshot

    public int MaxGuests { get; set; }
    public int Bedrooms { get; set; }
    public int Beds { get; set; }
    public int Bathrooms { get; set; }

    public double AverageRating { get; set; }
    public int ReviewCount { get; set; }

    public List<string> ImageUrls { get; set; } = new();
    public List<AmenityVM> Amenities { get; set; } = new();

    public BookingVM Booking { get; set; } = new();
}