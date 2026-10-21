using System.Globalization;
using Application.Common.CQS.Queries;
using Application.Common.Repositories;
using Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.CustomFieldManager;

public static class CustomFieldRules
{
    public static readonly string[] Entities = { "Lead", "Customer", "SupportCase" };
    public static readonly string[] Types = { "Text", "Number", "Date", "Boolean", "Picklist" };

    public static List<string> Choices(string? options) =>
        (options ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct().ToList();

    /// <summary>Returns an error message, or null when the value suits the field.</summary>
    public static string? Validate(CustomFieldDefinition d, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return d.IsRequired ? $"{d.Label} is required." : null;
        }
        if (value.Length > 2000) return $"{d.Label} is too long.";
        switch (d.FieldType)
        {
            case "Number":
                if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _)) return $"{d.Label} must be a number.";
                break;
            case "Date":
                if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) return $"{d.Label} must be a date.";
                break;
            case "Boolean":
                if (value != "true" && value != "false") return $"{d.Label} must be yes or no.";
                break;
            case "Picklist":
                if (!Choices(d.Options).Contains(value)) return $"{d.Label}: choose one of the listed values.";
                break;
        }
        return null;
    }
}

public record CustomFieldDefinitionDto(string? Id, string? Entity, string? Label, string? FieldType, List<string> Options, bool IsRequired, bool IsActive, int SortOrder);

public class GetCustomFieldDefinitionsResult { public List<CustomFieldDefinitionDto> Data { get; init; } = new(); }

public class GetCustomFieldDefinitionsRequest : IRequest<GetCustomFieldDefinitionsResult>
{
    public string? Entity { get; init; }
    public bool ActiveOnly { get; init; }
}

public class GetCustomFieldDefinitionsHandler : IRequestHandler<GetCustomFieldDefinitionsRequest, GetCustomFieldDefinitionsResult>
{
    private readonly IQueryContext _context;
    public GetCustomFieldDefinitionsHandler(IQueryContext context) { _context = context; }

    public async Task<GetCustomFieldDefinitionsResult> Handle(GetCustomFieldDefinitionsRequest request, CancellationToken cancellationToken)
    {
        var q = _context.CustomFieldDefinition.AsNoTracking().Where(x => !x.IsDeleted);
        if (!string.IsNullOrEmpty(request.Entity)) q = q.Where(x => x.Entity == request.Entity);
        if (request.ActiveOnly) q = q.Where(x => x.IsActive);
        var rows = await q.OrderBy(x => x.Entity).ThenBy(x => x.SortOrder).ThenBy(x => x.Label).ToListAsync(cancellationToken);
        return new GetCustomFieldDefinitionsResult
        {
            Data = rows.Select(x => new CustomFieldDefinitionDto(x.Id, x.Entity, x.Label, x.FieldType, CustomFieldRules.Choices(x.Options), x.IsRequired, x.IsActive, x.SortOrder)).ToList()
        };
    }
}

public class SaveCustomFieldDefinitionResult { public string? Id { get; init; } }

public class SaveCustomFieldDefinitionRequest : IRequest<SaveCustomFieldDefinitionResult>
{
    public string? Id { get; init; }
    public string? Entity { get; init; }
    public string? Label { get; init; }
    public string? FieldType { get; init; }
    public string? Options { get; init; }
    public bool IsRequired { get; init; }
    public bool IsActive { get; init; } = true;
    public int SortOrder { get; init; }
    public string? UserId { get; set; }
}

public class SaveCustomFieldDefinitionValidator : AbstractValidator<SaveCustomFieldDefinitionRequest>
{
    public SaveCustomFieldDefinitionValidator()
    {
        RuleFor(x => x.Entity).Must(x => CustomFieldRules.Entities.Contains(x)).WithMessage("Entity must be Lead, Customer or SupportCase.");
        RuleFor(x => x.Label).NotEmpty().MaximumLength(100);
        RuleFor(x => x.FieldType).Must(x => CustomFieldRules.Types.Contains(x)).WithMessage("Unknown field type.");
        RuleFor(x => x.Options).NotEmpty().When(x => x.FieldType == "Picklist").WithMessage("A picklist needs at least one choice.");
    }
}

public class SaveCustomFieldDefinitionHandler : IRequestHandler<SaveCustomFieldDefinitionRequest, SaveCustomFieldDefinitionResult>
{
    private readonly ICommandRepository<CustomFieldDefinition> _repository;
    private readonly IUnitOfWork _unitOfWork;

