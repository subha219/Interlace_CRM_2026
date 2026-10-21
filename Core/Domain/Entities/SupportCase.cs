using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public class SupportCase : BaseEntity, IHasOwner
{
    /// <summary>Id of the user who owns this record; set to the creator on insert.</summary>
    public string? OwnerUserId { get; set; }

    public string? Number { get; set; }
    public string? Title { get; set; }
    public CaseStatus? Status { get; set; }
    public CasePriority? Priority { get; set; }
    public CaseChannel? Channel { get; set; }
    public string? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public string? SalesRepresentativeId { get; set; }
    public SalesRepresentative? SalesRepresentative { get; set; }
    public DateTime? DueDate { get; set; }
    public string? Description { get; set; }
    public string? Resolution { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }

    /// <summary>When the first reply is due (local time, like DueDate).</summary>
    public DateTime? FirstResponseDue { get; set; }
    public DateTime? FirstRespondedAt { get; set; }
    public DateTime? EscalatedAt { get; set; }

    /// <summary>Optional category (queue), see SupportCategory.</summary>
    public string? CategoryId { get; set; }

    /// <summary>Customer satisfaction, 1 to 5.</summary>
    public int? CsatScore { get; set; }
    public string? CsatComment { get; set; }
    public string? CsatToken { get; set; }
    public DateTime? CsatSubmittedAt { get; set; }
    public ICollection<SupportCaseComment> CommentList { get; set; } = new List<SupportCaseComment>();
}
