using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

/// <summary>Service targets for one case priority.</summary>
public class SlaPolicy : BaseEntity
{
    public CasePriority Priority { get; set; }
    public int FirstResponseHours { get; set; }
    public int ResolutionHours { get; set; }

    /// <summary>Told once when a case of this priority breaches its resolution target.</summary>
    public string? EscalationUserId { get; set; }
}
