using System.Security.Cryptography;
using System.Text;
using Application.Common.CQS.Queries;
using Application.Common.Repositories;
using Application.Common.Services.IntegrationManager;
using Application.Features.NumberSequenceManager;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.IntegrationManager;

public static class IntegrationRules
{
    public static readonly string[] Events = { "lead.created", "lead.stage_changed", "lead.won", "lead.lost", "customer.created", "case.created" };

    public static string Hash(string key) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();

    public static string NewSecret(int bytes = 32) => Convert.ToBase64String(RandomNumberGenerator.GetBytes(bytes)).Replace('+', '-').Replace('/', '_').TrimEnd('=');
}

// ---------- API keys ----------

public record ApiKeyDto(string? Id, string? Name, string? Prefix, bool IsActive, DateTime? LastUsedAtUtc, DateTime? ExpiresAtUtc, DateTime? CreatedAtUtc);
public class ListApiKeysResult { public List<ApiKeyDto> Data { get; init; } = new(); }
public class ListApiKeysRequest : IRequest<ListApiKeysResult> { }

public class ListApiKeysHandler : IRequestHandler<ListApiKeysRequest, ListApiKeysResult>
{
    private readonly IQueryContext _context;
    public ListApiKeysHandler(IQueryContext context) { _context = context; }

    public async Task<ListApiKeysResult> Handle(ListApiKeysRequest request, CancellationToken cancellationToken)
    {
        var rows = await _context.ApiKey.AsNoTracking().Where(x => !x.IsDeleted).OrderByDescending(x => x.CreatedAtUtc).ToListAsync(cancellationToken);
        return new ListApiKeysResult { Data = rows.Select(x => new ApiKeyDto(x.Id, x.Name, x.Prefix, x.IsActive, x.LastUsedAtUtc, x.ExpiresAtUtc, x.CreatedAtUtc)).ToList() };
    }
}

public class CreateApiKeyResult { public string? Id { get; init; } public string? Key { get; init; } }

public class CreateApiKeyRequest : IRequest<CreateApiKeyResult>
{
    public string? Name { get; init; }
    public int? ExpiresInDays { get; init; }
    public string? UserId { get; set; }
}

public class CreateApiKeyValidator : AbstractValidator<CreateApiKeyRequest>
{
    public CreateApiKeyValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ExpiresInDays).InclusiveBetween(1, 3650).When(x => x.ExpiresInDays != null);
    }
}

/// <summary>The full key is returned once, here. Only its hash is kept.</summary>
public class CreateApiKeyHandler : IRequestHandler<CreateApiKeyRequest, CreateApiKeyResult>
{
    private readonly ICommandRepository<ApiKey> _repository;
    private readonly IUnitOfWork _unitOfWork;
    public CreateApiKeyHandler(ICommandRepository<ApiKey> repository, IUnitOfWork unitOfWork) { _repository = repository; _unitOfWork = unitOfWork; }

    public async Task<CreateApiKeyResult> Handle(CreateApiKeyRequest request, CancellationToken cancellationToken)
    {
        var key = "ik_" + IntegrationRules.NewSecret(30);
        var entity = new ApiKey
        {
            CreatedById = request.UserId,
            Name = request.Name!.Trim(),
            KeyHash = IntegrationRules.Hash(key),
            Prefix = key.Substring(0, 9),
            OwnerUserId = request.UserId,
            ExpiresAtUtc = request.ExpiresInDays == null ? null : DateTime.UtcNow.AddDays(request.ExpiresInDays.Value)
        };
        await _repository.CreateAsync(entity, cancellationToken);
        await _unitOfWork.SaveAsync(cancellationToken);
        return new CreateApiKeyResult { Id = entity.Id, Key = key };
    }
}

public class RevokeApiKeyResult { public bool Revoked { get; init; } }
public class RevokeApiKeyRequest : IRequest<RevokeApiKeyResult> { public string? Id { get; init; } public string? UserId { get; set; } }

public class RevokeApiKeyHandler : IRequestHandler<RevokeApiKeyRequest, RevokeApiKeyResult>
{
    private readonly ICommandRepository<ApiKey> _repository;
    private readonly IUnitOfWork _unitOfWork;
    public RevokeApiKeyHandler(ICommandRepository<ApiKey> repository, IUnitOfWork unitOfWork) { _repository = repository; _unitOfWork = unitOfWork; }

    public async Task<RevokeApiKeyResult> Handle(RevokeApiKeyRequest request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetAsync(request.Id ?? "", cancellationToken) ?? throw new Exception("Key not found.");
        entity.IsActive = false;
        entity.UpdatedById = request.UserId;
        _repository.Update(entity);
        await _unitOfWork.SaveAsync(cancellationToken);
        return new RevokeApiKeyResult { Revoked = true };
    }
}

// ---------- webhooks ----------

public record WebhookDto(string? Id, string? Name, string? Url, string? Secret, string? Events, bool IsActive);
public class ListWebhooksResult { public List<WebhookDto> Data { get; init; } = new(); public string[] AvailableEvents { get; init; } = IntegrationRules.Events; }
public class ListWebhooksRequest : IRequest<ListWebhooksResult> { }

