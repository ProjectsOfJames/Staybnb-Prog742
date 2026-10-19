namespace Staybnb.Models;

public class HostApplication
{
    public int Id { get; set; }

    public string ApplicationUserId { get; set; } = string.Empty;
    public ApplicationUser ApplicationUser { get; set; } = null!;

    public int PropertyId { get; set; }
    public HostProperty Property { get; set; } = null!;

    public ApplicationStatus Status { get; set; } = ApplicationStatus.Pending;
    public DateTime AppliedAt { get; set; } = DateTime.UtcNow;

    // Review details (for the Admin UI and audit trail)
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewedById { get; set; }
    public string? RejectionReason { get; set; }
}