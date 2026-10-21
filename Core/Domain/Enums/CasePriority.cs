using System.ComponentModel;

namespace Domain.Enums;

public enum CasePriority
{
    [Description("Low")]
    Low = 0,
    [Description("Normal")]
    Normal = 1,
    [Description("High")]
    High = 2,
    [Description("Critical")]
    Critical = 3
}
