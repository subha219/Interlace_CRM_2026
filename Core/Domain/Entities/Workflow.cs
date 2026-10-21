using Domain.Common;

namespace Domain.Entities;

/// <summary>When an event happens and the conditions match, run the actions.</summary>
public class WorkflowRule : BaseEntity
{
    public string? Name { get; set; }

    /// <summary>For example lead.created, lead.won, case.created.</summary>
    public string? TriggerEvent { get; set; }

    /// <summary>JSON array of { field, op, value }. All must match.</summary>
    public string? ConditionsJson { get; set; }

    /// <summary>JSON array of { type, ...parameters }.</summary>
    public string? ActionsJson { get; set; }
    public bool IsActive { get; set; } = true;
    public int RunCount { get; set; }
    public DateTime? LastRunAtUtc { get; set; }
}

public class WorkflowRun : BaseEntity
{
    public string? RuleId { get; set; }
    public string? RuleName { get; set; }
    public string? EventName { get; set; }
    public string? RecordId { get; set; }
    public string? RecordLabel { get; set; }
    public bool Success { get; set; }
    public string? Detail { get; set; }
    public DateTime RunAtUtc { get; set; }
}
