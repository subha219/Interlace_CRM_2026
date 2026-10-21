using System.Security.Cryptography;
using System.Text;
using Application.Common.CQS.Queries;
using Application.Common.Repositories;
using Application.Common.Services.EmailManager;
using Application.Features.IntegrationManager;
using Application.Features.SupportCaseManager.Commands;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.PortalManager;

public static class PortalSecurity
{
    public static string NewToken() => IntegrationRules.NewSecret(32);

    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public static readonly TimeSpan LoginLifetime = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(8);
    public const int MaxLoginRequestsPerHour = 5;

    public static bool LoginUsable(DateTime? usedAt, DateTime? expiresAt, DateTime nowUtc) => usedAt == null && expiresAt != null && expiresAt > nowUtc;
    public static bool SessionUsable(DateTime? expiresAt, DateTime nowUtc) => expiresAt != null && expiresAt > nowUtc;
}

// ---------- sign-in ----------

public class RequestPortalLoginRequest : IRequest<bool>
{
    public string? Email { get; init; }
    public string? BaseUrl { get; set; }
}

public class RequestPortalLoginValidator : AbstractValidator<RequestPortalLoginRequest>
{
    public RequestPortalLoginValidator() { RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(255); }
}

/// <summary>Always completes the same way, so nobody can use it to find out which e-mails are customers.</summary>
public class RequestPortalLoginHandler : IRequestHandler<RequestPortalLoginRequest, bool>
{
    private readonly ICommandRepository<PortalSession> _repository;
    private readonly IQueryContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailService _email;

    public RequestPortalLoginHandler(ICommandRepository<PortalSession> repository, IQueryContext context, IUnitOfWork unitOfWork, IEmailService email)
    {
        _repository = repository; _context = context; _unitOfWork = unitOfWork; _email = email;
    }

    public async Task<bool> Handle(RequestPortalLoginRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email!.Trim().ToLowerInvariant();

        var customerId = await _context.Customer.AsNoTracking().Where(x => !x.IsDeleted && x.EmailAddress != null && x.EmailAddress.ToLower() == email)
            .Select(x => x.Id).FirstOrDefaultAsync(cancellationToken);
        customerId ??= await _context.CustomerContact.AsNoTracking().Where(x => !x.IsDeleted && x.CustomerId != null && x.EmailAddress != null && x.EmailAddress.ToLower() == email)
            .Select(x => x.CustomerId).FirstOrDefaultAsync(cancellationToken);
        if (customerId == null) return true;

        var since = DateTime.UtcNow.AddHours(-1);
        var recent = await _repository.GetQuery().CountAsync(x => x.Email == email && x.CreatedAtUtc > since, cancellationToken);
        if (recent >= PortalSecurity.MaxLoginRequestsPerHour) return true;

        var token = PortalSecurity.NewToken();
        await _repository.CreateAsync(new PortalSession
        {
            CustomerId = customerId, Email = email, LoginTokenHash = PortalSecurity.Hash(token),
            LoginExpiresAtUtc = DateTime.UtcNow.Add(PortalSecurity.LoginLifetime)
        }, cancellationToken);
        await _unitOfWork.SaveAsync(cancellationToken);

        var link = $"{(request.BaseUrl ?? "").TrimEnd('/')}/Accounts/Portal?login={Uri.EscapeDataString(token)}";
        await _email.SendEmailAsync(email, "Your customer portal link",
            $"<p>Hello,</p><p>Use this link to open the customer portal. It works once and expires in 30 minutes:</p><p><a href=\"{link}\">{link}</a></p><p>If you did not ask for it, you can ignore this e-mail.</p>");
        return true;
    }
}

public class PortalSessionResult { public string? SessionToken { get; init; } public string? CustomerName { get; init; } }

public class ExchangePortalLoginRequest : IRequest<PortalSessionResult> { public string? Token { get; init; } }

