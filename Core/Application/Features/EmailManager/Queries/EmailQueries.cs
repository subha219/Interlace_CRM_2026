using Application.Common.CQS.Queries;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.EmailManager.Queries;

public class GetEmailTemplateListResult { public List<EmailTemplate>? Data { get; init; } }
public class GetEmailTemplateListRequest : IRequest<GetEmailTemplateListResult> { }

public class GetEmailTemplateListHandler : IRequestHandler<GetEmailTemplateListRequest, GetEmailTemplateListResult>
{
    private readonly IQueryContext _context;
    public GetEmailTemplateListHandler(IQueryContext context) { _context = context; }

    public async Task<GetEmailTemplateListResult> Handle(GetEmailTemplateListRequest request, CancellationToken cancellationToken) =>
        new() { Data = await _context.EmailTemplate.AsNoTracking().Where(x => !x.IsDeleted).OrderBy(x => x.Name).ToListAsync(cancellationToken) };
}

public record GetEmailLogListDto
{
    public string? Id { get; init; }
    public string? ToEmail { get; init; }
    public string? Subject { get; init; }
    public bool Success { get; init; }
    public string? Error { get; init; }
    public string? RelatedType { get; init; }
    public string? RelatedId { get; init; }
    public string? RelatedName { get; init; }
    public string? SentByName { get; init; }
    public DateTime? SentAtUtc { get; init; }
}

public class GetEmailLogListResult { public List<GetEmailLogListDto>? Data { get; init; } }

public class GetEmailLogListRequest : IRequest<GetEmailLogListResult>
{
    public string? RelatedType { get; init; }
    public string? RelatedId { get; init; }
    public int Top { get; init; } = 200;
}

public class GetEmailLogListHandler : IRequestHandler<GetEmailLogListRequest, GetEmailLogListResult>
{
    private readonly IQueryContext _context;
    public GetEmailLogListHandler(IQueryContext context) { _context = context; }

    public async Task<GetEmailLogListResult> Handle(GetEmailLogListRequest request, CancellationToken cancellationToken)
    {
        var query = _context.EmailLog.AsNoTracking().Where(x => !x.IsDeleted);
        if (!string.IsNullOrEmpty(request.RelatedType)) query = query.Where(x => x.RelatedType == request.RelatedType && x.RelatedId == request.RelatedId);

        var data = await query.OrderByDescending(x => x.SentAtUtc).Take(Math.Clamp(request.Top, 1, 1000))
            .Select(x => new GetEmailLogListDto
            {
                Id = x.Id, ToEmail = x.ToEmail, Subject = x.Subject, Success = x.Success, Error = x.Error,
                RelatedType = x.RelatedType, RelatedId = x.RelatedId, RelatedName = x.RelatedName,
                SentByName = x.SentByName, SentAtUtc = x.SentAtUtc
            }).ToListAsync(cancellationToken);

        return new GetEmailLogListResult { Data = data };
    }
}
