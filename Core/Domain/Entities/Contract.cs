using Domain.Common;

namespace Domain.Entities;

/// <summary>A customer agreement with a term, a value and a renewal date.</summary>
public class Contract : BaseEntity, IHasOwner
{
    public string? OwnerUserId { get; set; }
    public string? Number { get; set; }
    public string? Title { get; set; }
    public string? CustomerId { get; set; }

    /// <summary>Draft, Active, Cancelled or Renewed. "Expired" is worked out from the end date.</summary>
    public string? Status { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public double? Value { get; set; }

    /// <summary>Monthly, Quarterly, Yearly or OneTime.</summary>
    public string? BillingCycle { get; set; }
    public bool AutoRenew { get; set; }
    public int RenewalNoticeDays { get; set; } = 60;
    public string? Notes { get; set; }
    public string? RenewedFromId { get; set; }
}
