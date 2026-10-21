using Application.Common.CQS.Queries;
using Application.Common.Repositories;
using Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.ReportManager;

public record ReportEntityDto(string Key, string Label, List<ReportField> Fields);
public class GetReportCatalogResult { public List<ReportEntityDto> Entities { get; init; } = new(); public string[] Operators { get; init; } = ReportEngine.Operators; }
public class GetReportCatalogRequest : IRequest<GetReportCatalogResult> { public List<string>? AllowedEntities { get; set; } }

public class GetReportCatalogHandler : IRequestHandler<GetReportCatalogRequest, GetReportCatalogResult>
{
    public Task<GetReportCatalogResult> Handle(GetReportCatalogRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(new GetReportCatalogResult
        {
            Entities = ReportCatalog.Fields.Where(e => request.AllowedEntities == null || request.AllowedEntities.Contains(e.Key))
                .Select(e => new ReportEntityDto(e.Key, ReportCatalog.Labels[e.Key], e.Value)).ToList()
        });
}

public class ReportColumnDto { public string Key { get; init; } = ""; public string Label { get; init; } = ""; public string Type { get; init; } = "text"; }

public class RunReportResult
{
    public List<ReportColumnDto> Columns { get; init; } = new();
    public List<List<object?>> Rows { get; init; } = new();
    public int Total { get; init; }
    public bool Truncated { get; init; }
    public bool Grouped { get; init; }
}

public class RunReportRequest : IRequest<RunReportResult>
{
    public string? Entity { get; init; }
    public List<string>? Columns { get; init; }
    public List<ReportFilter>? Filters { get; init; }

    /// <summary>Field to group by. Dates group by month. Empty for a plain list.</summary>
    public string? GroupBy { get; init; }

    /// <summary>count, sum:field or avg:field. Used with GroupBy.</summary>
    public string? Metric { get; init; }
    public string? SortBy { get; init; }
    public bool SortDescending { get; init; }
    public int? Limit { get; init; }
}

public class RunReportValidator : AbstractValidator<RunReportRequest>
{
    public RunReportValidator()
    {
        RuleFor(x => x.Entity).Must(e => ReportCatalog.Fields.ContainsKey(e ?? "")).WithMessage("Unknown record type.");
        RuleFor(x => x).Custom((x, ctx) =>
        {
            if (!ReportCatalog.Fields.TryGetValue(x.Entity ?? "", out var fields)) return;
            var keys = fields.Select(f => f.Key).ToHashSet();
            foreach (var c in x.Columns ?? new()) if (!keys.Contains(c)) ctx.AddFailure($"Unknown column: {c}");
            foreach (var f in x.Filters ?? new())
            {
                if (!keys.Contains(f.Field)) ctx.AddFailure($"Unknown filter field: {f.Field}");
                if (!ReportEngine.Operators.Contains(f.Op)) ctx.AddFailure($"Unknown operator: {f.Op}");
            }
            if (!string.IsNullOrEmpty(x.GroupBy) && !keys.Contains(x.GroupBy)) ctx.AddFailure("Unknown group field.");
            if (!string.IsNullOrEmpty(x.SortBy) && !keys.Contains(x.SortBy)) ctx.AddFailure("Unknown sort field.");
            var metric = x.Metric ?? "count";
            if (metric != "count")
            {
                var parts = metric.Split(':', 2);
                var f = parts.Length == 2 ? fields.FirstOrDefault(i => i.Key == parts[1]) : null;
                if (f == null || f.Type != "number" || !new[] { "sum", "avg", "min", "max" }.Contains(parts[0])) ctx.AddFailure("Choose a numeric field for sum, average, min or max.");
            }
        });
    }
}

public class RunReportHandler : IRequestHandler<RunReportRequest, RunReportResult>
{
    private readonly IQueryContext _context;
    public RunReportHandler(IQueryContext context) { _context = context; }

