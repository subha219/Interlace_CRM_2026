using System.Net;
using System.Security.Cryptography;
using System.Text;
using Application.Common.CQS.Queries;
using Application.Common.Repositories;
using Application.Common.Services.EmailManager;
using Application.Features.ContractManager;
using Application.Features.NumberSequenceManager;
using Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.SignatureManager;

public static class SignatureRules
{
    public const int MaxOtpAttempts = 5;
    public const int MaxOtpPerRequest = 5;
    public static readonly TimeSpan OtpLifetime = TimeSpan.FromMinutes(10);
    public const int DefaultExpiryDays = 14;

    public static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static string NewOtp() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    /// <summary>The code is tied to the request so a code for one document is useless for another.</summary>
    public static string OtpHash(string requestId, string otp) => Hash(requestId + ":" + (otp ?? "").Trim());

    public static bool FixedEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a ?? ""), Encoding.UTF8.GetBytes(b ?? ""));

    /// <summary>Status people see: an unsigned request past its date is Expired.</summary>
    public static string Effective(string? status, DateTime? expiresAtUtc, DateTime nowUtc) =>
        (status is "Sent" or "Viewed") && expiresAtUtc != null && expiresAtUtc < nowUtc ? "Expired" : status ?? "Sent";

    public static bool CanSign(string? status, DateTime? expiresAtUtc, DateTime nowUtc) => Effective(status, expiresAtUtc, nowUtc) is "Sent" or "Viewed";

    public static bool OtpUsable(string? hash, DateTime? expiresAtUtc, int attempts, DateTime nowUtc) =>
        !string.IsNullOrEmpty(hash) && expiresAtUtc != null && expiresAtUtc > nowUtc && attempts < MaxOtpAttempts;

    public static string ContractText(Contract c, string? customerName)
    {
        string Date(DateTime? d) => d == null ? "-" : d.Value.ToString("dd MMM yyyy");
        var sb = new StringBuilder();
        sb.AppendLine($"CONTRACT {c.Number}");
        sb.AppendLine(c.Title ?? "");
        sb.AppendLine();
        sb.AppendLine($"Customer: {customerName ?? "-"}");
        sb.AppendLine($"Start date: {Date(c.StartDate)}");
        sb.AppendLine($"End date: {Date(c.EndDate)}");
        sb.AppendLine($"Value: {(c.Value == null ? "-" : c.Value.Value.ToString("N2", System.Globalization.CultureInfo.InvariantCulture))}");
        sb.AppendLine($"Billing: {c.BillingCycle ?? "-"}");
        sb.AppendLine($"Renews automatically: {(c.AutoRenew ? "Yes" : "No")}");
        if (!string.IsNullOrWhiteSpace(c.Notes)) { sb.AppendLine(); sb.AppendLine("Terms and notes:"); sb.AppendLine(c.Notes.Trim()); }
        return sb.ToString().TrimEnd();
    }

    public static string QuotationText(Quotation q, string? customerName, IEnumerable<(string? Product, string? Summary, double Qty, double Price, double Total)> items)
    {
        string Date(DateTime? d) => d == null ? "-" : d.Value.ToString("dd MMM yyyy");
        string N(double? v) => (v ?? 0).ToString("N2", System.Globalization.CultureInfo.InvariantCulture);
        var sb = new StringBuilder();
        sb.AppendLine($"QUOTATION {q.Number}" + (q.RevisionNumber is > 1 ? $" (revision {q.RevisionNumber})" : ""));
        sb.AppendLine($"Customer: {customerName ?? "-"}");
        sb.AppendLine($"Date: {Date(q.QuotationDate)}");
        sb.AppendLine($"Valid until: {Date(q.ValidUntil)}");
        sb.AppendLine();
        sb.AppendLine("Items:");
        foreach (var i in items)
        {
            var label = string.IsNullOrWhiteSpace(i.Summary) ? i.Product : $"{i.Product} - {i.Summary}";
            sb.AppendLine($"- {label}: {i.Qty:0.##} x {N(i.Price)} = {N(i.Total)}");
        }
        sb.AppendLine();
        if ((q.DiscountPercent ?? 0) > 0) sb.AppendLine($"Discount: {q.DiscountPercent:0.##}% (-{N(q.DiscountAmount)})");
        sb.AppendLine($"Amount before tax: {N(q.BeforeTaxAmount)}");
        sb.AppendLine($"Tax: {N(q.TaxAmount)}");
        sb.AppendLine($"Total: {N(q.AfterTaxAmount)}");
        if (!string.IsNullOrWhiteSpace(q.Description)) { sb.AppendLine(); sb.AppendLine("Notes:"); sb.AppendLine(q.Description.Trim()); }
        return sb.ToString().TrimEnd();
    }

    /// <summary>A quotation may go out for signature only when it is final: approved if it needed approval, and not already closed.</summary>
    public static string? QuotationBlocker(Quotation q)
    {
        if (q.ApprovalStatus is "Required" or "Pending") return "This quotation needs approval before it can be sent for signature.";
        if (q.ApprovalStatus == "Rejected") return "This quotation was rejected. Revise it first.";
        if (q.QuotationStatus is Domain.Enums.QuotationStatus.Cancelled or Domain.Enums.QuotationStatus.Rejected or Domain.Enums.QuotationStatus.Expired or Domain.Enums.QuotationStatus.Converted)
            return $"A {q.QuotationStatus!.Value.ToString().ToLowerInvariant()} quotation cannot be sent for signature.";
        return null;
    }

    public static bool LooksLikeEmail(string? e) =>
        !string.IsNullOrWhiteSpace(e) && e.Length <= 255 && !e.Contains(' ') && e.IndexOf('@') > 0 && e.IndexOf('@') < e.Length - 3;
}

