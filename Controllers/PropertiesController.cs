using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Staybnb.Data;
using Staybnb.ViewModels;

namespace Staybnb.Controllers;

[Authorize(Roles = "Guest")]
public class PropertiesController : Controller
{
    public IActionResult Index(string? city, string? propertyType)
    {
        var all = DummyData.Cards();

        var filtered = all.Where(p =>
                (string.IsNullOrEmpty(city) || p.City == city) &&
                (string.IsNullOrEmpty(propertyType) || p.PropertyType == propertyType))
            .ToList();

        return View(new PropertyGalleryVM
        {
            Properties = filtered,
            City = city,
            PropertyType = propertyType,
            Cities = all.Select(p => p.City).Distinct().OrderBy(c => c).ToList(),
            PropertyTypes = all.Select(p => p.PropertyType).Distinct().OrderBy(t => t).ToList()
        });
    }

    public IActionResult Details(int id) => View(DummyData.Details(id));
}