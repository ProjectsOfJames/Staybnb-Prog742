using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Staybnb.Data;

namespace Staybnb.Controllers;

// SuperAdmin is also seeded into the Admin role, so it passes this check too
[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    public IActionResult Dashboard() => View(DummyData.AdminDashboard());

    // Class-level Admin and action-level SuperAdmin must both be satisfied
    [Authorize(Roles = "SuperAdmin")]
    public IActionResult Users() => View(DummyData.Users());
}