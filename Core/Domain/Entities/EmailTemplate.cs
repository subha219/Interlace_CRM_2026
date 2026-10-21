using Domain.Common;

namespace Domain.Entities;

public class EmailTemplate : BaseEntity
{
    public string? Name { get; set; }
    public string? Subject { get; set; }

    /// <summary>Plain text or HTML. Merge fields: {{name}}, {{company}}, {{email}}, {{number}}, {{sender}}.</summary>
    public string? Body { get; set; }

    /// <summary>Marketing templates are only sent to records that have not opted out.</summary>
    public bool IsMarketing { get; set; }
}
