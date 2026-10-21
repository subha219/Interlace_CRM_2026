using System.ComponentModel;

namespace Domain.Enums;

public enum InvoiceStatus
{
    [Description("Draft")]
    Draft = 0,
    [Description("Sent")]
    Sent = 1,
    [Description("Partially Paid")]
    PartiallyPaid = 2,
    [Description("Paid")]
    Paid = 3,
    [Description("Overdue")]
    Overdue = 4,
    [Description("Cancelled")]
    Cancelled = 5
}