public class SignRequestDto
{
    public string? Id { get; init; }
    public string? Number { get; init; }
    public string? RelatedType { get; init; }
    public string? RelatedId { get; init; }
    public string? Title { get; init; }
    public string? SignerName { get; init; }
    public string? SignerEmail { get; init; }
    public string? Status { get; init; }
    public DateTime? ExpiresAtUtc { get; init; }
    public DateTime? CreatedAtUtc { get; init; }
    public DateTime? ViewedAtUtc { get; init; }
    public DateTime? SignedAtUtc { get; init; }
    public string? SignedName { get; init; }
    public string? SignedIp { get; init; }
    public string? SignedUserAgent { get; init; }
    public string? DocumentHash { get; init; }
    public string? DeclineReason { get; init; }
    public string? RequestedByName { get; init; }
}

// ---------- staff side ----------

public class GetSignRequestsResult { public List<SignRequestDto> Data { get; init; } = new(); }
public class GetSignRequestsRequest : IRequest<GetSignRequestsResult> { public string? RelatedId { get; init; } }

public class GetSignRequestsHandler : IRequestHandler<GetSignRequestsRequest, GetSignRequestsResult>
{
    private readonly IQueryContext _context;
    public GetSignRequestsHandler(IQueryContext context) { _context = context; }

    public async Task<GetSignRequestsResult> Handle(GetSignRequestsRequest request, CancellationToken cancellationToken)
    {
        var q = _context.SignRequest.AsNoTracking().Where(x => !x.IsDeleted);
        if (!string.IsNullOrEmpty(request.RelatedId)) q = q.Where(x => x.RelatedId == request.RelatedId);
        var rows = await q.OrderByDescending(x => x.CreatedAtUtc).Take(300).ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        return new GetSignRequestsResult
        {
            Data = rows.Select(x => new SignRequestDto
            {
                Id = x.Id, Number = x.Number, RelatedType = x.RelatedType, RelatedId = x.RelatedId, Title = x.Title, SignerName = x.SignerName, SignerEmail = x.SignerEmail,
                Status = SignatureRules.Effective(x.Status, x.ExpiresAtUtc, now), ExpiresAtUtc = x.ExpiresAtUtc, CreatedAtUtc = x.CreatedAtUtc, ViewedAtUtc = x.ViewedAtUtc,
                SignedAtUtc = x.SignedAtUtc, SignedName = x.SignedName, SignedIp = x.SignedIp, SignedUserAgent = x.SignedUserAgent, DocumentHash = x.DocumentHash,
                DeclineReason = x.DeclineReason, RequestedByName = x.RequestedByName
            }).ToList()
        };
    }
}

public class CreateSignRequestResult { public string? Id { get; init; } public string? Number { get; init; } }

public class CreateSignRequestRequest : IRequest<CreateSignRequestResult>
{
    public string? RelatedType { get; init; }
    public string? RelatedId { get; init; }
    public string? SignerName { get; init; }
    public string? SignerEmail { get; init; }
    public int ExpiryDays { get; init; } = SignatureRules.DefaultExpiryDays;
    public string? BaseUrl { get; set; }
    public string? UserId { get; set; }
    public string? UserName { get; set; }
}

