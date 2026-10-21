using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public class SupportCaseComment : BaseEntity
{
    public string? SupportCaseId { get; set; }
    public SupportCase? SupportCase { get; set; }
    public string? Comment { get; set; }

    /// <summary>Shown to the customer in the portal. Internal notes stay false/null.</summary>
    public bool? VisibleToCustomer { get; set; }

    /// <summary>Written by the customer in the portal.</summary>
    public bool? FromCustomer { get; set; }
}
