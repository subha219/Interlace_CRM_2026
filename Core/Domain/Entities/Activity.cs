using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

/// <summary>A task, call, meeting, e-mail or note, optionally linked to a lead, customer or support case.</summary>
public class Activity : BaseEntity, IHasOwner
{
    public string? OwnerUserId { get; set; }
    public string? Number { get; set; }
    public string? Subject { get; set; }
    public string? Description { get; set; }
    public ActivityType? Type { get; set; }
    public ActivityStatus? Status { get; set; }

    /// <summary>Low, Normal or High.</summary>
    public string? Priority { get; set; }

    /// <summary>When it starts, or the due date for a task.</summary>
    public DateTime? StartAt { get; set; }
    public DateTime? EndAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Outcome { get; set; }

    /// <summary>Lead, Customer or SupportCase.</summary>
    public string? RelatedType { get; set; }
    public string? RelatedId { get; set; }
    public string? RelatedName { get; set; }

    public string? AssignedToUserId { get; set; }
}
