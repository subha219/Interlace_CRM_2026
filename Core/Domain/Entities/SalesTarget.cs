using Domain.Common;

namespace Domain.Entities;

/// <summary>Revenue target of one user for one month.</summary>
public class SalesTarget : BaseEntity
{
    public string? UserId { get; set; }
    public int Year { get; set; }

    /// <summary>1 to 12.</summary>
    public int Month { get; set; }
    public double Amount { get; set; }
}
