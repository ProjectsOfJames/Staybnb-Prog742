using System.ComponentModel.DataAnnotations;
using Staybnb.Models;

namespace Staybnb.ViewModels;

/// <summary>What the guest submits from the booking widget. Prices are never taken from the client.</summary>
public class BookingVM
{
    public int PropertyId { get; set; }

    [Required, DataType(DataType.Date)]
    [Display(Name = "Check-in")]
    public DateTime CheckInDate { get; set; } = DateTime.Today.AddDays(7);

    [Required, DataType(DataType.Date)]
    [Display(Name = "Checkout")]
    public DateTime CheckOutDate { get; set; } = DateTime.Today.AddDays(8);

    [Range(1, 50)]
    [Display(Name = "Guests")]
    public int NumberOfGuests { get; set; } = 1;
}

/// <summary>One row in a booking list (host booking requests, admin recent bookings, guest bookings).</summary>
public class BookingRowVM
{
    public int Id { get; set; }
    public string PropertyTitle { get; set; } = string.Empty;
    public string GuestName { get; set; } = string.Empty;
    public string GuestEmail { get; set; } = string.Empty;
    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
    public int NumberOfGuests { get; set; }
    public decimal TotalPrice { get; set; }
    public BookingStatus Status { get; set; }
}