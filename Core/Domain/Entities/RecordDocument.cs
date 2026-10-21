using Domain.Common;

namespace Domain.Entities;

/// <summary>A file attached to a lead, customer, support case, quotation or invoice.</summary>
public class RecordDocument : BaseEntity
{
    public string? RelatedType { get; set; }
    public string? RelatedId { get; set; }
    public string? RelatedName { get; set; }

    /// <summary>Stored file name (see FileDocument).</summary>
    public string? GeneratedName { get; set; }
    public string? OriginalName { get; set; }
    public string? Description { get; set; }
    public long? FileSize { get; set; }
    public string? UploadedByName { get; set; }
}
