using Application.Common.CQS.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.SearchManager.Queries;

public record SearchHitDto(string Type, string? Id, string Title, string Subtitle, string Url);

public class GlobalSearchResult
{
    public List<SearchHitDto> Hits { get; init; } = new();
}

public class GlobalSearchRequest : IRequest<GlobalSearchResult>
{
    public string? Q { get; init; }
}

public class GlobalSearchHandler : IRequestHandler<GlobalSearchRequest, GlobalSearchResult>
{
    private const int PerType = 5;
    private readonly IQueryContext _context;

    public GlobalSearchHandler(IQueryContext context)
    {
        _context = context;
    }

    public async Task<GlobalSearchResult> Handle(GlobalSearchRequest request, CancellationToken cancellationToken)
    {
        var q = (request.Q ?? string.Empty).Trim();
        var hits = new List<SearchHitDto>();
        if (q.Length < 2)
        {
            return new GlobalSearchResult { Hits = hits };
        }

        var customers = await _context.Customer.AsNoTracking()
            .Where(x => !x.IsDeleted && ((x.Name ?? "").Contains(q) || (x.Number ?? "").Contains(q) || (x.EmailAddress ?? "").Contains(q) || (x.PhoneNumber ?? "").Contains(q)))
            .OrderBy(x => x.Name).Take(PerType)
            .Select(x => new { x.Id, x.Name, x.Number, x.EmailAddress })
            .ToListAsync(cancellationToken);
        hits.AddRange(customers.Select(x => new SearchHitDto("Customer", x.Id, x.Name ?? x.Number ?? "", $"{x.Number} {x.EmailAddress}".Trim(), "/Customers/Customer360?id=" + x.Id)));

        var leads = await _context.Lead.AsNoTracking()
            .Where(x => !x.IsDeleted && ((x.Title ?? "").Contains(q) || (x.Number ?? "").Contains(q) || (x.CompanyName ?? "").Contains(q)))
            .OrderByDescending(x => x.CreatedAtUtc).Take(PerType)
            .Select(x => new { x.Id, x.Title, x.Number, x.CompanyName })
            .ToListAsync(cancellationToken);
        hits.AddRange(leads.Select(x => new SearchHitDto("Lead", x.Id, x.Title ?? x.Number ?? "", $"{x.Number} {x.CompanyName}".Trim(), "/Pipelines/PipelineBoard")));

        var quotations = await _context.Quotation.AsNoTracking()
            .Where(x => !x.IsDeleted && (x.Number ?? "").Contains(q))
            .OrderByDescending(x => x.QuotationDate).Take(PerType)
            .Select(x => new { x.Id, x.Number, Customer = x.Customer != null ? x.Customer.Name : null })
            .ToListAsync(cancellationToken);
        hits.AddRange(quotations.Select(x => new SearchHitDto("Quotation", x.Id, x.Number ?? "", x.Customer ?? "", "/Quotations/QuotationList")));

        var invoices = await _context.Invoice.AsNoTracking()
            .Where(x => !x.IsDeleted && (x.Number ?? "").Contains(q))
            .OrderByDescending(x => x.InvoiceDate).Take(PerType)
            .Select(x => new { x.Id, x.Number, Customer = x.Customer != null ? x.Customer.Name : null })
            .ToListAsync(cancellationToken);
        hits.AddRange(invoices.Select(x => new SearchHitDto("Invoice", x.Id, x.Number ?? "", x.Customer ?? "", "/Invoices/InvoiceList")));

        var orders = await _context.SalesOrder.AsNoTracking()
            .Where(x => !x.IsDeleted && (x.Number ?? "").Contains(q))
            .OrderByDescending(x => x.OrderDate).Take(PerType)
            .Select(x => new { x.Id, x.Number, Customer = x.Customer != null ? x.Customer.Name : null })
            .ToListAsync(cancellationToken);
        hits.AddRange(orders.Select(x => new SearchHitDto("Sales order", x.Id, x.Number ?? "", x.Customer ?? "", "/SalesOrders/SalesOrderList")));

        var cases = await _context.SupportCase.AsNoTracking()
            .Where(x => !x.IsDeleted && ((x.Title ?? "").Contains(q) || (x.Number ?? "").Contains(q)))
            .OrderByDescending(x => x.CreatedAtUtc).Take(PerType)
            .Select(x => new { x.Id, x.Number, x.Title })
            .ToListAsync(cancellationToken);
        hits.AddRange(cases.Select(x => new SearchHitDto("Case", x.Id, x.Title ?? x.Number ?? "", x.Number ?? "", "/SupportCases/SupportCaseList")));

        var products = await _context.Product.AsNoTracking()
            .Where(x => !x.IsDeleted && ((x.Name ?? "").Contains(q) || (x.Number ?? "").Contains(q)))
            .OrderBy(x => x.Name).Take(PerType)
            .Select(x => new { x.Id, x.Name, x.Number })
            .ToListAsync(cancellationToken);
        hits.AddRange(products.Select(x => new SearchHitDto("Product", x.Id, x.Name ?? x.Number ?? "", x.Number ?? "", "/Products/ProductList")));

        var vendors = await _context.Vendor.AsNoTracking()
            .Where(x => !x.IsDeleted && ((x.Name ?? "").Contains(q) || (x.Number ?? "").Contains(q)))
            .OrderBy(x => x.Name).Take(PerType)
            .Select(x => new { x.Id, x.Name, x.Number })
            .ToListAsync(cancellationToken);
        hits.AddRange(vendors.Select(x => new SearchHitDto("Vendor", x.Id, x.Name ?? x.Number ?? "", x.Number ?? "", "/Vendors/VendorList")));

        return new GlobalSearchResult { Hits = hits };
    }
}
