namespace Staybnb.ViewModels;

/// <summary>One line in the inbox: a conversation with one other person.</summary>
public class ConversationRowVM
{
    public string OtherUserId { get; set; } = string.Empty;
    public string OtherName { get; set; } = string.Empty;
    public string OtherRole { get; set; } = string.Empty;
    public string Preview { get; set; } = string.Empty;
    public bool LastIsMine { get; set; }
    public DateTime LastTimestamp { get; set; }
    public int UnreadCount { get; set; }
}

public class MessageVM
{
    public int Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public bool IsMine { get; set; }
    public bool IsRead { get; set; }
}

public class ConversationVM
{
    public string OtherUserId { get; set; } = string.Empty;
    public string OtherName { get; set; } = string.Empty;
    public string OtherRole { get; set; } = string.Empty;
    public List<MessageVM> Messages { get; set; } = new();
}