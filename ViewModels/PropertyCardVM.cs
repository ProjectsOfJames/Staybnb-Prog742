namespace Staybnb.ViewModels;

/// <summary>Everything the shared _PropertyCard partial needs </summary>
public class PropertyCardVM
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string PropertyType { get; set; } = string.Empty;
    public decimal PricePerNight { get; set; }
    public List<string> ImageUrls { get; set; } = new();
    public double AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public bool IsWishlisted { get; set; }
}