public class ListWebhooksHandler : IRequestHandler<ListWebhooksRequest, ListWebhooksResult>
{
    private readonly IQueryContext _context;
    public ListWebhooksHandler(IQueryContext context) { _context = context; }

    public async Task<ListWebhooksResult> Handle(ListWebhooksRequest request, CancellationToken cancellationToken)
    {
        var rows = await _context.WebhookEndpoint.AsNoTracking().Where(x => !x.IsDeleted).OrderBy(x => x.Name).ToListAsync(cancellationToken);
        return new ListWebhooksResult { Data = rows.Select(x => new WebhookDto(x.Id, x.Name, x.Url, x.Secret, x.Events, x.IsActive)).ToList() };
    }
}

public class SaveWebhookResult { public string? Id { get; init; } }

public class SaveWebhookRequest : IRequest<SaveWebhookResult>
{
    public string? Id { get; init; }
    public string? Name { get; init; }
    public string? Url { get; init; }
    public string? Events { get; init; }
    public bool IsActive { get; init; } = true;
    public bool RotateSecret { get; init; }
    public string? UserId { get; set; }
}

public class SaveWebhookValidator : AbstractValidator<SaveWebhookRequest>
{
    public SaveWebhookValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Url).NotEmpty().MaximumLength(1000)
            .Must(u => Uri.TryCreate(u, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
            .WithMessage("Enter a full http(s) address.");
        RuleFor(x => x.Events).NotEmpty().WithMessage("Choose at least one event.");
    }
}

public class SaveWebhookHandler : IRequestHandler<SaveWebhookRequest, SaveWebhookResult>
{
    private readonly ICommandRepository<WebhookEndpoint> _repository;
    private readonly IUnitOfWork _unitOfWork;
    public SaveWebhookHandler(ICommandRepository<WebhookEndpoint> repository, IUnitOfWork unitOfWork) { _repository = repository; _unitOfWork = unitOfWork; }

    public async Task<SaveWebhookResult> Handle(SaveWebhookRequest request, CancellationToken cancellationToken)
    {
        WebhookEndpoint? entity = null;
        if (!string.IsNullOrEmpty(request.Id))
        {
            entity = await _repository.GetAsync(request.Id, cancellationToken) ?? throw new Exception("Webhook not found.");
        }
        var creating = entity == null;
        entity ??= new WebhookEndpoint { CreatedById = request.UserId, Secret = "whsec_" + IntegrationRules.NewSecret() };

        var chosen = request.Events!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(e => e == "*" || IntegrationRules.Events.Contains(e)).Distinct().ToList();
        if (chosen.Count == 0) throw new Exception("Choose at least one valid event.");

        entity.Name = request.Name!.Trim();
        entity.Url = request.Url!.Trim();
        entity.Events = string.Join(",", chosen);
        entity.IsActive = request.IsActive;
        if (!creating && request.RotateSecret) entity.Secret = "whsec_" + IntegrationRules.NewSecret();

        if (creating) await _repository.CreateAsync(entity, cancellationToken);
        else { entity.UpdatedById = request.UserId; _repository.Update(entity); }
        await _unitOfWork.SaveAsync(cancellationToken);
        return new SaveWebhookResult { Id = entity.Id };
    }
}

public class DeleteWebhookResult { public bool Deleted { get; init; } }
public class DeleteWebhookRequest : IRequest<DeleteWebhookResult> { public string? Id { get; init; } public string? UserId { get; set; } }

public class DeleteWebhookHandler : IRequestHandler<DeleteWebhookRequest, DeleteWebhookResult>
{
    private readonly ICommandRepository<WebhookEndpoint> _repository;
    private readonly IUnitOfWork _unitOfWork;
    public DeleteWebhookHandler(ICommandRepository<WebhookEndpoint> repository, IUnitOfWork unitOfWork) { _repository = repository; _unitOfWork = unitOfWork; }

    public async Task<DeleteWebhookResult> Handle(DeleteWebhookRequest request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetAsync(request.Id ?? "", cancellationToken) ?? throw new Exception("Webhook not found.");
        entity.UpdatedById = request.UserId;
        _repository.Delete(entity);
        await _unitOfWork.SaveAsync(cancellationToken);
        return new DeleteWebhookResult { Deleted = true };
    }
}

public record DeliveryDto(string? Id, string? EndpointName, string? EventName, bool Success, int? HttpStatus, int Attempts, string? Error, DateTime DeliveredAtUtc);
public class ListDeliveriesResult { public List<DeliveryDto> Data { get; init; } = new(); }
public class ListDeliveriesRequest : IRequest<ListDeliveriesResult> { public string? EndpointId { get; init; } }

public class ListDeliveriesHandler : IRequestHandler<ListDeliveriesRequest, ListDeliveriesResult>
{
    private readonly IQueryContext _context;
    public ListDeliveriesHandler(IQueryContext context) { _context = context; }

