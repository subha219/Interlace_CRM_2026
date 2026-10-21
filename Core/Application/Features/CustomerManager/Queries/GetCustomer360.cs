using Application.Common.CQS.Queries;
using Application.Common.Extensions;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.CustomerManager.Queries;

public record C360DocDto(string? Id, string? Number, DateTime? Date, DateTime? DueDate, string? Status, double Amount, double Balance);
public record C360CaseDto(string? Id, string? Number, string? Title, string? Status, string? Priority, DateTime? DueDate, bool SlaBreached);
public record C360TimelineDto(DateTime? Date, string Type, string Title, string Detail, double? Amount, string Url);

public class GetCustomer360Result
{
    public string? Id { get; init; }
    public string? Number { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string? GroupName { get; init; }
    public string? CategoryName { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string? Website { get; init; }
    public string? Address { get; init; }
    public double LifetimeRevenue { get; init; }
    public double TotalBilled { get; init; }
    public double OpenBalance { get; init; }
    public double OverdueBalance { get; init; }
    public int InvoiceCount { get; init; }
    public int QuotationCount { get; init; }
    public int OrderCount { get; init; }
    public int OpenCaseCount { get; init; }
    public DateTime? LastInteraction { get; init; }
    public string HealthLabel { get; init; } = "Healthy";
    public List<string> HealthReasons { get; init; } = new();
    public List<C360DocDto> Quotations { get; init; } = new();
    public List<C360DocDto> Invoices { get; init; } = new();
    public List<C360DocDto> Orders { get; init; } = new();
    public List<C360CaseDto> Cases { get; init; } = new();
    public List<C360TimelineDto> Timeline { get; init; } = new();
}

public class GetCustomer360Request : IRequest<GetCustomer360Result>
{
    public string? CustomerId { get; init; }
}

public class GetCustomer360Handler : IRequestHandler<GetCustomer360Request, GetCustomer360Result>
{
    private readonly IQueryContext _context;

    public GetCustomer360Handler(IQueryContext context)
    {
        _context = context;
    }

    public async Task<GetCustomer360Result> Handle(GetCustomer360Request request, CancellationToken cancellationToken)
    {
        var id = request.CustomerId ?? string.Empty;
        var customer = await _context.Customer.AsNoTracking()
            .Include(x => x.CustomerGroup)
            .Include(x => x.CustomerCategory)
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);

        if (customer == null)
        {
            throw new Exception($"Entity not found: {request.CustomerId}");
        }

        var now = DateTime.Now;
        var today = now.Date;

        var invoices = await _context.Invoice.AsNoTracking()
            .Where(x => !x.IsDeleted && x.CustomerId == id)
            .OrderByDescending(x => x.InvoiceDate)
            .Select(x => new { x.Id, x.Number, x.InvoiceDate, x.DueDate, x.InvoiceStatus, Total = x.AfterTaxAmount ?? 0, Paid = x.AmountPaid ?? 0, Balance = x.BalanceDue ?? 0 })
            .ToListAsync(cancellationToken);

        var quotations = await _context.Quotation.AsNoTracking()
            .Where(x => !x.IsDeleted && x.CustomerId == id)
            .OrderByDescending(x => x.QuotationDate)
            .Select(x => new { x.Id, x.Number, x.QuotationDate, x.ValidUntil, x.QuotationStatus, Total = x.AfterTaxAmount ?? 0 })
            .ToListAsync(cancellationToken);

        var orders = await _context.SalesOrder.AsNoTracking()
            .Where(x => !x.IsDeleted && x.CustomerId == id)
            .OrderByDescending(x => x.OrderDate)
            .Select(x => new { x.Id, x.Number, x.OrderDate, x.OrderStatus, Total = x.AfterTaxAmount ?? 0 })
            .ToListAsync(cancellationToken);

        var cases = await _context.SupportCase.AsNoTracking()
            .Where(x => !x.IsDeleted && x.CustomerId == id)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new { x.Id, x.Number, x.Title, x.Status, x.Priority, x.DueDate, x.CreatedAtUtc })
            .ToListAsync(cancellationToken);

        var payments = await _context.Payment.AsNoTracking()
            .Where(x => !x.IsDeleted && x.Invoice != null && x.Invoice.CustomerId == id)
            .Select(x => new { x.Number, x.PaymentDate, Amount = x.Amount ?? 0 })
            .ToListAsync(cancellationToken);

        var counted = invoices.Where(x => x.InvoiceStatus != InvoiceStatus.Cancelled && x.InvoiceStatus != InvoiceStatus.Draft).ToList();
        var overdue = counted.Where(x => x.Balance > 0 && x.DueDate.HasValue && x.DueDate.Value.Date < today).ToList();
        var openCases = cases.Where(x => x.Status != CaseStatus.Resolved && x.Status != CaseStatus.Closed).ToList();
        var breached = openCases.Where(x => x.DueDate.HasValue && x.DueDate.Value < now).ToList();

        //>>> timeline
        var timeline = new List<C360TimelineDto>();
        timeline.AddRange(quotations.Select(x => new C360TimelineDto(x.QuotationDate, "Quotation", $"Quotation {x.Number}", x.QuotationStatus?.ToFriendlyName() ?? "", x.Total, "/Quotations/QuotationList")));
        timeline.AddRange(orders.Select(x => new C360TimelineDto(x.OrderDate, "Order", $"Sales order {x.Number}", x.OrderStatus?.ToFriendlyName() ?? "", x.Total, "/SalesOrders/SalesOrderList")));
        timeline.AddRange(invoices.Select(x => new C360TimelineDto(x.InvoiceDate, "Invoice", $"Invoice {x.Number}", x.InvoiceStatus?.ToFriendlyName() ?? "", x.Total, "/Invoices/InvoiceList")));
        timeline.AddRange(payments.Select(x => new C360TimelineDto(x.PaymentDate, "Payment", $"Payment {x.Number}", "Received", x.Amount, "/Payments/PaymentList")));
        timeline.AddRange(cases.Select(x => new C360TimelineDto(x.CreatedAtUtc?.ToLocalTime(), "Case", $"Case {x.Number}", $"{x.Title} ({x.Status?.ToFriendlyName()})", null, "/SupportCases/SupportCaseList")));
        timeline = timeline.Where(x => x.Date.HasValue).OrderByDescending(x => x.Date).Take(60).ToList();

        var lastInteraction = timeline.Select(x => x.Date).Max();

        //>>> relationship health with reasons
        var reasons = new List<string>();
        var label = "Healthy";
        if (overdue.Count > 0)
        {
            label = "At risk";
            reasons.Add($"{overdue.Count} overdue invoice(s) worth {overdue.Sum(x => x.Balance):N2}.");
        }
        if (breached.Count > 0)
        {
            label = "At risk";
            reasons.Add($"{breached.Count} support case(s) are past their SLA.");
        }
        if (label == "Healthy" && openCases.Count >= 3)
        {
            label = "Needs attention";
            reasons.Add($"{openCases.Count} open support cases.");
        }
        if (label == "Healthy" && lastInteraction.HasValue && (today - lastInteraction.Value.Date).TotalDays > 180)
        {
            label = "Needs attention";
            reasons.Add($"No activity for {(int)(today - lastInteraction.Value.Date).TotalDays} days. Churn risk.");
        }
        if (timeline.Count == 0)
        {
            reasons.Add("No history yet. Treat as a new prospect.");
        }
        if (reasons.Count == 0)
        {
            reasons.Add("Paying on time, no open issues, recent activity.");
        }

        var address = string.Join(", ", new[] { customer.Street, customer.City, customer.State, customer.ZipCode, customer.Country }.Where(x => !string.IsNullOrWhiteSpace(x)));

        return new GetCustomer360Result
        {
            Id = customer.Id,
            Number = customer.Number,
            Name = customer.Name,
            Description = customer.Description,
            GroupName = customer.CustomerGroup?.Name,
            CategoryName = customer.CustomerCategory?.Name,
            Email = customer.EmailAddress,
            Phone = customer.PhoneNumber,
            Website = customer.Website,
            Address = address,
            LifetimeRevenue = counted.Sum(x => x.Paid),
            TotalBilled = counted.Sum(x => x.Total),
            OpenBalance = counted.Sum(x => x.Balance),
            OverdueBalance = overdue.Sum(x => x.Balance),
            InvoiceCount = invoices.Count,
            QuotationCount = quotations.Count,
            OrderCount = orders.Count,
            OpenCaseCount = openCases.Count,
            LastInteraction = lastInteraction,
            HealthLabel = label,
            HealthReasons = reasons,
            Quotations = quotations.Select(x => new C360DocDto(x.Id, x.Number, x.QuotationDate, x.ValidUntil, x.QuotationStatus?.ToFriendlyName(), x.Total, 0)).ToList(),
            Invoices = invoices.Select(x => new C360DocDto(x.Id, x.Number, x.InvoiceDate, x.DueDate, x.InvoiceStatus?.ToFriendlyName(), x.Total, x.Balance)).ToList(),
            Orders = orders.Select(x => new C360DocDto(x.Id, x.Number, x.OrderDate, null, x.OrderStatus?.ToFriendlyName(), x.Total, 0)).ToList(),
            Cases = cases.Select(x => new C360CaseDto(x.Id, x.Number, x.Title, x.Status?.ToFriendlyName(), x.Priority?.ToFriendlyName(),
                x.DueDate, openCases.Any(c => c.Id == x.Id) && x.DueDate.HasValue && x.DueDate.Value < now)).ToList(),
            Timeline = timeline
        };
    }
}
