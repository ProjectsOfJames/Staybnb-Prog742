using Microsoft.AspNetCore.Identity;
using Staybnb.Models;

namespace Staybnb.Data;

public static class RoleSeeder
{
    public const string Guest = "Guest";
    public const string Host = "Host";
    public const string Admin = "Admin";
    public const string SuperAdmin = "SuperAdmin";

    public static readonly string[] AllRoles = { Guest, Host, Admin, SuperAdmin };

    public static async Task SeedAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var config = services.GetRequiredService<IConfiguration>();

        // 1. Create the four roles if missing
        foreach (var role in AllRoles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        // 2. Create the SuperAdmin user
        var email = config["Seed:SuperAdmin:Email"] ?? "superadmin@staybnb.com";
        var password = config["Seed:SuperAdmin:Password"] ?? "SuperAdmin#2026!";

        var superAdmin = await userManager.FindByEmailAsync(email);
        if (superAdmin == null)
        {
            superAdmin = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FirstName = "Super",
                LastName = "Admin"
            };

            var result = await userManager.CreateAsync(superAdmin, password);
            if (!result.Succeeded)
                throw new InvalidOperationException(
                    "SuperAdmin seeding failed: " + string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        // 3. SuperAdmin also holds Admin so that [Authorize(Roles = "Admin")] dashboard pages work for them.
        //    SuperAdmin-only pages use [Authorize(Roles = "SuperAdmin")].
        foreach (var role in new[] { SuperAdmin, Admin })
        {
            if (!await userManager.IsInRoleAsync(superAdmin, role))
                await userManager.AddToRoleAsync(superAdmin, role);
        }
    }
}