public class ExchangePortalLoginHandler : IRequestHandler<ExchangePortalLoginRequest, PortalSessionResult>
{
    private readonly ICommandRepository<PortalSession> _repository;
    private readonly IQueryContext _context;
    private readonly IUnitOfWork _unitOfWork;
    public ExchangePortalLoginHandler(ICommandRepository<PortalSession> repository, IQueryContext context, IUnitOfWork unitOfWork) { _repository = repository; _context = context; _unitOfWork = unitOfWork; }

    public async Task<PortalSessionResult> Handle(ExchangePortalLoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token)) throw new UnauthorizedAccessException("Invalid link.");
        var hash = PortalSecurity.Hash(request.Token);
        var row = await _repository.GetQuery().FirstOrDefaultAsync(x => !x.IsDeleted && x.LoginTokenHash == hash, cancellationToken);
        if (row == null || !PortalSecurity.LoginUsable(row.UsedAtUtc, row.LoginExpiresAtUtc, DateTime.UtcNow)) throw new UnauthorizedAccessException("This link has expired. Please request a new one.");

        var session = PortalSecurity.NewToken();
        row.UsedAtUtc = DateTime.UtcNow;
        row.SessionTokenHash = PortalSecurity.Hash(session);
        row.SessionExpiresAtUtc = DateTime.UtcNow.Add(PortalSecurity.SessionLifetime);
        _repository.Update(row);
        await _unitOfWork.SaveAsync(cancellationToken);

        var name = await _context.Customer.AsNoTracking().Where(x => x.Id == row.CustomerId).Select(x => x.Name).FirstOrDefaultAsync(cancellationToken);
        return new PortalSessionResult { SessionToken = session, CustomerName = name };
    }
}

public record PortalIdentity(string CustomerId, string? Email, string? CustomerName);

public class ResolvePortalSessionRequest : IRequest<PortalIdentity> { public string? Token { get; init; } public bool EndSession { get; init; } }

public class ResolvePortalSessionHandler : IRequestHandler<ResolvePortalSessionRequest, PortalIdentity>
{
    private readonly ICommandRepository<PortalSession> _repository;
    private readonly IQueryContext _context;
    private readonly IUnitOfWork _unitOfWork;
    public ResolvePortalSessionHandler(ICommandRepository<PortalSession> repository, IQueryContext context, IUnitOfWork unitOfWork) { _repository = repository; _context = context; _unitOfWork = unitOfWork; }

    public async Task<PortalIdentity> Handle(ResolvePortalSessionRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token)) throw new UnauthorizedAccessException("Please sign in.");
        var hash = PortalSecurity.Hash(request.Token);
        var row = await _repository.GetQuery().FirstOrDefaultAsync(x => !x.IsDeleted && x.SessionTokenHash == hash, cancellationToken);
        if (row == null || row.CustomerId == null || !PortalSecurity.SessionUsable(row.SessionExpiresAtUtc, DateTime.UtcNow)) throw new UnauthorizedAccessException("Your session has expired. Please sign in again.");

        if (request.EndSession)
        {
            row.SessionExpiresAtUtc = DateTime.UtcNow;
            _repository.Update(row);
            await _unitOfWork.SaveAsync(cancellationToken);
        }
        var name = await _context.Customer.AsNoTracking().Where(x => x.Id == row.CustomerId).Select(x => x.Name).FirstOrDefaultAsync(cancellationToken);
        return new PortalIdentity(row.CustomerId, row.Email, name);
    }
}

// ---------- the customer's own data ----------

public record PortalCaseRow(string Id, string? Number, string? Title, string Status, string? Priority, DateTime? CreatedAtUtc, DateTime? ResolvedAtUtc);
public record PortalInvoiceRow(string? Number, DateTime? InvoiceDate, DateTime? DueDate, double? Total, double? Balance, string? Status);
public record PortalContractRow(string? Number, string? Title, string? Status, DateTime? EndDate, double? Value);

