using Domain.Common;

namespace Domain.Entities;

public class QuotationItem : BaseEntity
{
    public string? QuotationId { get; set; }
    public Quotation? Quotation { get; set; }
    public string? ProductId { get; set; }
    public Product? Product { get; set; }
    public string? Summary { get; set; }
    public double? UnitPrice { get; set; } = 0;
    public double? Quantity { get; set; } = 1;
    public double? Total { get; set; } = 0;

}
