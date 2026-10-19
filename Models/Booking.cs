using System.ComponentModel.DataAnnotations.Schema;

namespace Staybnb.Models;

public class Booking
{
    public int Id { get; set; }

    public int PropertyId { get; set; }
    public HostProperty Property { get; set; } = null!;

    public string GuestId { get; set; } = string.Empty;
    public ApplicationUser Guest { get; set; } = null!;

    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
    public int NumberOfGuests { get; set; } = 1;

    // Price snapshot: stored so later price changes never alter old bookings
    public decimal CleaningFee { get; set; }
    public decimal ServiceFee { get; set; }
    public decimal TotalPrice { get; set; }

    public BookingStatus Status { get; set; } = BookingStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [NotMapped]
    public int Nights => (CheckOutDate.Date - CheckInDate.Date).Days;

    public Payment? Payment { get; set; }            // one-to-one
    public GuestCheckIn? GuestCheckIn { get; set; }  // one-to-one
}