public class PortalOverviewResult
{
    public string? CustomerName { get; init; }
    public List<PortalCaseRow> Cases { get; init; } = new();
    public List<PortalInvoiceRow> Invoices { get; init; } = new();
    public List<PortalContractRow> Contracts { get; init; } = new();
}

public class GetPortalOverviewRequest : IRequest<PortalOverviewResult> { public PortalIdentity? Identity { get; init; } }

public class GetPortalOverviewHandler : IRequestHandler<GetPortalOverviewRequest, PortalOverviewResult>
{
    private readonly IQueryContext _context;
    public GetPortalOverviewHandler(IQueryContext context) { _context = context; }

    public async Task<PortalOverviewResult> Handle(GetPortalOverviewRequest request, CancellationToken cancellationToken)
    {
        var id = request.Identity!.CustomerId;
        var cases = await _context.SupportCase.AsNoTracking().Where(x => !x.IsDeleted && x.CustomerId == id).OrderByDescending(x => x.CreatedAtUtc).Take(100)
            .Select(x => new { x.Id, x.Number, x.Title, x.Status, x.Priority, x.CreatedAtUtc, x.ResolvedAtUtc }).ToListAsync(cancellationToken);
        var invoices = await _context.Invoice.AsNoTracking().Where(x => !x.IsDeleted && x.CustomerId == id && x.InvoiceStatus != InvoiceStatus.Draft && x.InvoiceStatus != InvoiceStatus.Cancelled)
            .OrderByDescending(x => x.InvoiceDate).Take(100).Select(x => new { x.Number, x.InvoiceDate, x.DueDate, x.AfterTaxAmount, x.BalanceDue, x.InvoiceStatus }).ToListAsync(cancellationToken);
        var contracts = await _context.Contract.AsNoTracking().Where(x => !x.IsDeleted && x.CustomerId == id && x.Status != "Draft" && x.Status != "Cancelled")
            .OrderByDescending(x => x.EndDate).Take(50).Select(x => new { x.Number, x.Title, x.Status, x.EndDate, x.Value }).ToListAsync(cancellationToken);
        return new PortalOverviewResult
        {
            CustomerName = request.Identity.CustomerName,
            Cases = cases.Select(x => new PortalCaseRow(x.Id, x.Number, x.Title, (x.Status ?? CaseStatus.New).ToString(), x.Priority?.ToString(), x.CreatedAtUtc, x.ResolvedAtUtc)).ToList(),
            Invoices = invoices.Select(x => new PortalInvoiceRow(x.Number, x.InvoiceDate, x.DueDate, x.AfterTaxAmount, x.BalanceDue, x.InvoiceStatus?.ToString())).ToList(),
            Contracts = contracts.Select(x => new PortalContractRow(x.Number, x.Title, Application.Features.ContractManager.ContractRules.Effective(x.Status, x.EndDate, DateTime.Today), x.EndDate, x.Value)).ToList()
        };
    }
}

public record PortalComment(string Who, string? Text, DateTime? At);

public class PortalCaseResult
{
    public PortalCaseRow? Case { get; init; }
    public string? Description { get; init; }
    public string? Resolution { get; init; }
    public List<PortalComment> Comments { get; init; } = new();
}

public class GetPortalCaseRequest : IRequest<PortalCaseResult> { public PortalIdentity? Identity { get; init; } public string? CaseId { get; init; } }

public class GetPortalCaseHandler : IRequestHandler<GetPortalCaseRequest, PortalCaseResult>
{
    private readonly IQueryContext _context;
    public GetPortalCaseHandler(IQueryContext context) { _context = context; }

