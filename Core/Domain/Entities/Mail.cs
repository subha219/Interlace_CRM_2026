using Domain.Common;

namespace Domain.Entities;

/// <summary>One user's link to their Gmail mailbox (read-only). Only the encrypted refresh token is stored.</summary>
public class MailConnection : BaseEntity
{
    public string? UserId { get; set; }
    public string? Email { get; set; }
    public string? RefreshTokenProtected { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Mail received before this time (milliseconds since 1970, UTC) has already been looked at.</summary>
    public long? CursorMs { get; set; }
    public DateTime? LastSyncUtc { get; set; }
    public string? LastStatus { get; set; }
}

/// <summary>An e-mail exchanged with a customer or lead. Only the subject and a short preview are kept, never the full message.</summary>
public class EmailMessage : BaseEntity
{
    public string? UserId { get; set; }
    public string? GmailId { get; set; }
    public string? ThreadId { get; set; }
    public string? Subject { get; set; }
    public string? FromAddress { get; set; }
    public string? ToAddresses { get; set; }
    public string? Snippet { get; set; }

    /// <summary>In or Out.</summary>
    public string? Direction { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public string? RelatedType { get; set; }
    public string? RelatedId { get; set; }
    public string? RelatedName { get; set; }
    public string? ActivityId { get; set; }
}
