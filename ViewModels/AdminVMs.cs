using Staybnb.Models;

namespace Staybnb.ViewModels;

public class ActivityRowVM
{
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Status { get; set; } = "Active";
}

public class ApplicationRowVM
{
    public int Id { get; set; }
    public string Applicant { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PropertyTitle { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public ApplicationStatus Status { get; set; }
}

public class UserRowVM
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class AdminDashboardVM
{
    public int TotalUsers { get; set; }
    public int PendingHostApps { get; set; }
    public int ActiveProperties { get; set; }
    public decimal PlatformRevenue { get; set; }
    public int TotalBookings { get; set; }
    public int PendingBookings { get; set; }
    public int ApprovedHosts { get; set; }

    public List<ActivityRowVM> RecentActivity { get; set; } = new();
    public List<ApplicationRowVM> RecentApplications { get; set; } = new();
    public List<BookingRowVM> RecentBookings { get; set; } = new();
}