    public async Task<PortalCaseResult> Handle(GetPortalCaseRequest request, CancellationToken cancellationToken)
    {
        var c = await _context.SupportCase.AsNoTracking().FirstOrDefaultAsync(x => !x.IsDeleted && x.Id == request.CaseId && x.CustomerId == request.Identity!.CustomerId, cancellationToken)
            ?? throw new KeyNotFoundException("Case not found.");
        var comments = await _context.SupportCaseComment.AsNoTracking().Where(x => !x.IsDeleted && x.SupportCaseId == c.Id && x.VisibleToCustomer == true)
            .OrderBy(x => x.CreatedAtUtc).Select(x => new { x.Comment, x.CreatedAtUtc, x.FromCustomer }).ToListAsync(cancellationToken);
        return new PortalCaseResult
        {
            Case = new PortalCaseRow(c.Id, c.Number, c.Title, (c.Status ?? CaseStatus.New).ToString(), c.Priority?.ToString(), c.CreatedAtUtc, c.ResolvedAtUtc),
            Description = c.Description,
            Resolution = c.Resolution,
            Comments = comments.Select(x => new PortalComment(x.FromCustomer == true ? "You" : "Support", x.Comment, x.CreatedAtUtc)).ToList()
        };
    }
}

public class OpenPortalCaseResult { public string? Id { get; init; } public string? Number { get; init; } }

public class OpenPortalCaseRequest : IRequest<OpenPortalCaseResult>
{
    public PortalIdentity? Identity { get; set; }
    public string? Title { get; init; }
    public string? Description { get; init; }
    public string? Priority { get; init; }
}

public class OpenPortalCaseValidator : AbstractValidator<OpenPortalCaseRequest>
{
    public OpenPortalCaseValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.Priority).Must(p => string.IsNullOrEmpty(p) || p == "Low" || p == "Normal" || p == "High").WithMessage("Choose Low, Normal or High.");
    }
}

public class OpenPortalCaseHandler : IRequestHandler<OpenPortalCaseRequest, OpenPortalCaseResult>
{
    private readonly ISender _sender;
    public OpenPortalCaseHandler(ISender sender) { _sender = sender; }

    public async Task<OpenPortalCaseResult> Handle(OpenPortalCaseRequest request, CancellationToken cancellationToken)
    {
        var priority = request.Priority switch { "Low" => CasePriority.Low, "High" => CasePriority.High, _ => CasePriority.Normal };
        var result = await _sender.Send(new CreateSupportCaseRequest
        {
            Title = request.Title!.Trim(),
            Status = ((int)CaseStatus.New).ToString(),
            Priority = ((int)priority).ToString(),
            Channel = ((int)CaseChannel.Web).ToString(),
            CustomerId = request.Identity!.CustomerId,
            Description = $"{request.Description!.Trim()}\n\n(Opened in the customer portal by {request.Identity.Email})"
        }, cancellationToken);
        return new OpenPortalCaseResult { Id = result.Data?.Id, Number = result.Data?.Number };
    }
}

public class AddPortalCommentRequest : IRequest<bool>
{
    public PortalIdentity? Identity { get; set; }
    public string? CaseId { get; init; }
    public string? Text { get; init; }
}

public class AddPortalCommentValidator : AbstractValidator<AddPortalCommentRequest>
{
    public AddPortalCommentValidator()
    {
        RuleFor(x => x.CaseId).NotEmpty();
        RuleFor(x => x.Text).NotEmpty().MaximumLength(4000);
    }
}

public class AddPortalCommentHandler : IRequestHandler<AddPortalCommentRequest, bool>
{
    private readonly ICommandRepository<SupportCaseComment> _comments;
    private readonly ICommandRepository<SupportCase> _cases;
    private readonly ICommandRepository<Notification> _notifications;
    private readonly IUnitOfWork _unitOfWork;