    public async Task<RunReportResult> Handle(RunReportRequest request, CancellationToken cancellationToken)
    {
        var entity = request.Entity!;
        var fields = ReportCatalog.Fields[entity];
        string FieldType(string key) => fields.First(f => f.Key == key).Type;

        var rows = await ReportCatalog.FetchAsync(_context, entity, cancellationToken);
        var truncated = rows.Count >= ReportEngine.MaxRows;

        foreach (var f in request.Filters ?? new())
        {
            var type = FieldType(f.Field);
            rows = rows.Where(r => ReportEngine.Matches(r, f, type)).ToList();
        }

        var limit = Math.Clamp(request.Limit ?? 1000, 1, 5000);

        if (!string.IsNullOrEmpty(request.GroupBy))
        {
            var gf = fields.First(f => f.Key == request.GroupBy);
            var metric = string.IsNullOrWhiteSpace(request.Metric) ? "count" : request.Metric;
            var groups = rows.GroupBy(r => ReportEngine.GroupKey(r.GetValueOrDefault(gf.Key), gf.Type))
                .Select(g => new { Key = g.Key, Value = ReportEngine.Aggregate(g.Select(x => (IReadOnlyDictionary<string, object?>)x), metric), Count = g.Count() })
                .ToList();

            var ordered = gf.Type == "date" ? groups.OrderBy(g => g.Key).ToList() : groups.OrderByDescending(g => g.Value).ToList();
            var metricLabel = metric == "count" ? "Count" : $"{metric.Split(':')[0]} of {fields.First(f => f.Key == metric.Split(':')[1]).Label}";

            return new RunReportResult
            {
                Grouped = true,
                Total = rows.Count,
                Truncated = truncated,
                Columns = new()
                {
                    new ReportColumnDto { Key = gf.Key, Label = gf.Type == "date" ? gf.Label + " (month)" : gf.Label, Type = gf.Type },
                    new ReportColumnDto { Key = "value", Label = metricLabel, Type = "number" }
                },
                Rows = ordered.Take(limit).Select(g => new List<object?> { g.Key, g.Value }).ToList()
            };
        }

        var columns = (request.Columns is { Count: > 0 } ? request.Columns : fields.Take(6).Select(f => f.Key).ToList())
            .Select(k => fields.First(f => f.Key == k)).ToList();

        if (!string.IsNullOrEmpty(request.SortBy))
        {
            var sf = fields.First(f => f.Key == request.SortBy);
            object? Key(Dictionary<string, object?> r) => sf.Type == "number" ? ReportEngine.Number(r.GetValueOrDefault(sf.Key)) ?? double.MinValue
                : sf.Type == "date" ? (r.GetValueOrDefault(sf.Key) as DateTime? ?? DateTime.MinValue)
                : ReportEngine.Text(r.GetValueOrDefault(sf.Key), sf.Type).ToLowerInvariant();
            rows = request.SortDescending ? rows.OrderByDescending(Key).ToList() : rows.OrderBy(Key).ToList();
        }

        return new RunReportResult
        {
            Total = rows.Count,
            Truncated = truncated,
            Columns = columns.Select(c => new ReportColumnDto { Key = c.Key, Label = c.Label, Type = c.Type }).ToList(),
            Rows = rows.Take(limit).Select(r => columns.Select(c => c.Type == "number" ? (object?)ReportEngine.Number(r.GetValueOrDefault(c.Key)) : ReportEngine.Text(r.GetValueOrDefault(c.Key), c.Type)).ToList()).ToList()
        };
    }
}

// ---------- saved reports ----------

public record ReportDto(string? Id, string? Name, string? Entity, string? DefinitionJson, bool IsShared, bool IsMine, string? OwnerName);
public class ListReportsResult { public List<ReportDto> Data { get; init; } = new(); }
public class ListReportsRequest : IRequest<ListReportsResult> { public string? UserId { get; set; } public List<string>? AllowedEntities { get; set; } }

public class ListReportsHandler : IRequestHandler<ListReportsRequest, ListReportsResult>
{
    private readonly IQueryContext _context;
    public ListReportsHandler(IQueryContext context) { _context = context; }