public class CreateSignRequestValidator : AbstractValidator<CreateSignRequestRequest>
{
    public CreateSignRequestValidator()
    {
        RuleFor(x => x.RelatedType).Must(t => t is "Contract" or "Quotation").WithMessage("Only contracts and quotations can be sent for signature.");
        RuleFor(x => x.RelatedId).NotEmpty();
        RuleFor(x => x.SignerName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.SignerEmail).Must(SignatureRules.LooksLikeEmail).WithMessage("Enter a valid e-mail address for the signer.");
        RuleFor(x => x.ExpiryDays).InclusiveBetween(1, 60);
    }
}

public class CreateSignRequestHandler : IRequestHandler<CreateSignRequestRequest, CreateSignRequestResult>
{
    private readonly IQueryContext _context;
    private readonly ICommandRepository<SignRequest> _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly NumberSequenceService _numbers;
    private readonly IEmailService _email;

    public CreateSignRequestHandler(IQueryContext context, ICommandRepository<SignRequest> repository, IUnitOfWork unitOfWork, NumberSequenceService numbers, IEmailService email)
    { _context = context; _repository = repository; _unitOfWork = unitOfWork; _numbers = numbers; _email = email; }

    public async Task<CreateSignRequestResult> Handle(CreateSignRequestRequest request, CancellationToken cancellationToken)
    {
        string relatedId, title, text;
        if (request.RelatedType == "Quotation")
        {
            var q = await _context.Quotation.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.RelatedId && !x.IsDeleted, cancellationToken) ?? throw new Exception("Quotation not found.");
            var blocker = SignatureRules.QuotationBlocker(q);
            if (blocker != null) throw new Exception(blocker);
            var qCustomer = q.CustomerId == null ? null : await _context.Customer.AsNoTracking().Where(x => x.Id == q.CustomerId).Select(x => x.Name).FirstOrDefaultAsync(cancellationToken);
            var rows = await _context.QuotationItem.AsNoTracking().Where(x => x.QuotationId == q.Id && !x.IsDeleted)
                .Select(x => new { Product = x.Product != null ? x.Product.Name : null, x.Summary, x.Quantity, x.UnitPrice, x.Total }).ToListAsync(cancellationToken);
            text = SignatureRules.QuotationText(q, qCustomer, rows.Select(r => (r.Product, r.Summary, r.Quantity ?? 0, r.UnitPrice ?? 0, r.Total ?? 0)));
            relatedId = q.Id; title = $"Quotation {q.Number}";
        }
        else
        {
            var contract = await _context.Contract.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.RelatedId && !x.IsDeleted, cancellationToken) ?? throw new Exception("Contract not found.");
            if (contract.Status == "Cancelled") throw new Exception("A cancelled contract cannot be sent for signature.");
            var customerName = contract.CustomerId == null ? null : await _context.Customer.AsNoTracking().Where(x => x.Id == contract.CustomerId).Select(x => x.Name).FirstOrDefaultAsync(cancellationToken);
            text = SignatureRules.ContractText(contract, customerName);
            relatedId = contract.Id; title = contract.Title ?? $"Contract {contract.Number}";
        }
        if (text.Length > 8000) throw new Exception("The document text is too long to send for signature. Shorten the notes.");

        // a new request replaces any earlier one that is still waiting for the same contract
        var older = await _repository.GetQuery().Where(x => x.RelatedId == relatedId && !x.IsDeleted && (x.Status == "Sent" || x.Status == "Viewed")).ToListAsync(cancellationToken);
        foreach (var o in older) { o.Status = "Cancelled"; _repository.Update(o); }

        var token = SignatureRules.NewToken();
        var row = new SignRequest
        {
            Number = _numbers.GenerateNumber(nameof(SignRequest), "", "SIG"), RelatedType = request.RelatedType, RelatedId = relatedId, Title = title,
            DocumentText = text, DocumentHash = SignatureRules.Hash(text), SignerName = request.SignerName!.Trim(), SignerEmail = request.SignerEmail!.Trim(),
            TokenHash = SignatureRules.Hash(token), Status = "Sent", ExpiresAtUtc = DateTime.UtcNow.AddDays(request.ExpiryDays),
            RequestedByUserId = request.UserId, RequestedByName = request.UserName, CreatedById = request.UserId
        };

        var link = $"{(request.BaseUrl ?? "").TrimEnd('/')}/Accounts/Sign?token={Uri.EscapeDataString(token)}";
        var result = await _email.SendEmailWithResultAsync(row.SignerEmail!, $"Please review and sign: {title}",
            $"<p>Hello {WebUtility.HtmlEncode(row.SignerName)},</p><p>{WebUtility.HtmlEncode(request.UserName ?? "Our team")} has asked you to review and sign <strong>{WebUtility.HtmlEncode(title)}</strong>.</p>" +
            $"<p><a href=\"{link}\">Open the document</a></p><p>The link works until {row.ExpiresAtUtc:dd MMM yyyy}. You will confirm with a one-time code sent to this e-mail address.</p>");
        if (!result.Success) throw new Exception("The e-mail could not be sent: " + (result.Error ?? "unknown error"));

        await _repository.CreateAsync(row, cancellationToken);
        await _unitOfWork.SaveAsync(cancellationToken);
        return new CreateSignRequestResult { Id = row.Id, Number = row.Number };
    }
}