    public async Task<ListDeliveriesResult> Handle(ListDeliveriesRequest request, CancellationToken cancellationToken)
    {
        var q = _context.WebhookDelivery.AsNoTracking().Where(x => !x.IsDeleted);
        if (!string.IsNullOrEmpty(request.EndpointId)) q = q.Where(x => x.EndpointId == request.EndpointId);
        var rows = await q.OrderByDescending(x => x.DeliveredAtUtc).Take(100).ToListAsync(cancellationToken);
        return new ListDeliveriesResult { Data = rows.Select(x => new DeliveryDto(x.Id, x.EndpointName, x.EventName, x.Success, x.HttpStatus, x.Attempts, x.Error, x.DeliveredAtUtc)).ToList() };
    }
}

public class TestWebhookResult { public bool Queued { get; init; } }
public class TestWebhookRequest : IRequest<TestWebhookResult> { public string? Id { get; init; } }

public class TestWebhookHandler : IRequestHandler<TestWebhookRequest, TestWebhookResult>
{
    private readonly IWebhookPublisher _publisher;
    public TestWebhookHandler(IWebhookPublisher publisher) { _publisher = publisher; }

    public Task<TestWebhookResult> Handle(TestWebhookRequest request, CancellationToken cancellationToken)
    {
        _publisher.Publish(new WebhookEvent("test.ping", new { message = "Hello from Interlace CRM" }, request.Id));
        return Task.FromResult(new TestWebhookResult { Queued = true });
    }
}

// ---------- public lead capture ----------

public class CaptureLeadResult { public string? LeadNumber { get; init; } }

public class CaptureLeadRequest : IRequest<CaptureLeadResult>
{
    public string? ApiKey { get; set; }
    public string? Name { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string? Company { get; init; }
    public string? Message { get; init; }
    public string? Source { get; init; }
    public bool? Consent { get; init; }
}

public class CaptureLeadValidator : AbstractValidator<CaptureLeadRequest>
{
    public CaptureLeadValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).EmailAddress().MaximumLength(100).When(x => !string.IsNullOrEmpty(x.Email));
        RuleFor(x => x).Must(x => !string.IsNullOrWhiteSpace(x.Email) || !string.IsNullOrWhiteSpace(x.Phone)).WithMessage("Provide an email or a phone number.");
        RuleFor(x => x.Phone).MaximumLength(50);
        RuleFor(x => x.Company).MaximumLength(100);
        RuleFor(x => x.Message).MaximumLength(2000);
        RuleFor(x => x.Source).MaximumLength(100);
    }
}

public class CaptureLeadHandler : IRequestHandler<CaptureLeadRequest, CaptureLeadResult>
{
    private readonly ICommandRepository<ApiKey> _keys;
    private readonly ICommandRepository<Lead> _leads;
    private readonly IUnitOfWork _unitOfWork;
    private readonly NumberSequenceService _numbers;

    public CaptureLeadHandler(ICommandRepository<ApiKey> keys, ICommandRepository<Lead> leads, IUnitOfWork unitOfWork, NumberSequenceService numbers)
    {
        _keys = keys;
        _leads = leads;
        _unitOfWork = unitOfWork;
        _numbers = numbers;
    }

    public async Task<CaptureLeadResult> Handle(CaptureLeadRequest request, CancellationToken cancellationToken)
    {
        var hash = IntegrationRules.Hash(request.ApiKey ?? "");
        var now = DateTime.UtcNow;
        var key = await _keys.GetQuery().FirstOrDefaultAsync(x => !x.IsDeleted && x.IsActive && x.KeyHash == hash, cancellationToken);
        if (key == null || (key.ExpiresAtUtc != null && key.ExpiresAtUtc < now))
        {
            throw new UnauthorizedAccessException("Invalid API key.");
        }

        key.LastUsedAtUtc = now;
        _keys.Update(key);

        var lead = new Lead
        {
            CreatedById = key.OwnerUserId,
            OwnerUserId = key.OwnerUserId,
            Number = _numbers.GenerateNumber(nameof(Lead), "", "LEA"),
            Title = $"Web enquiry: {request.Name!.Trim()}",
            Description = request.Message,
            CompanyName = string.IsNullOrWhiteSpace(request.Company) ? request.Name!.Trim() : request.Company.Trim(),
            CompanyEmail = request.Email?.Trim(),
            CompanyPhoneNumber = request.Phone?.Trim(),
            LeadSource = string.IsNullOrWhiteSpace(request.Source) ? "Web form" : request.Source.Trim(),
            ConsentMarketing = request.Consent,
            ConsentUpdatedAtUtc = request.Consent == null ? null : now,
            DateProspecting = now,
            DateClosingEstimation = now.AddDays(30),
            PipelineStage = PipelineStage.Prospecting,
            ClosingStatus = ClosingStatus.OnProgress
        };
        await _leads.CreateAsync(lead, cancellationToken);
        await _unitOfWork.SaveAsync(cancellationToken);
        return new CaptureLeadResult { LeadNumber = lead.Number };
    }
}
