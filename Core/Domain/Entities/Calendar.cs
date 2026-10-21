using Domain.Common;

namespace Domain.Entities;

/// <summary>One user's link to an outside calendar (Google). Only the encrypted refresh token is stored.</summary>
public class CalendarConnection : BaseEntity
{
    public string? UserId { get; set; }
    public string? Provider { get; set; }
    public string? Email { get; set; }
    public string? RefreshTokenProtected { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastSyncUtc { get; set; }
    public string? LastStatus { get; set; }
}

/// <summary>Which calendar event belongs to which activity, and what was last sent (hash).</summary>
public class CalendarEventLink : BaseEntity
{
    public string? ActivityId { get; set; }
    public string? UserId { get; set; }
    public string? ExternalEventId { get; set; }
    public string? Hash { get; set; }
    public DateTime? SyncedAtUtc { get; set; }
}
