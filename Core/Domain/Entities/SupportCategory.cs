using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

/// <summary>A kind of support case. Acts as a queue: new cases of this category can be routed to a default owner.</summary>
public class SupportCategory : BaseEntity
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? DefaultOwnerUserId { get; set; }
    public CasePriority? DefaultPriority { get; set; }
    public bool IsActive { get; set; } = true;
}
