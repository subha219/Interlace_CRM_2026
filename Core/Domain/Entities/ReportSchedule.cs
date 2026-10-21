using Domain.Common;

namespace Domain.Entities;

/// <summary>E-mails a saved report on a schedule. It runs as the report's owner, with the owner's current access.</summary>
public class ReportSchedule : BaseEntity
{
    public string? ReportId { get; set; }
    public string? OwnerUserId { get; set; }

    /// <summary>Daily, Weekly or Monthly.</summary>
    public string? Frequency { get; set; }

    /// <summary>Weekly: 0 (Sunday) to 6. Monthly: day of month 1 to 28. Daily: unused.</summary>
    public int Day { get; set; }

    /// <summary>Hour of the day, server local time, 0 to 23.</summary>
    public int Hour { get; set; }

    /// <summary>Comma separated e-mail addresses (at most 10).</summary>
    public string? Recipients { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastRunAtUtc { get; set; }
    public string? LastStatus { get; set; }

    /// <summary>What to attach: Csv, Pdf or Both. Null means Csv.</summary>
    public string? Attach { get; set; }
}
