using Staybnb.Data;
using Staybnb.Models;

namespace Staybnb.Services;

public interface IActivityLogger
{
    /// <summary>
    /// Queues a log entry. It is saved by the caller's next SaveChangesAsync, so the entry and the
    /// change it describes are stored together (or not at all).
    /// </summary>
    void Log(string userId, string action, ActivityType type);

    /// <summary>Records a log entry immediately. Use where no other save follows (login, logout, registration).</summary>
    Task LogAsync(string userId, string action, ActivityType type);
}

/// <summary>Audit trail of security-relevant actions: who did what, and when.</summary>
public class ActivityLogger : IActivityLogger
{
    private const int ActionMax = 100;

    private readonly ApplicationDbContext _db;

    public ActivityLogger(ApplicationDbContext db) => _db = db;

    public void Log(string userId, string action, ActivityType type)
    {
        _db.ActivityLogs.Add(new ActivityLog
        {
            UserId = userId,
            Action = action.Length > ActionMax ? action[..ActionMax] : action,
            ActivityType = type
        });
    }

    public async Task LogAsync(string userId, string action, ActivityType type)
    {
        Log(userId, action, type);
        await _db.SaveChangesAsync();
    }
}