public class CancelSignRequestRequest : IRequest<bool> { public string? Id { get; init; } public string? UserId { get; set; } }

public class CancelSignRequestHandler : IRequestHandler<CancelSignRequestRequest, bool>
{
    private readonly ICommandRepository<SignRequest> _repository;
    private readonly IUnitOfWork _unitOfWork;
    public CancelSignRequestHandler(ICommandRepository<SignRequest> repository, IUnitOfWork unitOfWork) { _repository = repository; _unitOfWork = unitOfWork; }

    public async Task<bool> Handle(CancelSignRequestRequest request, CancellationToken cancellationToken)
    {
        var r = await _repository.GetAsync(request.Id ?? "", cancellationToken) ?? throw new Exception("Request not found.");
        if (r.Status is "Signed" or "Declined") throw new Exception("A finished request cannot be cancelled.");
        r.Status = "Cancelled"; r.UpdatedById = request.UserId;
        _repository.Update(r);
        await _unitOfWork.SaveAsync(cancellationToken);
        return true;
    }
}

// ---------- signer side (anonymous, by token) ----------

internal static class SignLookup
{
    public static async Task<SignRequest> ByTokenAsync(ICommandRepository<SignRequest> repo, string? token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 100) throw new UnauthorizedAccessException("Link not valid.");
        var hash = SignatureRules.Hash(token);
        return await repo.GetQuery().FirstOrDefaultAsync(x => x.TokenHash == hash && !x.IsDeleted, ct) ?? throw new UnauthorizedAccessException("Link not valid.");
    }
}

public class SigningViewResult
{
    public string? Title { get; init; }
    public string? DocumentText { get; init; }
    public string? SignerName { get; init; }
    public string? SignerEmailMasked { get; init; }
    public string? Status { get; init; }
    public DateTime? ExpiresAtUtc { get; init; }
    public DateTime? SignedAtUtc { get; init; }
    public string? RequestedByName { get; init; }
}

public class GetSigningViewRequest : IRequest<SigningViewResult> { public string? Token { get; set; } }

public class GetSigningViewHandler : IRequestHandler<GetSigningViewRequest, SigningViewResult>
{
    private readonly ICommandRepository<SignRequest> _repository;
    private readonly IUnitOfWork _unitOfWork;
    public GetSigningViewHandler(ICommandRepository<SignRequest> repository, IUnitOfWork unitOfWork) { _repository = repository; _unitOfWork = unitOfWork; }

    public static string Mask(string? email)
    {
        if (string.IsNullOrEmpty(email) || !email.Contains('@')) return "";
        var at = email.IndexOf('@');
        return email[0] + new string('*', Math.Max(1, at - 1)) + email[at..];
    }

    public async Task<SigningViewResult> Handle(GetSigningViewRequest request, CancellationToken cancellationToken)
    {
        var r = await SignLookup.ByTokenAsync(_repository, request.Token, cancellationToken);
        var status = SignatureRules.Effective(r.Status, r.ExpiresAtUtc, DateTime.UtcNow);
        if (r.Status == "Sent" && status == "Sent") { r.Status = "Viewed"; r.ViewedAtUtc = DateTime.UtcNow; _repository.Update(r); await _unitOfWork.SaveAsync(cancellationToken); status = "Viewed"; }
        return new SigningViewResult
        {
            Title = r.Title, DocumentText = r.DocumentText, SignerName = r.SignerName, SignerEmailMasked = Mask(r.SignerEmail), Status = status,
            ExpiresAtUtc = r.ExpiresAtUtc, SignedAtUtc = r.SignedAtUtc, RequestedByName = r.RequestedByName
        };
    }
}

