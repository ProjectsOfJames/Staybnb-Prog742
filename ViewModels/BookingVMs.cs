using System.ComponentModel.DataAnnotations;
using Staybnb.Models;

namespace Staybnb.ViewModels;

/// <summary>What the guest submits from the booking widget. Prices are NEVER taken from the client.</summary>
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

/// <summary>One row in a booking list (host booking requests, admin recent bookings).</summary>
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

/// <summary>A guest's own booking, used on both "My Bookings" and the booking details page.</summary>
public class GuestBookingVM
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public string PropertyTitle { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string HostName { get; set; } = string.Empty;
    public string HostId { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }

    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
    public int NumberOfGuests { get; set; }

    public decimal CleaningFee { get; set; }
    public decimal ServiceFee { get; set; }
    public decimal TotalPrice { get; set; }

    public BookingStatus Status { get; set; }
    public CheckInStatus? CheckInStatus { get; set; }
    public DateTime CreatedAt { get; set; }

    public int Nights => (CheckOutDate.Date - CheckInDate.Date).Days;
    public decimal StaySubtotal => TotalPrice - CleaningFee - ServiceFee;

    public bool CanCancel =>
        (Status == BookingStatus.Pending || Status == BookingStatus.Approved) && CheckInDate.Date > DateTime.Today;

    public bool CanCheckIn => Status == BookingStatus.Approved;

    public PaymentStatus? PaymentState { get; set; }

    // Payment is due once the host approves; it stays open until the guest pays
    public bool CanPay =>
        (Status == BookingStatus.Approved || Status == BookingStatus.CheckedIn) && PaymentState == PaymentStatus.Pending;

    // Review state, set by the controller
    public bool CanReview { get; set; }
    public bool HasReviewed { get; set; }
    public int? MyRating { get; set; }
    public string? MyComment { get; set; }
}