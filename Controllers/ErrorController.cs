using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Staybnb.Controllers;

/// <summary>Friendly pages for 404s and unexpected errors </summary>
[AllowAnonymous]
public class ErrorController : Controller
{
    [Route("Error/{code?}")]
    public IActionResult Index(int? code)
    {
        var status = code is >= 400 and <= 599 ? code.Value : 500;
        Response.StatusCode = status;

        ViewData["Code"] = status;
        ViewData["Heading"] = status switch
        {
            404 => "Page not found",
            403 => "Access denied",
            401 => "Please sign in",
            _ => "Something went wrong"
        };
        ViewData["Message"] = status switch
        {
            404 => "We couldn't find the page you were looking for. It may have moved or never existed.",
            403 => "You don't have permission to view this page.",
            401 => "You need to sign in to view this page.",
            _ => "An unexpected error occurred. Please try again in a moment."
        };

        return View();
    }
}