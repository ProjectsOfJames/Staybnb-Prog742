using System.ComponentModel.DataAnnotations;

namespace Staybnb.Models;

public class Message
{
    public int Id { get; set; }

    public string SenderId { get; set; } = string.Empty;
    public ApplicationUser Sender { get; set; } = null!;

    public string ReceiverId { get; set; } = string.Empty;
    public ApplicationUser Receiver { get; set; } = null!;

    [Required]
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    // for the inbox unread state
    public bool IsRead { get; set; }
}