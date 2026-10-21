using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

/// <summary>One row per change of a lead's pipeline stage or closing result (written automatically on save).</summary>
public class LeadStageHistory : BaseEntity
{
    public string? LeadId { get; set; }
    public PipelineStage? FromStage { get; set; }
    public PipelineStage? ToStage { get; set; }
    public ClosingStatus? FromClosing { get; set; }
    public ClosingStatus? ToClosing { get; set; }
    public double? Amount { get; set; }
    public string? OwnerUserId { get; set; }
    public string? ChangedByUserId { get; set; }
    public DateTime ChangedAtUtc { get; set; }
}
