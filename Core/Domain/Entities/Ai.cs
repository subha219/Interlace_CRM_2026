using Domain.Common;

namespace Domain.Entities;

/// <summary>Company-wide AI switches and limits (a single row).</summary>
public class AiSetting : BaseEntity
{
    public bool Enabled { get; set; } = true;
    public int DailyLimitPerUser { get; set; } = 50;
    public int MonthlyLimitTotal { get; set; } = 2000;
}

/// <summary>One request that reached the AI model. Stores sizes, not the text itself.</summary>
public class AiUsageLog : BaseEntity
{
    public string? UserId { get; set; }
    public string? Feature { get; set; }
    public string? RelatedType { get; set; }
    public string? RelatedId { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int InputChars { get; set; }
    public int OutputChars { get; set; }
    public int DurationMs { get; set; }
}
