using Domain.Common;

namespace Domain.Entities;

/// <summary>A phone call placed or received through the telephony provider.</summary>
public class CallLog : BaseEntity
{
    /// <summary>Out or In.</summary>
    public string? Direction { get; set; }
    public string? CustomerNumber { get; set; }
    public string? AgentNumber { get; set; }

    /// <summary>Queued, Ringing, InProgress, Completed, Busy, NoAnswer or Failed.</summary>
    public string? Status { get; set; }
    public int DurationSeconds { get; set; }
    public string? ExternalId { get; set; }
    public string? RecordingUrl { get; set; }
    public string? Notes { get; set; }
    public string? Error { get; set; }
    public string? RelatedType { get; set; }
    public string? RelatedId { get; set; }
    public string? RelatedName { get; set; }
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public string? ActivityId { get; set; }
}
