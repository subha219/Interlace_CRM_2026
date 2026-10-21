using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;


public class Customer : BaseEntity, IHasOwner
{
    /// <summary>Id of the user who owns this record; set to the creator on insert.</summary>
    public string? OwnerUserId { get; set; }

    public string? Name { get; set; }
    public string? Number { get; set; }
    public string? Description { get; set; }
    public string? Street { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
    public string? Country { get; set; }
    public string? PhoneNumber { get; set; }
    public string? FaxNumber { get; set; }
    public string? EmailAddress { get; set; }
    public string? Website { get; set; }
    public string? WhatsApp { get; set; }
    public string? LinkedIn { get; set; }
    public string? Facebook { get; set; }
    public string? Instagram { get; set; }
    public string? TwitterX { get; set; }
    public string? TikTok { get; set; }
    public string? CustomerGroupId { get; set; }
    public CustomerGroup? CustomerGroup { get; set; }
    public string? CustomerCategoryId { get; set; }
    public CustomerCategory? CustomerCategory { get; set; }
    public LifecycleStage? LifecycleStage { get; set; }
    public string? Source { get; set; }
    public string? Tags { get; set; }
    public bool? ConsentMarketing { get; set; }
    public DateTime? ConsentUpdatedAtUtc { get; set; }
    /// <summary>The lead this customer was converted from, if any.</summary>
    public string? SourceLeadId { get; set; }
    public ICollection<CustomerContact> CustomerContactList { get; set; } = new List<CustomerContact>();
}
