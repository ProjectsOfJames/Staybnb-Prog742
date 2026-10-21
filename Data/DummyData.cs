using Staybnb.Models;
using Staybnb.ViewModels;

namespace Staybnb.Data;

/// <summary>
/// TEMPORARY sample data for the 
/// </summary>
public static class DummyData
{
    private static string Img(string seed) => $"https://picsum.photos/seed/{seed}/800/600";

    public static List<AmenityVM> Amenities() => new()
    {
        new() { Id = 1, Name = "Wifi",             IconClass = "bi-wifi" },
        new() { Id = 2, Name = "Kitchen",          IconClass = "bi-cup-hot" },
        new() { Id = 3, Name = "Washer",           IconClass = "bi-droplet" },
        new() { Id = 4, Name = "Dryer",            IconClass = "bi-wind" },
        new() { Id = 5, Name = "Air Conditioning", IconClass = "bi-snow" },
        new() { Id = 6, Name = "Heating",          IconClass = "bi-thermometer-sun" },
        new() { Id = 7, Name = "Pool",             IconClass = "bi-water" },
        new() { Id = 8, Name = "Free Parking",     IconClass = "bi-p-circle" },
    };

    private static PropertyCardVM Card(int id, string title, string city, string type, decimal price, int images) => new()
    {
        Id = id,
        Title = title,
        City = city,
        PropertyType = type,
        PricePerNight = price,
        ImageUrls = Enumerable.Range(1, images).Select(i => Img($"staybnb{id}-{i}")).ToList(),
        AverageRating = 4.5 + (id % 5) / 10.0,
        ReviewCount = id * 3
    };

    public static List<PropertyCardVM> Cards() => new()
    {
        Card(1, "Luxury Apartment",        "Durban",       "Apartment", 1320, 5),
        Card(2, "Exceptional Villa",       "Drakensberg",  "Villa",     1320, 6),
        Card(3, "Cozy 3 Bedroom House",    "Nelspruit",    "House",      825, 4),
        Card(4, "Cozy Holiday Cabin",      "Mpumalanga",   "Cabin",     1230, 5),
        Card(5, "Exceptional Holiday Villa","Nelspruit",   "Villa",     1120, 6),
        Card(6, "Beautiful Beachfront",    "Umhlanga",     "Beachfront",2100, 5),
        Card(7, "Lovely City Apartment",   "Umhlanga",     "Apartment", 1780, 6),
        Card(8, "Cozy Forest Cabin",       "Durban",       "Cabin",     3100, 5),
    };

    public static PropertyDetailsVM Details(int id)
    {
        var card = Cards().FirstOrDefault(c => c.Id == id) ?? Cards()[0];
        return new PropertyDetailsVM
        {
            Id = card.Id,
            Title = card.Title,
            City = card.City,
            Address = $"12 Sample Street, {card.City}",
            Description = "Escape to this peaceful retreat, tucked away among towering trees and birdsong. " +
                          "A welcoming interior with rustic charm, soft lighting and all the essentials for a relaxing stay. " +
                          "Enjoy slow mornings on the deck, fresh air, and evenings wrapped in calm.",
            HostName = "Elsie Masuku",
            PricePerNight = card.PricePerNight,
            CleaningFee = 350,
            MaxGuests = 6,
            Bedrooms = 3,
            Beds = 4,
            Bathrooms = 2,
            AverageRating = card.AverageRating,
            ReviewCount = card.ReviewCount,
            ImageUrls = Enumerable.Range(1, 5).Select(i => Img($"staybnb{card.Id}-{i}")).ToList(),
            Amenities = Amenities().Take(6).ToList(),
            Booking = new BookingVM { PropertyId = card.Id }
        };
    }

    public static HostDashboardVM HostDashboard() => new()
    {
        HostName = "Elsie Masuku",
        TotalProperties = 2,
        ActiveProperties = 2,
        TotalBookings = 3,
        BookingsThisMonth = 3,
        TotalEarnings = 8938,
        AverageRating = 0,
        ReviewCount = 0,
        PendingRequests = 1
    };

    public static List<HostPropertyRowVM> HostProperties() => new()
    {
        new() { Id = 7, Title = "Lovely City Apartment", City = "Umhlanga", PropertyType = "Apartment", PricePerNight = 1780,
                ImageUrl = Img("staybnb7-1"), ImageCount = 6, MaxGuests = 5, Bedrooms = 4, Beds = 4, Bathrooms = 2,
                IsActive = true, BookingCount = 1, ReviewCount = 0 },
        new() { Id = 8, Title = "Cozy Forest Cabin", City = "Durban", PropertyType = "Cabin", PricePerNight = 3100,
                ImageUrl = Img("staybnb8-1"), ImageCount = 5, MaxGuests = 8, Bedrooms = 4, Beds = 5, Bathrooms = 4,
                IsActive = true, BookingCount = 2, ReviewCount = 0 },
        new() { Id = 9, Title = "Seaside Studio", City = "Cape Town", PropertyType = "Apartment", PricePerNight = 950,
                ImageUrl = Img("staybnb9-1"), ImageCount = 3, MaxGuests = 2, Bedrooms = 1, Beds = 1, Bathrooms = 1,
                IsActive = false, BookingCount = 0, ReviewCount = 0 },
    };

