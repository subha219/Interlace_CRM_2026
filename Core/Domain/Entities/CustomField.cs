using Domain.Common;

namespace Domain.Entities;

/// <summary>Admin-defined extra field on Lead, Customer or SupportCase.</summary>
public class CustomFieldDefinition : BaseEntity
{
    /// <summary>Lead, Customer or SupportCase.</summary>
    public string? Entity { get; set; }
    public string? Label { get; set; }

    /// <summary>Text, Number, Date, Boolean or Picklist.</summary>
    public string? FieldType { get; set; }

    /// <summary>Picklist choices, one per line.</summary>
    public string? Options { get; set; }
    public bool IsRequired { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

public class CustomFieldValue : BaseEntity
{
    public string? Entity { get; set; }
    public string? RecordId { get; set; }
    public string? DefinitionId { get; set; }
    public string? Value { get; set; }
}
