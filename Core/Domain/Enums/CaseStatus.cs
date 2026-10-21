using System.ComponentModel;

namespace Domain.Enums;

public enum CaseStatus
{
    [Description("New")]
    New = 0,
    [Description("Open")]
    Open = 1,
    [Description("Pending")]
    Pending = 2,
    [Description("Resolved")]
    Resolved = 3,
    [Description("Closed")]
    Closed = 4
}
