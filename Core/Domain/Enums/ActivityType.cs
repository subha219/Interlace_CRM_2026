using System.ComponentModel;

namespace Domain.Enums;

public enum ActivityType
{
    [Description("Task")] Task = 0,
    [Description("Call")] Call = 1,
    [Description("Meeting")] Meeting = 2,
    [Description("Email")] Email = 3,
    [Description("Note")] Note = 4
}

public enum ActivityStatus
{
    [Description("Open")] Open = 0,
    [Description("Done")] Done = 1,
    [Description("Cancelled")] Cancelled = 2
}
