using Domain.Common;

namespace Domain.Entities;

public class AuditLog : BaseEntity
{
    public string? EntityName { get; set; }
    public string? EntityId { get; set; }

    /// <summary>Created, Updated or Deleted.</summary>
    public string? Action { get; set; }
    public string? UserId { get; set; }
    public string? UserName { get; set; }

    /// <summary>JSON array of { field, old, new }.</summary>
    public string? Changes { get; set; }
}
