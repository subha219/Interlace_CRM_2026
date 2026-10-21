using Application.Common.CQS.Queries;
using Application.Common.Extensions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.AuditLogManager.Queries;

public record GetAuditLogListDto
{
    public string? Id { get; init; }
    public string? EntityName { get; init; }
    public string? EntityId { get; init; }
    public string? Action { get; init; }
    public string? UserName { get; init; }
    public string? Changes { get; init; }
    public DateTime? CreatedAtUtc { get; init; }
}

public class GetAuditLogListResult
{
    public List<GetAuditLogListDto>? Data { get; init; }
}

public class GetAuditLogListRequest : IRequest<GetAuditLogListResult>
{
    public string? EntityName { get; init; }
    public string? EntityId { get; init; }
    public int Top { get; init; } = 500;
}

public class GetAuditLogListHandler : IRequestHandler<GetAuditLogListRequest, GetAuditLogListResult>
{
    private readonly IQueryContext _context;

    public GetAuditLogListHandler(IQueryContext context)
    {
        _context = context;
    }

    public async Task<GetAuditLogListResult> Handle(GetAuditLogListRequest request, CancellationToken cancellationToken)
    {
        var query = _context.AuditLog
            .AsNoTracking()
            .IsDeletedEqualTo()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.EntityName))
        {
            query = query.Where(x => x.EntityName == request.EntityName);
        }

        if (!string.IsNullOrWhiteSpace(request.EntityId))
        {
            query = query.Where(x => x.EntityId == request.EntityId);
        }

        var data = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(Math.Clamp(request.Top, 1, 2000))
            .Select(x => new GetAuditLogListDto
            {
                Id = x.Id,
                EntityName = x.EntityName,
                EntityId = x.EntityId,
                Action = x.Action,
                UserName = x.UserName,
                Changes = x.Changes,
                CreatedAtUtc = x.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return new GetAuditLogListResult { Data = data };
    }
}
