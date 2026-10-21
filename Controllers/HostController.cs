using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Staybnb.Data;

namespace Staybnb.Controllers;

[Authorize(Roles = "Host")]
public class HostController : Controller
{
    public IActionResult Dashboard() => View(DummyData.HostDashboard());

    public IActionResult Properties() => View(DummyData.HostProperties());

    public IActionResult Bookings() => View(DummyData.BookingRows());
}