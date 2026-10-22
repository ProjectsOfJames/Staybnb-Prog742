using Microsoft.EntityFrameworkCore;
using Staybnb.Data;

namespace Staybnb.Services;

public static class RoleLabels
{
    /// <summary>For each user id, returns their highest role: SuperAdmin > Admin > Host > Guest.</summary>
    public static async Task<Dictionary<string, string>> HighestRolesAsync(ApplicationDbContext db, IEnumerable<string> userIds)
    {
        var ids = userIds.Distinct().ToList();

        var pairs = await (from ur in db.UserRoles
                           join r in db.Roles on ur.RoleId equals r.Id
                           where ids.Contains(ur.UserId)
                           select new { ur.UserId, r.Name }).ToListAsync();

        string Pick(List<string?> names)
        {
            if (names.Contains(RoleSeeder.SuperAdmin)) return "Super Admin";
            if (names.Contains(RoleSeeder.Admin)) return "Admin";
            if (names.Contains(RoleSeeder.Host)) return "Host";
            if (names.Contains(RoleSeeder.Guest)) return "Guest";
            return "User";
        }

        return ids.ToDictionary(
            id => id,
            id => Pick(pairs.Where(p => p.UserId == id).Select(p => p.Name).ToList()));
    }
}