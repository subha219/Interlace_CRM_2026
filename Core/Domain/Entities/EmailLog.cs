using Domain.Common;

namespace Domain.Entities;

/// <summary>One e-mail sent from the CRM.</summary>
public class EmailLog : BaseEntity
{
    public string? ToEmail { get; set; }
    public string? Subject { get; set; }
    public string? Body { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? RelatedType { get; set; }
    public string? RelatedId { get; set; }
    public string? RelatedName { get; set; }
    public string? SentByUserId { get; set; }
    public string? SentByName { get; set; }
    public DateTime? SentAtUtc { get; set; }
}
