using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Staybnb.Data;
using Staybnb.ViewModels;

namespace Staybnb.Controllers;

[Authorize(Roles = "Guest")]
public class HostApplicationController : Controller
{
    [HttpGet]
    public IActionResult Create()
    {
        return View(new HostApplicationVM { AvailableAmenities = DummyData.Amenities() });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Create(HostApplicationVM model)
    {
        // applications requires to be submitted with an image
        if (model.Images == null || model.Images.Count == 0 || model.Images.All(f => f.Length == 0))
            ModelState.AddModelError(nameof(model.Images), "Please upload at least one property image.");

        if (!ModelState.IsValid)
        {
            model.AvailableAmenities = DummyData.Amenities();
            return View(model);
        }

        TempData["Info"] = "Shell only: validation passed. Saving the application is built in Segment 5.";
        return RedirectToAction(nameof(Create));
    }
}