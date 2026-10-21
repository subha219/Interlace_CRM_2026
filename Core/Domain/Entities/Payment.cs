using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public class Payment : BaseEntity
{
    public string? Number { get; set; }
    public string? InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }
    public DateTime? PaymentDate { get; set; }
    public double? Amount { get; set; }
    public PaymentMethod? Method { get; set; }
    public string? Reference { get; set; }
    public string? Description { get; set; }
}
