using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;
using Staybnb.Services;
using Staybnb.ViewModels;

namespace Staybnb.Controllers;

/// <summary>
/// Database-backed inbox. A "conversation" is all messages between two users.
/// </summary>
[Authorize]
public class MessagesController : Controller
{
    private const int MaxLength = 2000;
    private const int MaxShown = 200;

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly INotificationService _notifications;

    public MessagesController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, INotificationService notifications)
    {
        _db = db;
        _userManager = userManager;
        _notifications = notifications;
    }

    private string UserId => _userManager.GetUserId(User)!;

    // Inbox
    public async Task<IActionResult> Index()
    {
        var me = UserId;

        // One row per person I've exchanged messages with
        var groups = await _db.Messages.AsNoTracking()
            .Where(m => m.SenderId == me || m.ReceiverId == me)
            .GroupBy(m => m.SenderId == me ? m.ReceiverId : m.SenderId)
            .Select(g => new
            {
                OtherId = g.Key,
                LastId = g.Max(m => m.Id),
                Unread = g.Count(m => m.ReceiverId == me && !m.IsRead)
            })
            .OrderByDescending(g => g.LastId)
            .ToListAsync();

        var lastIds = groups.Select(g => g.LastId).ToList();
        var otherIds = groups.Select(g => g.OtherId).ToList();

        var lastMessages = await _db.Messages.AsNoTracking()
            .Where(m => lastIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id);

        var users = await _userManager.Users.AsNoTracking()
            .Where(u => otherIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id);

        var roles = await RoleLabels.HighestRolesAsync(_db, otherIds);

        var rows = groups
            .Where(g => users.ContainsKey(g.OtherId))
            .Select(g =>
            {
                var last = lastMessages[g.LastId];
                var preview = last.Content.Length > 80 ? last.Content[..80] + "…" : last.Content;
                return new ConversationRowVM
                {
                    OtherUserId = g.OtherId,
                    OtherName = users[g.OtherId].FullName,
                    OtherRole = roles.GetValueOrDefault(g.OtherId, "User"),
                    Preview = preview,
                    LastIsMine = last.SenderId == me,
                    LastTimestamp = last.Timestamp,
                    UnreadCount = g.Unread
                };
            })
            .ToList();

        return View(rows);
    }

    // One conversation 
    [HttpGet]
    public async Task<IActionResult> Conversation(string id)
    {
        var me = UserId;
        if (id == me) return RedirectToAction(nameof(Index));

        var other = await _userManager.FindByIdAsync(id);
        if (other == null) return NotFound();

        var messages = await _db.Messages.AsNoTracking()
            .Where(m => (m.SenderId == me && m.ReceiverId == id) || (m.SenderId == id && m.ReceiverId == me))
            .OrderByDescending(m => m.Id)
            .Take(MaxShown)
            .ToListAsync();
        messages.Reverse();

        // Opening the conversation marks everything they sent me as read (persisted in the database)
        await _db.Messages
            .Where(m => m.SenderId == id && m.ReceiverId == me && !m.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.IsRead, true));

        // ...and clears the matching "New message" notification
        var conversationLink = $"/Messages/Conversation/{id}";
        await _db.Notifications
            .Where(n => n.UserId == me && n.Type == NotificationType.NewMessage && !n.IsRead && n.LinkUrl == conversationLink)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));

        var roles = await RoleLabels.HighestRolesAsync(_db, new[] { id });

        return View(new ConversationVM
        {
            OtherUserId = id,
            OtherName = other.FullName,
            OtherRole = roles.GetValueOrDefault(id, "User"),
            Messages = messages.Select(m => new MessageVM
            {
                Id = m.Id,
                Content = m.Content,
                Timestamp = m.Timestamp,
                IsMine = m.SenderId == me,
                IsRead = m.IsRead
            }).ToList()
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Send(string id, string? content)
    {
        var me = UserId;
        if (id == me) return RedirectToAction(nameof(Index));

        var other = await _userManager.FindByIdAsync(id);
        if (other == null) return NotFound();

        var text = (content ?? string.Empty).Trim();
        if (text.Length == 0 || text.Length > MaxLength)
        {
            TempData["Error"] = $"Write a message between 1 and {MaxLength} characters.";
            return RedirectToAction(nameof(Conversation), new { id });
        }

        _db.Messages.Add(new Message
        {
            SenderId = me,
            ReceiverId = id,
            Content = text
        });

        // One "New message" notification per conversation until the recipient opens it (no spam)
        var link = $"/Messages/Conversation/{me}";
        var alreadyNotified = await _db.Notifications.AnyAsync(n =>
            n.UserId == id && n.Type == NotificationType.NewMessage && !n.IsRead && n.LinkUrl == link);

        if (!alreadyNotified)
        {
            var sender = await _userManager.GetUserAsync(User);
            _notifications.Notify(id, "New message", $"{sender?.FullName ?? "Someone"} sent you a message.",
                NotificationType.NewMessage, link);
        }

        await _db.SaveChangesAsync();

        return RedirectToAction(nameof(Conversation), new { id });
    }
}