using Domain.Common;

namespace Domain.Entities;

/// <summary>Single row of company-wide sales rules.</summary>
public class SalesPolicy : BaseEntity
{
    /// <summary>A quotation discount above this percentage needs approval.</summary>
    public double DiscountApprovalThresholdPercent { get; set; } = 10;
}
