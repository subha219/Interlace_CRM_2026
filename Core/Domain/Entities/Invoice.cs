using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public class Invoice : BaseEntity
{
    public string? Number { get; set; }
    public DateTime? InvoiceDate { get; set; }
    public InvoiceStatus? InvoiceStatus { get; set; }
    public DateTime? DueDate { get; set; }
    public string? QuotationId { get; set; }
    public string? Description { get; set; }
    public string? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public string? TaxId { get; set; }
    public Tax? Tax { get; set; }
    public double? BeforeTaxAmount { get; set; }
    public double? TaxAmount { get; set; }
    public double? AfterTaxAmount { get; set; }
    public double? AmountPaid { get; set; }
    public double? BalanceDue { get; set; }
    public ICollection<InvoiceItem> InvoiceItemList { get; set; } = new List<InvoiceItem>();
    public ICollection<Payment> PaymentList { get; set; } = new List<Payment>();
}
