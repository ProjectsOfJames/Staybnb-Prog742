using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;

namespace Staybnb.Services;

public interface IBookingAvailability
{
    /// <summary>True when no approved or checked-in booking overlaps the requested dates.</summary>
    Task<bool> IsAvailableAsync(int propertyId, DateTime checkIn, DateTime checkOut, int? excludeBookingId = null);
}

/// <summary>
/// The one place that decides whether dates are free. Used when a guest books AND when a host approves,
/// so the two can never disagree. Two stays overlap when each starts before the other ends;
/// same-day turnover (check-out day == next check-in day) is allowed.
/// </summary>
public class BookingAvailabilityService : IBookingAvailability
{
    private readonly ApplicationDbContext _db;

    public BookingAvailabilityService(ApplicationDbContext db) => _db = db;

    public async Task<bool> IsAvailableAsync(int propertyId, DateTime checkIn, DateTime checkOut, int? excludeBookingId = null)
    {
        var start = checkIn.Date;
        var end = checkOut.Date;
        var exclude = excludeBookingId ?? 0;   // ids start at 1, so 0 excludes nothing

        var taken = await _db.Bookings.AnyAsync(b =>
            b.PropertyId == propertyId &&
            b.Id != exclude &&
            (b.Status == BookingStatus.Approved || b.Status == BookingStatus.CheckedIn) &&
            b.CheckInDate < end && b.CheckOutDate > start);

        return !taken;
    }
}