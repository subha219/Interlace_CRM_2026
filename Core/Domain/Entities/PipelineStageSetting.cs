using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

/// <summary>Company-specific name and win probability of one pipeline stage.</summary>
public class PipelineStageSetting : BaseEntity
{
    public PipelineStage Stage { get; set; }
    public string? Label { get; set; }

    /// <summary>0 to 100.</summary>
    public double Probability { get; set; }
}
