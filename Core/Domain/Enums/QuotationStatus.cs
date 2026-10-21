using System.ComponentModel;

namespace Domain.Enums;

public enum QuotationStatus
{
    [Description("Draft")]
    Draft = 0,
    [Description("Sent")]
    Sent = 1,
    [Description("Accepted")]
    Accepted = 2,
    [Description("Rejected")]
    Rejected = 3,
    [Description("Expired")]
    Expired = 4,
    [Description("Converted")]
    Converted = 5,
    [Description("Cancelled")]
    Cancelled = 6
}