    public AddPortalCommentHandler(ICommandRepository<SupportCaseComment> comments, ICommandRepository<SupportCase> cases, ICommandRepository<Notification> notifications, IUnitOfWork unitOfWork)
    {
        _comments = comments; _cases = cases; _notifications = notifications; _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(AddPortalCommentRequest request, CancellationToken cancellationToken)
    {
        var c = await _cases.GetQuery().FirstOrDefaultAsync(x => !x.IsDeleted && x.Id == request.CaseId && x.CustomerId == request.Identity!.CustomerId, cancellationToken)
            ?? throw new KeyNotFoundException("Case not found.");
        if (c.Status == CaseStatus.Closed) throw new Exception("This case is closed. Please open a new one.");

        await _comments.CreateAsync(new SupportCaseComment { SupportCaseId = c.Id, Comment = request.Text!.Trim(), VisibleToCustomer = true, FromCustomer = true }, cancellationToken);
        if (c.Status == CaseStatus.Resolved)
        {
            c.Status = CaseStatus.Open;
            c.ResolvedAtUtc = null;
            _cases.Update(c);
        }
        await _notifications.CreateAsync(new Notification
        {
            Title = "Customer replied", Message = $"{c.Number} \"{c.Title}\": the customer added a message in the portal.", Type = "Info",
            Url = "/SupportCases/SupportCaseList", UserId = c.OwnerUserId, IsRead = false, CreatedAtUtc = DateTime.UtcNow
        }, cancellationToken);
        await _unitOfWork.SaveAsync(cancellationToken);
        return true;
    }
}

// ---------- staff reply that the customer can see ----------

public class ReplyToCustomerRequest : IRequest<bool>
{
    public string? CaseId { get; init; }
    public string? Text { get; init; }
    public string? BaseUrl { get; set; }
    public string? UserId { get; set; }
}

public class ReplyToCustomerValidator : AbstractValidator<ReplyToCustomerRequest>
{
    public ReplyToCustomerValidator()
    {
        RuleFor(x => x.CaseId).NotEmpty();
        RuleFor(x => x.Text).NotEmpty().MaximumLength(4000);
    }
}

public class ReplyToCustomerHandler : IRequestHandler<ReplyToCustomerRequest, bool>
{
    private readonly ISender _sender;
    private readonly ICommandRepository<SupportCaseComment> _comments;
    private readonly IQueryContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailService _email;

    public ReplyToCustomerHandler(ISender sender, ICommandRepository<SupportCaseComment> comments, IQueryContext context, IUnitOfWork unitOfWork, IEmailService email)
    {
        _sender = sender; _comments = comments; _context = context; _unitOfWork = unitOfWork; _email = email;
    }

    public async Task<bool> Handle(ReplyToCustomerRequest request, CancellationToken cancellationToken)
    {
        var c = await _context.SupportCase.AsNoTracking().Where(x => !x.IsDeleted && x.Id == request.CaseId)
            .Select(x => new { x.Id, x.Number, x.Title, x.CustomerId }).FirstOrDefaultAsync(cancellationToken) ?? throw new Exception("Case not found.");

        //>>> goes through the normal comment command so first-response tracking still applies
        var created = await _sender.Send(new Application.Features.SupportCaseCommentManager.Commands.CreateSupportCaseCommentRequest
        {
            SupportCaseId = c.Id, Comment = request.Text!.Trim(), CreatedById = request.UserId
        }, cancellationToken);
        var row = await _comments.GetAsync(created.Data!.Id, cancellationToken);
        if (row != null)
        {
            row.VisibleToCustomer = true;
            _comments.Update(row);
            await _unitOfWork.SaveAsync(cancellationToken);
        }

        var to = await _context.Customer.AsNoTracking().Where(x => x.Id == c.CustomerId).Select(x => x.EmailAddress).FirstOrDefaultAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(to))
        {
            await _email.SendEmailAsync(to, $"New reply on case {c.Number}",
                $"<p>There is a new reply on your support case <strong>{System.Net.WebUtility.HtmlEncode(c.Title)}</strong> ({c.Number}).</p><p>Sign in to the customer portal to read it: <a href=\"{request.BaseUrl?.TrimEnd('/')}/Accounts/Portal\">{request.BaseUrl?.TrimEnd('/')}/Accounts/Portal</a></p>");
        }
        return true;
    }
}
