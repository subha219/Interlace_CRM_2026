using Application.Common.CQS.Queries;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.ActivityManager.Queries;

public record GetActivityListDto
{
    public string? Id { get; init; }
    public string? Number { get; init; }
    public string? Subject { get; init; }
    public string? Description { get; init; }
    public ActivityType? Type { get; init; }
    public string? TypeName { get; init; }
    public ActivityStatus? Status { get; init; }
    public string? StatusName { get; init; }
    public string? Priority { get; init; }
    public DateTime? StartAt { get; init; }
    public DateTime? EndAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public string? Outcome { get; init; }
    public string? RelatedType { get; init; }
    public string? RelatedId { get; init; }
    public string? RelatedName { get; init; }
    public string? AssignedToUserId { get; init; }
    public bool IsOverdue { get; init; }
}

public class GetActivityListResult
{
    public List<GetActivityListDto>? Data { get; init; }
}

public class GetActivityListRequest : IRequest<GetActivityListResult>
{
    /// <summary>Only activities assigned to the signed-in user.</summary>
    public bool Mine { get; init; }
    public ActivityStatus? Status { get; init; }
    public ActivityType? Type { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    public string? RelatedType { get; init; }
    public string? RelatedId { get; init; }
    public int Top { get; init; } = 500;

    /// <summary>Filled by the controller from the signed-in user.</summary>
    public string? UserId { get; set; }
}

public class GetActivityListHandler : IRequestHandler<GetActivityListRequest, GetActivityListResult>
{
    private readonly IQueryContext _context;

    public GetActivityListHandler(IQueryContext context)
    {
        _context = context;
    }

    public async Task<GetActivityListResult> Handle(GetActivityListRequest request, CancellationToken cancellationToken)
    {
        var query = _context.Activity.AsNoTracking().Where(x => !x.IsDeleted);

        if (request.Mine && !string.IsNullOrEmpty(request.UserId)) query = query.Where(x => x.AssignedToUserId == request.UserId);
        if (request.Status != null) query = query.Where(x => x.Status == request.Status);
        if (request.Type != null) query = query.Where(x => x.Type == request.Type);
        if (request.From != null) query = query.Where(x => (x.StartAt ?? x.EndAt) >= request.From);
        if (request.To != null) query = query.Where(x => (x.StartAt ?? x.EndAt) < request.To);
        if (!string.IsNullOrEmpty(request.RelatedType)) query = query.Where(x => x.RelatedType == request.RelatedType && x.RelatedId == request.RelatedId);

        var rows = await query
            .OrderBy(x => x.StartAt == null)
            .ThenBy(x => x.StartAt)
            .Take(Math.Clamp(request.Top, 1, 2000))
            .ToListAsync(cancellationToken);

        var now = DateTime.Now;
        var data = rows.Select(x => new GetActivityListDto
        {
            Id = x.Id,
            Number = x.Number,
            Subject = x.Subject,
            Description = x.Description,
            Type = x.Type,
            TypeName = x.Type?.ToString(),
            Status = x.Status,
            StatusName = x.Status?.ToString(),
            Priority = x.Priority,
            StartAt = x.StartAt,
            EndAt = x.EndAt,
            CompletedAt = x.CompletedAt,
            Outcome = x.Outcome,
            RelatedType = x.RelatedType,
            RelatedId = x.RelatedId,
            RelatedName = x.RelatedName,
            AssignedToUserId = x.AssignedToUserId,
            IsOverdue = x.Status == ActivityStatus.Open && (x.EndAt ?? x.StartAt) != null && (x.EndAt ?? x.StartAt) < now
        }).ToList();

        return new GetActivityListResult { Data = data };
    }
}