public class SendSigningCodeRequest : IRequest<bool> { public string? Token { get; set; } }

public class SendSigningCodeHandler : IRequestHandler<SendSigningCodeRequest, bool>
{
    private readonly ICommandRepository<SignRequest> _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailService _email;
    public SendSigningCodeHandler(ICommandRepository<SignRequest> repository, IUnitOfWork unitOfWork, IEmailService email) { _repository = repository; _unitOfWork = unitOfWork; _email = email; }

    public async Task<bool> Handle(SendSigningCodeRequest request, CancellationToken cancellationToken)
    {
        var r = await SignLookup.ByTokenAsync(_repository, request.Token, cancellationToken);
        if (!SignatureRules.CanSign(r.Status, r.ExpiresAtUtc, DateTime.UtcNow)) throw new Exception("This request can no longer be signed.");
        if (r.OtpSent >= SignatureRules.MaxOtpPerRequest) throw new Exception("Too many codes were requested. Ask the sender for a new link.");

        var otp = SignatureRules.NewOtp();
        r.OtpHash = SignatureRules.OtpHash(r.Id, otp);
        r.OtpExpiresAtUtc = DateTime.UtcNow.Add(SignatureRules.OtpLifetime);
        r.OtpAttempts = 0;
        r.OtpSent++;
        _repository.Update(r);
        await _unitOfWork.SaveAsync(cancellationToken);

        var sent = await _email.SendEmailWithResultAsync(r.SignerEmail!, "Your signing code",
            $"<p>Your code to sign <strong>{WebUtility.HtmlEncode(r.Title ?? "the document")}</strong> is:</p><p style=\"font-size:24px;letter-spacing:4px\"><strong>{otp}</strong></p><p>It works for 10 minutes. If you did not expect this, ignore this e-mail.</p>");
        if (!sent.Success) throw new Exception("The code could not be e-mailed. Please try again.");
        return true;
    }
}

public class SignDocumentRequest : IRequest<bool>
{
    public string? Token { get; set; }
    public string? Code { get; init; }
    public string? TypedName { get; init; }
    public bool Agree { get; init; }
    public string? Ip { get; set; }
    public string? UserAgent { get; set; }
}

public class SignDocumentValidator : AbstractValidator<SignDocumentRequest>
{
    public SignDocumentValidator()
    {
        RuleFor(x => x.Code).NotEmpty().Length(6).WithMessage("Enter the 6-digit code from your e-mail.");
        RuleFor(x => x.TypedName).NotEmpty().MaximumLength(200).WithMessage("Type your full name to sign.");
        RuleFor(x => x.Agree).Equal(true).WithMessage("Please confirm that you agree.");
    }
}

public class SignDocumentHandler : IRequestHandler<SignDocumentRequest, bool>
{
    private readonly ICommandRepository<SignRequest> _repository;
    private readonly ICommandRepository<Contract> _contracts;
    private readonly ICommandRepository<Quotation> _quotations;
    private readonly ICommandRepository<Notification> _notifications;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailService _email;

    public SignDocumentHandler(ICommandRepository<SignRequest> repository, ICommandRepository<Contract> contracts, ICommandRepository<Quotation> quotations, ICommandRepository<Notification> notifications, IUnitOfWork unitOfWork, IEmailService email)
    { _repository = repository; _contracts = contracts; _quotations = quotations; _notifications = notifications; _unitOfWork = unitOfWork; _email = email; }

