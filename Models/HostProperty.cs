using System.ComponentModel.DataAnnotations;

namespace Staybnb.Models;

public class HostProperty
{
    public int Id { get; set; }

    [Required, StringLength(100)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    public decimal PricePerNight { get; set; }

    // Does not show in the ERD, but required by TotalPrice = (PricePerNight x Nights) + CleaningFee + ServiceFee
    // ServiceFee is calculated as a percentage at booking time
    public decimal CleaningFee { get; set; }

    [Required]
    public string Address { get; set; } = string.Empty;

    // Needed for "filter by city or property type"
    [Required, StringLength(100)]
    public string City { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string PropertyType { get; set; } = "Apartment";

    public int MaxGuests { get; set; } = 1;
    public int Bedrooms { get; set; } = 1;
    public int Beds { get; set; } = 1;
    public int Bathrooms { get; set; } = 1;

    // False until the HostApplication is approved; hosts can toggle afterwards
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string HostId { get; set; } = string.Empty;
    public ApplicationUser Host { get; set; } = null!;

    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
    public ICollection<PropertyImage> Images { get; set; } = new List<PropertyImage>();
    public ICollection<Amenity> Amenities { get; set; } = new List<Amenity>();   // many-to-many
    public CheckInProcess? CheckInProcess { get; set; }                          // one-to-one
}