    public SaveCustomFieldDefinitionHandler(ICommandRepository<CustomFieldDefinition> repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<SaveCustomFieldDefinitionResult> Handle(SaveCustomFieldDefinitionRequest request, CancellationToken cancellationToken)
    {
        CustomFieldDefinition? entity = null;
        if (!string.IsNullOrEmpty(request.Id))
        {
            entity = await _repository.GetAsync(request.Id, cancellationToken) ?? throw new Exception("Field not found.");
        }

        var creating = entity == null;
        entity ??= new CustomFieldDefinition { CreatedById = request.UserId, Entity = request.Entity };

        //>>> entity and type are fixed once values may exist
        if (creating) entity.FieldType = request.FieldType;
        entity.Label = request.Label!.Trim();
        entity.Options = request.FieldType == "Picklist" ? string.Join("\n", CustomFieldRules.Choices(request.Options)) : null;
        entity.IsRequired = request.IsRequired;
        entity.IsActive = request.IsActive;
        entity.SortOrder = request.SortOrder;

        if (creating) await _repository.CreateAsync(entity, cancellationToken);
        else { entity.UpdatedById = request.UserId; _repository.Update(entity); }

        await _unitOfWork.SaveAsync(cancellationToken);
        return new SaveCustomFieldDefinitionResult { Id = entity.Id };
    }
}

public class DeleteCustomFieldDefinitionResult { public bool Deleted { get; init; } }

public class DeleteCustomFieldDefinitionRequest : IRequest<DeleteCustomFieldDefinitionResult>
{
    public string? Id { get; init; }
    public string? UserId { get; set; }
}

public class DeleteCustomFieldDefinitionHandler : IRequestHandler<DeleteCustomFieldDefinitionRequest, DeleteCustomFieldDefinitionResult>
{
    private readonly ICommandRepository<CustomFieldDefinition> _repository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteCustomFieldDefinitionHandler(ICommandRepository<CustomFieldDefinition> repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<DeleteCustomFieldDefinitionResult> Handle(DeleteCustomFieldDefinitionRequest request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetAsync(request.Id ?? "", cancellationToken) ?? throw new Exception("Field not found.");
        entity.UpdatedById = request.UserId;
        _repository.Delete(entity);
        await _unitOfWork.SaveAsync(cancellationToken);
        return new DeleteCustomFieldDefinitionResult { Deleted = true };
    }
}

public record CustomFieldValueDto(string? DefinitionId, string? Label, string? FieldType, List<string> Options, bool IsRequired, string? Value);

public class GetCustomFieldValuesResult { public List<CustomFieldValueDto> Data { get; init; } = new(); }

public class GetCustomFieldValuesRequest : IRequest<GetCustomFieldValuesResult>
{
    public string? Entity { get; init; }
    public string? RecordId { get; init; }
}

public class GetCustomFieldValuesHandler : IRequestHandler<GetCustomFieldValuesRequest, GetCustomFieldValuesResult>
{
    private readonly IQueryContext _context;
    public GetCustomFieldValuesHandler(IQueryContext context) { _context = context; }

    public async Task<GetCustomFieldValuesResult> Handle(GetCustomFieldValuesRequest request, CancellationToken cancellationToken)
    {
        var defs = await _context.CustomFieldDefinition.AsNoTracking()
            .Where(x => !x.IsDeleted && x.IsActive && x.Entity == request.Entity)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Label).ToListAsync(cancellationToken);
        var values = await _context.CustomFieldValue.AsNoTracking()
            .Where(x => !x.IsDeleted && x.Entity == request.Entity && x.RecordId == request.RecordId).ToListAsync(cancellationToken);

        return new GetCustomFieldValuesResult
        {
            Data = defs.Select(d => new CustomFieldValueDto(d.Id, d.Label, d.FieldType, CustomFieldRules.Choices(d.Options), d.IsRequired,
                values.FirstOrDefault(v => v.DefinitionId == d.Id)?.Value)).ToList()
        };
    }
}

public class SaveCustomFieldValuesResult { public int Saved { get; init; } }

public class SaveCustomFieldValuesRequest : IRequest<SaveCustomFieldValuesResult>
{
    public string? Entity { get; init; }
    public string? RecordId { get; init; }

    /// <summary>Definition id to value.</summary>
    public Dictionary<string, string?>? Values { get; init; }
    public string? UserId { get; set; }
}

public class SaveCustomFieldValuesValidator : AbstractValidator<SaveCustomFieldValuesRequest>
{
    public SaveCustomFieldValuesValidator()
    {
        RuleFor(x => x.Entity).Must(x => CustomFieldRules.Entities.Contains(x)).WithMessage("Unknown entity.");
        RuleFor(x => x.RecordId).NotEmpty();
        RuleFor(x => x.Values).NotNull();
    }
}

public class SaveCustomFieldValuesHandler : IRequestHandler<SaveCustomFieldValuesRequest, SaveCustomFieldValuesResult>
{
    private readonly ICommandRepository<CustomFieldValue> _values;
    private readonly IQueryContext _context;
    private readonly IUnitOfWork _unitOfWork;

    public SaveCustomFieldValuesHandler(ICommandRepository<CustomFieldValue> values, IQueryContext context, IUnitOfWork unitOfWork)
    {
        _values = values;
        _context = context;
        _unitOfWork = unitOfWork;
    }

    public async Task<SaveCustomFieldValuesResult> Handle(SaveCustomFieldValuesRequest request, CancellationToken cancellationToken)
    {
        var defs = await _context.CustomFieldDefinition.AsNoTracking()
            .Where(x => !x.IsDeleted && x.IsActive && x.Entity == request.Entity).ToListAsync(cancellationToken);

        //>>> validate everything before writing anything
        var errors = defs.Select(d => CustomFieldRules.Validate(d, request.Values!.GetValueOrDefault(d.Id))).Where(e => e != null).ToList();
        if (errors.Count > 0) throw new Exception(string.Join(" ", errors));

        var existing = await _values.GetQuery()
            .Where(x => !x.IsDeleted && x.Entity == request.Entity && x.RecordId == request.RecordId).ToListAsync(cancellationToken);

        var saved = 0;
        foreach (var d in defs)
        {
            if (!request.Values!.TryGetValue(d.Id, out var value)) continue;
            value = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            var row = existing.FirstOrDefault(x => x.DefinitionId == d.Id);
            if (row == null)
            {
                if (value == null) continue;
                await _values.CreateAsync(new CustomFieldValue { CreatedById = request.UserId, Entity = request.Entity, RecordId = request.RecordId, DefinitionId = d.Id, Value = value }, cancellationToken);
            }
            else if (row.Value != value)
            {
                row.Value = value;
                row.UpdatedById = request.UserId;
                _values.Update(row);
            }
            saved++;
        }

        await _unitOfWork.SaveAsync(cancellationToken);
        return new SaveCustomFieldValuesResult { Saved = saved };
    }
}
