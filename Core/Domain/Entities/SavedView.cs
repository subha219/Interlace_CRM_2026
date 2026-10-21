using Domain.Common;

namespace Domain.Entities;

/// <summary>A named grid state (filters, sorting, columns, search) on one page.</summary>
public class SavedView : BaseEntity
{
    /// <summary>Page path, for example /Leads/LeadList.</summary>
    public string? Page { get; set; }
    public string? Name { get; set; }

    /// <summary>JSON written and read by the grid script.</summary>
    public string? StateJson { get; set; }
    public string? OwnerUserId { get; set; }
    public string? OwnerName { get; set; }

    /// <summary>Shared views are visible to every user.</summary>
    public bool IsShared { get; set; }
}