    public async Task<bool> Handle(SignDocumentRequest request, CancellationToken cancellationToken)
    {
        var r = await SignLookup.ByTokenAsync(_repository, request.Token, cancellationToken);
        var now = DateTime.UtcNow;
        if (!SignatureRules.CanSign(r.Status, r.ExpiresAtUtc, now)) throw new Exception("This request can no longer be signed.");
        if (!SignatureRules.OtpUsable(r.OtpHash, r.OtpExpiresAtUtc, r.OtpAttempts, now)) throw new Exception("The code has expired or was used too many times. Ask for a new code.");

        if (!SignatureRules.FixedEquals(SignatureRules.OtpHash(r.Id, request.Code ?? ""), r.OtpHash!))
        {
            r.OtpAttempts++;
            _repository.Update(r);
            await _unitOfWork.SaveAsync(cancellationToken);
            throw new Exception("That code is not right.");
        }
        if (SignatureRules.Hash(r.DocumentText ?? "") != r.DocumentHash) throw new Exception("The document no longer matches what was sent. Ask the sender for a new request.");

        r.Status = "Signed"; r.SignedAtUtc = now; r.SignedName = request.TypedName!.Trim();
        r.SignedIp = request.Ip is { Length: > 60 } ? request.Ip[..60] : request.Ip;
        r.SignedUserAgent = request.UserAgent is { Length: > 300 } ? request.UserAgent[..300] : request.UserAgent;
        r.OtpHash = null; r.OtpExpiresAtUtc = null;
        _repository.Update(r);

        if (r.RelatedType == "Contract" && !string.IsNullOrEmpty(r.RelatedId))
        {
            var c = await _contracts.GetAsync(r.RelatedId, cancellationToken);
            if (c != null && c.Status == "Draft") { c.Status = "Active"; _contracts.Update(c); }
        }
        if (r.RelatedType == "Quotation" && !string.IsNullOrEmpty(r.RelatedId))
        {
            var q = await _quotations.GetAsync(r.RelatedId, cancellationToken);
            if (q != null && q.QuotationStatus is null or Domain.Enums.QuotationStatus.Draft or Domain.Enums.QuotationStatus.Sent) { q.QuotationStatus = Domain.Enums.QuotationStatus.Accepted; _quotations.Update(q); }
        }
        await _notifications.CreateAsync(new Notification
        {
            Title = "Document signed", Message = $"{r.SignedName} signed {r.Title}", Type = "Info", Url = "/Signatures/SignatureList", UserId = r.RequestedByUserId, DedupKey = "signed:" + r.Id
        }, cancellationToken);
        await _unitOfWork.SaveAsync(cancellationToken);

        await _email.SendEmailAsync(r.SignerEmail!, $"Signed: {r.Title}",
            $"<p>Thank you, {WebUtility.HtmlEncode(r.SignedName)}.</p><p>You signed <strong>{WebUtility.HtmlEncode(r.Title ?? "the document")}</strong> on {now:dd MMM yyyy HH:mm} UTC.</p>" +
            $"<p>Document fingerprint (SHA-256): <code>{r.DocumentHash}</code></p><hr/><pre style=\"white-space:pre-wrap;font-family:inherit\">{WebUtility.HtmlEncode(r.DocumentText)}</pre>");
        return true;
    }
}

public class DeclineDocumentRequest : IRequest<bool>
{
    public string? Token { get; set; }
    public string? Reason { get; init; }
}

public class DeclineDocumentValidator : AbstractValidator<DeclineDocumentRequest>
{
    public DeclineDocumentValidator() { RuleFor(x => x.Reason).MaximumLength(500); }
}

public class DeclineDocumentHandler : IRequestHandler<DeclineDocumentRequest, bool>
{
    private readonly ICommandRepository<SignRequest> _repository;
    private readonly ICommandRepository<Notification> _notifications;
    private readonly IUnitOfWork _unitOfWork;
    public DeclineDocumentHandler(ICommandRepository<SignRequest> repository, ICommandRepository<Notification> notifications, IUnitOfWork unitOfWork) { _repository = repository; _notifications = notifications; _unitOfWork = unitOfWork; }

    public async Task<bool> Handle(DeclineDocumentRequest request, CancellationToken cancellationToken)
    {
        var r = await SignLookup.ByTokenAsync(_repository, request.Token, cancellationToken);
        if (!SignatureRules.CanSign(r.Status, r.ExpiresAtUtc, DateTime.UtcNow)) throw new Exception("This request can no longer be changed.");
        r.Status = "Declined"; r.DeclineReason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        _repository.Update(r);
        await _notifications.CreateAsync(new Notification
        {
            Title = "Signature declined", Message = $"{r.SignerName} declined {r.Title}", Type = "Warning", Url = "/Signatures/SignatureList", UserId = r.RequestedByUserId, DedupKey = "declined:" + r.Id
        }, cancellationToken);
        await _unitOfWork.SaveAsync(cancellationToken);
        return true;
    }
}