    public async Task<ListReportsResult> Handle(ListReportsRequest request, CancellationToken cancellationToken)
    {
        var rows = await _context.ReportDefinition.AsNoTracking()
            .Where(x => !x.IsDeleted && (x.OwnerUserId == request.UserId || x.IsShared)).OrderBy(x => x.Name).ToListAsync(cancellationToken);
        return new ListReportsResult
        {
            Data = rows.Where(x => request.AllowedEntities == null || request.AllowedEntities.Contains(x.Entity ?? ""))
                .Select(x => new ReportDto(x.Id, x.Name, x.Entity, x.DefinitionJson, x.IsShared, x.OwnerUserId == request.UserId, x.OwnerName)).ToList()
        };
    }
}

public class SaveReportResult { public string? Id { get; init; } }

public class SaveReportRequest : IRequest<SaveReportResult>
{
    public string? Name { get; init; }
    public string? Entity { get; init; }
    public string? DefinitionJson { get; init; }
    public bool IsShared { get; init; }
    public string? UserId { get; set; }
    public string? UserName { get; set; }
}

public class SaveReportValidator : AbstractValidator<SaveReportRequest>
{
    public SaveReportValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Entity).Must(e => ReportCatalog.Fields.ContainsKey(e ?? "")).WithMessage("Unknown record type.");
        RuleFor(x => x.DefinitionJson).NotEmpty().MaximumLength(50_000);
    }
}

/// <summary>Saving under an existing name of the same owner replaces that report.</summary>
public class SaveReportHandler : IRequestHandler<SaveReportRequest, SaveReportResult>
{
    private readonly ICommandRepository<ReportDefinition> _repository;
    private readonly IUnitOfWork _unitOfWork;
    public SaveReportHandler(ICommandRepository<ReportDefinition> repository, IUnitOfWork unitOfWork) { _repository = repository; _unitOfWork = unitOfWork; }

    public async Task<SaveReportResult> Handle(SaveReportRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name!.Trim();
        var entity = await _repository.GetQuery().FirstOrDefaultAsync(x => !x.IsDeleted && x.OwnerUserId == request.UserId && x.Name == name, cancellationToken);
        var creating = entity == null;
        entity ??= new ReportDefinition { CreatedById = request.UserId, OwnerUserId = request.UserId };

        entity.Name = name;
        entity.Entity = request.Entity;
        entity.DefinitionJson = request.DefinitionJson;
        entity.IsShared = request.IsShared;
        entity.OwnerName = request.UserName;

        if (creating) await _repository.CreateAsync(entity, cancellationToken);
        else { entity.UpdatedById = request.UserId; _repository.Update(entity); }
        await _unitOfWork.SaveAsync(cancellationToken);
        return new SaveReportResult { Id = entity.Id };
    }
}

public class DeleteReportResult { public bool Deleted { get; init; } }
public class DeleteReportRequest : IRequest<DeleteReportResult> { public string? Id { get; init; } public string? UserId { get; set; } }

public class DeleteReportHandler : IRequestHandler<DeleteReportRequest, DeleteReportResult>
{
    private readonly ICommandRepository<ReportDefinition> _repository;
    private readonly IUnitOfWork _unitOfWork;
    public DeleteReportHandler(ICommandRepository<ReportDefinition> repository, IUnitOfWork unitOfWork) { _repository = repository; _unitOfWork = unitOfWork; }

    public async Task<DeleteReportResult> Handle(DeleteReportRequest request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetAsync(request.Id ?? "", cancellationToken) ?? throw new Exception("Report not found.");
        if (entity.OwnerUserId != request.UserId) throw new Exception("Only the owner can delete a report.");
        entity.UpdatedById = request.UserId;
        _repository.Delete(entity);
        await _unitOfWork.SaveAsync(cancellationToken);
        return new DeleteReportResult { Deleted = true };
    }
}
