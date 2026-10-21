using Application.Common.Repositories;
using Application.Features.LeadManager.Commands;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.BulkManager;

public class BulkUpdateResult
{
    public int Updated { get; init; }
}

public class BulkUpdateRequest : IRequest<BulkUpdateResult>
{
    /// <summary>Lead or Customer.</summary>
    public string? Entity { get; init; }
    public List<string>? Ids { get; init; }

    /// <summary>SetOwner, AddTag, RemoveTag, SetSource, SetConsent, SetStage (leads) or SetLifecycle (customers).</summary>
    public string? Action { get; init; }
    public string? Value { get; init; }
    public string? UserId { get; set; }
}

public class BulkUpdateValidator : AbstractValidator<BulkUpdateRequest>
{
    public BulkUpdateValidator()
    {
        RuleFor(x => x.Entity).Must(x => x == "Lead" || x == "Customer").WithMessage("Entity must be Lead or Customer.");
        RuleFor(x => x.Ids).NotEmpty().Must(x => x!.Count <= 500).WithMessage("Select at most 500 records.");
        RuleFor(x => x.Action).NotEmpty();
    }
}

public class BulkUpdateHandler : IRequestHandler<BulkUpdateRequest, BulkUpdateResult>
{
    private readonly ICommandRepository<Lead> _leads;
    private readonly ICommandRepository<Customer> _customers;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISender _sender;

    public BulkUpdateHandler(ICommandRepository<Lead> leads, ICommandRepository<Customer> customers, IUnitOfWork unitOfWork, ISender sender)
    {
        _leads = leads;
        _customers = customers;
        _unitOfWork = unitOfWork;
        _sender = sender;
    }

    public async Task<BulkUpdateResult> Handle(BulkUpdateRequest request, CancellationToken cancellationToken)
    {
        var ids = request.Ids!.Distinct().ToList();
        var value = request.Value?.Trim();
        var action = request.Action!;
        var count = 0;

        if (request.Entity == "Lead")
        {
            if (action == "SetStage")
            {
                //>>> reuse the pipeline rules (won / lost / re-open) of a single move
                foreach (var id in ids)
                {
                    await _sender.Send(new MoveLeadStageRequest { LeadId = id, Target = value, UpdatedById = request.UserId }, cancellationToken);
                    count++;
                }
                return new BulkUpdateResult { Updated = count };
            }

            foreach (var x in await _leads.GetQuery().Where(x => ids.Contains(x.Id) && !x.IsDeleted).ToListAsync(cancellationToken))
            {
                switch (action)
                {
                    case "SetOwner": Require(value); x.OwnerUserId = value; break;
                    case "AddTag": Require(value); x.Tags = Tags.Add(x.Tags, value!); break;
                    case "RemoveTag": Require(value); x.Tags = Tags.Remove(x.Tags, value!); break;
                    case "SetSource": Require(value); x.LeadSource = value; break;
                    case "SetConsent": x.ConsentMarketing = Consent(value); x.ConsentUpdatedAtUtc = DateTime.UtcNow; break;
                    default: throw new Exception($"Unknown action: {action}");
                }
                x.UpdatedById = request.UserId;
                _leads.Update(x);
                count++;
            }
        }
        else
        {
            foreach (var x in await _customers.GetQuery().Where(x => ids.Contains(x.Id) && !x.IsDeleted).ToListAsync(cancellationToken))
            {
                switch (action)
                {
                    case "SetOwner": Require(value); x.OwnerUserId = value; break;
                    case "AddTag": Require(value); x.Tags = Tags.Add(x.Tags, value!); break;
                    case "RemoveTag": Require(value); x.Tags = Tags.Remove(x.Tags, value!); break;
                    case "SetSource": Require(value); x.Source = value; break;
                    case "SetConsent": x.ConsentMarketing = Consent(value); x.ConsentUpdatedAtUtc = DateTime.UtcNow; break;
                    case "SetLifecycle":
                        if (!Enum.TryParse<LifecycleStage>(value, true, out var stage)) throw new Exception($"Unknown lifecycle stage: {value}");
                        x.LifecycleStage = stage;
                        break;
                    default: throw new Exception($"Unknown action: {action}");
                }
                x.UpdatedById = request.UserId;
                _customers.Update(x);
                count++;
            }
        }

        await _unitOfWork.SaveAsync(cancellationToken);
        return new BulkUpdateResult { Updated = count };
    }

    private static void Require(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new Exception("A value is required for this action.");
    }

    private static bool? Consent(string? value) => value?.ToLowerInvariant() switch
    {
        "yes" or "true" or "agreed" => true,
        "no" or "false" or "opted out" => false,
        _ => null
    };
}

public static class Tags
{
    private static List<string> Split(string? tags) =>
        (tags ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    public static string? Add(string? tags, string tag)
    {
        var list = Split(tags);
        if (!list.Contains(tag, StringComparer.OrdinalIgnoreCase)) list.Add(tag.Trim());
        return string.Join(", ", list);
    }

    public static string? Remove(string? tags, string tag)
    {
        var list = Split(tags).Where(x => !string.Equals(x, tag.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        return list.Count == 0 ? null : string.Join(", ", list);
    }
}
