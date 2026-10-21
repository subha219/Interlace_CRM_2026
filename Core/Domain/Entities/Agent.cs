using Domain.Common;

namespace Domain.Entities;

/// <summary>Switch and size limit for one AI agent. Agents are off until an administrator turns them on.</summary>
public class AgentSetting : BaseEntity
{
    public string? AgentKey { get; set; }
    public bool Enabled { get; set; }
    public int MaxPerRun { get; set; } = 20;

    /// <summary>When true the agent also runs by itself about once a day. Null means off.</summary>
    public bool? AutoRun { get; set; }
}

/// <summary>One run of an agent: what it looked at and how many proposals it made.</summary>
public class AgentRun : BaseEntity
{
    public string? AgentKey { get; set; }
    public string? RunByUserId { get; set; }
    public string? RunByName { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? FinishedAtUtc { get; set; }
    public int Scanned { get; set; }
    public int Proposed { get; set; }
    public bool UsedAi { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Something an agent suggests. It changes nothing by itself: a person with the right role approves or rejects it.
/// Kind is Task or SetPriority; Payload is a small JSON object with the details.
/// </summary>
public class AgentProposal : BaseEntity
{
    public string? AgentKey { get; set; }
    public string? RunId { get; set; }
    public string? RelatedType { get; set; }
    public string? RelatedId { get; set; }
    public string? RelatedName { get; set; }
    public string? Kind { get; set; }
    public string? Title { get; set; }
    public string? Reason { get; set; }
    public string? Payload { get; set; }

    /// <summary>The module role a person needs to approve this (for example Activities or SupportCases).</summary>
    public string? RequiredRole { get; set; }

    /// <summary>ai or rules: who worked out the suggestion.</summary>
    public string? Source { get; set; }

    /// <summary>Pending, Approved, Rejected or Failed.</summary>
    public string? Status { get; set; }

    /// <summary>One open proposal per agent and record.</summary>
    public string? DedupKey { get; set; }
    public string? DecidedByUserId { get; set; }
    public string? DecidedByName { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    public string? Result { get; set; }
}
