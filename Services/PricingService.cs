namespace Staybnb.Services;

public record PriceBreakdown(int Nights, decimal StaySubtotal, decimal CleaningFee, decimal ServiceFee, decimal TotalPrice);

public interface IPricingService
{
    PriceBreakdown Calculate(decimal pricePerNight, decimal cleaningFee, DateTime checkIn, DateTime checkOut);
}

/// <summary>
/// Single source of truth for pricing:
/// TotalPrice = (PricePerNight x Nights) + CleaningFee + ServiceFee
/// Always calculated on the server; the browser's estimate is for display only.
/// </summary>
public class PricingService : IPricingService
{
    public const decimal ServiceFeeRate = 0.12m;

    public PriceBreakdown Calculate(decimal pricePerNight, decimal cleaningFee, DateTime checkIn, DateTime checkOut)
    {
        var nights = (checkOut.Date - checkIn.Date).Days;
        if (nights < 1)
            throw new ArgumentException("Check-out must be after check-in.");

        var stay = pricePerNight * nights;
        var service = Math.Round(stay * ServiceFeeRate, 2, MidpointRounding.AwayFromZero);
        var total = stay + cleaningFee + service;

        return new PriceBreakdown(nights, stay, cleaningFee, service, total);
    }
}