    public static List<BookingRowVM> BookingRows() => new()
    {
        new() { Id = 1, PropertyTitle = "Cozy Forest Cabin", GuestName = "Banele Mthembu", GuestEmail = "banele@gmail.com",
                CheckInDate = DateTime.Today.AddDays(3), CheckOutDate = DateTime.Today.AddDays(5), NumberOfGuests = 2,
                TotalPrice = 6572, Status = BookingStatus.Pending },
        new() { Id = 2, PropertyTitle = "Lovely City Apartment", GuestName = "Sboniso Mango", GuestEmail = "sbomango@gmail.com",
                CheckInDate = DateTime.Today.AddDays(10), CheckOutDate = DateTime.Today.AddDays(11), NumberOfGuests = 3,
                TotalPrice = 2491, Status = BookingStatus.Approved },
        new() { Id = 3, PropertyTitle = "Cozy Forest Cabin", GuestName = "Nthabiseng Mangi", GuestEmail = "nthabi@gmail.com",
                CheckInDate = DateTime.Today.AddDays(-4), CheckOutDate = DateTime.Today.AddDays(-2), NumberOfGuests = 4,
                TotalPrice = 6572, Status = BookingStatus.CheckedIn },
        new() { Id = 4, PropertyTitle = "Lovely City Apartment", GuestName = "Trevor Mlotshwa", GuestEmail = "trevor@gmail.com",
                CheckInDate = DateTime.Today.AddDays(-20), CheckOutDate = DateTime.Today.AddDays(-18), NumberOfGuests = 2,
                TotalPrice = 4120, Status = BookingStatus.Rejected },
    };

    public static AdminDashboardVM AdminDashboard() => new()
    {
        TotalUsers = 17,
        PendingHostApps = 1,
        ActiveProperties = 10,
        PlatformRevenue = 52058,
        TotalBookings = 13,
        PendingBookings = 2,
        ApprovedHosts = 10,
        RecentActivity = new()
        {
            new() { UserName = "Elsie Masuku",  Email = "elsie@gmail.com",  Action = "AdditionalPropertyApplicationSubmitted", Date = DateTime.Today },
            new() { UserName = "Muzi Mango",    Email = "muzimango@gmail.com", Action = "HostApplicationSubmitted", Date = DateTime.Today.AddDays(-40) },
            new() { UserName = "Trevor Mlotshwa", Email = "trevor@gmail.com", Action = "HostApplicationSubmitted", Date = DateTime.Today.AddDays(-40) },
        },
        RecentApplications = new()
        {
            new() { Id = 1, Applicant = "Elsie Masuku", Email = "elsie@gmail.com", PropertyTitle = "Lovely City Apartment", Date = DateTime.Today, Status = ApplicationStatus.Pending },
            new() { Id = 2, Applicant = "Neroshen Govendor", Email = "neroshen@gmail.com", PropertyTitle = "Cozy Cabin", Date = DateTime.Today.AddDays(-40), Status = ApplicationStatus.Approved },
            new() { Id = 3, Applicant = "Muzi Mango", Email = "muzimango@gmail.com", PropertyTitle = "Beach House", Date = DateTime.Today.AddDays(-41), Status = ApplicationStatus.Rejected },
        },
        RecentBookings = BookingRows().Take(3).ToList()
    };

    public static List<UserRowVM> Users() => new()
    {
        new() { Id = "u1", FullName = "Super Admin",       Email = "superadmin@staybnb.com", Role = "SuperAdmin", CreatedAt = DateTime.Today.AddDays(-60) },
        new() { Id = "u2", FullName = "Lunga Aphane",      Email = "lunga@gmail.com",        Role = "Admin",      CreatedAt = DateTime.Today.AddDays(-55) },
        new() { Id = "u3", FullName = "Elsie Masuku",      Email = "elsie@gmail.com",        Role = "Host",       CreatedAt = DateTime.Today.AddDays(-50) },
        new() { Id = "u4", FullName = "Banele Mthembu",    Email = "banele@gmail.com",       Role = "Guest",      CreatedAt = DateTime.Today.AddDays(-30) },
        new() { Id = "u5", FullName = "Sboniso Mango",     Email = "sbomango@gmail.com",     Role = "Guest",      CreatedAt = DateTime.Today.AddDays(-12) },
    };
}