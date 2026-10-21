using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public class Quotation : BaseEntity, IHasOwner
{
    /// <summary>Id of the user who owns this record; set to the creator on insert.</summary>
    public string? OwnerUserId { get; set; }

    public string? Number { get; set; }
    public DateTime? QuotationDate { get; set; }
    public QuotationStatus? QuotationStatus { get; set; }
    public DateTime? ValidUntil { get; set; }
    public string? ConvertedInvoiceId { get; set; }
    public string? ConvertedSalesOrderId { get; set; }
    public string? Description { get; set; }
    public string? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public string? TaxId { get; set; }
    public Tax? Tax { get; set; }
    public double? BeforeTaxAmount { get; set; }
    public double? TaxAmount { get; set; }
    public double? AfterTaxAmount { get; set; }

    /// <summary>Discount on the item subtotal, in percent. BeforeTaxAmount is the subtotal after this discount.</summary>
    public double? DiscountPercent { get; set; }
    public double? DiscountAmount { get; set; }

    /// <summary>None, Required, Pending, Approved or Rejected. Null means None.</summary>
    public string? ApprovalStatus { get; set; }
    public string? ApprovalRequestedById { get; set; }
    public string? ApprovalDecidedById { get; set; }
    public DateTime? ApprovalDecidedAtUtc { get; set; }
    public string? ApprovalNote { get; set; }

    /// <summary>Null or 1 for the first version.</summary>
    public int? RevisionNumber { get; set; }
    public string? ParentQuotationId { get; set; }
    public ICollection<QuotationItem> QuotationItemList { get; set; } = new List<QuotationItem>();
}
