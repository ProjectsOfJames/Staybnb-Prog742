using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Staybnb.Data;
using Staybnb.Models;
using Staybnb.Services;
using Staybnb.ViewModels;

namespace Staybnb.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IActivityLogger _logger;

    public AccountController(UserManager<ApplicationUser> userManager,
                             SignInManager<ApplicationUser> signInManager,
                             IActivityLogger logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _logger = logger;
    }

    // REGISTER
    [HttpGet, AllowAnonymous]
    public IActionResult Register() => View(new RegisterViewModel());

    [HttpPost, ValidateAntiForgeryToken, AllowAnonymous]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            FirstName = model.FirstName.Trim(),
            LastName = model.LastName.Trim()
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError(string.Empty, e.Description);
            return View(model);
        }

        // Requirement: ALL new registrants are automatically Guests
        await _userManager.AddToRoleAsync(user, RoleSeeder.Guest);

        await _logger.LogAsync(user.Id, "UserRegistered", ActivityType.Registration);

        await _signInManager.SignInAsync(user, isPersistent: false);
        return await RedirectByRoleAsync(user);
    }

    // LOGIN
    [HttpGet, AllowAnonymous]
    public IActionResult Login(string? returnUrl = null) =>
        View(new LoginViewModel { ReturnUrl = returnUrl });

    [HttpPost, ValidateAntiForgeryToken, AllowAnonymous]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var result = await _signInManager.PasswordSignInAsync(
            model.Email, model.Password, model.RememberMe, lockoutOnFailure: true);

        // Only accounts that exist can be logged against (the log is tied to a user)
        var user = await _userManager.FindByEmailAsync(model.Email);

        if (result.Succeeded)
        {
            if (user != null)
                await _logger.LogAsync(user.Id, "UserLoggedIn", ActivityType.Login);

            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                return Redirect(model.ReturnUrl);

            return await RedirectByRoleAsync(user!);
        }

        // Failed attempts and lockouts are security events worth auditing
        if (user != null)
            await _logger.LogAsync(user.Id, result.IsLockedOut ? "AccountLockedOut" : "LoginFailed", ActivityType.Login);

        ModelState.AddModelError(string.Empty,
            result.IsLockedOut ? "Account locked. Try again in a few minutes." : "Invalid email or password.");
        return View(model);
    }

    // LOGOUT
    [HttpPost, ValidateAntiForgeryToken, Authorize]
    public async Task<IActionResult> Logout()
    {
        var userId = _userManager.GetUserId(User);
        if (userId != null)
            await _logger.LogAsync(userId, "UserLoggedOut", ActivityType.Logout);

        await _signInManager.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    public IActionResult AccessDenied() => View();

    // Role-based landing page
    private async Task<IActionResult> RedirectByRoleAsync(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);

        if (roles.Contains(RoleSeeder.SuperAdmin) || roles.Contains(RoleSeeder.Admin))
            return Redirect("/Admin/Dashboard");

        if (roles.Contains(RoleSeeder.Host))
            return Redirect("/Host/Dashboard");

        return Redirect("/Properties");
    }
}