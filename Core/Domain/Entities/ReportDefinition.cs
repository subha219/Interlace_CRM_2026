using Domain.Common;

namespace Domain.Entities;

/// <summary>A saved report: which records, filters, columns and grouping.</summary>
public class ReportDefinition : BaseEntity
{
    public string? Name { get; set; }
    public string? Entity { get; set; }

    /// <summary>JSON of the run request written by the report builder page.</summary>
    public string? DefinitionJson { get; set; }
    public string? OwnerUserId { get; set; }
    public string? OwnerName { get; set; }
    public bool IsShared { get; set; }
}
