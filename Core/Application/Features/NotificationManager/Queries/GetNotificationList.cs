using Application.Common.CQS.Queries;
using Application.Common.Extensions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.NotificationManager.Queries;

public record GetNotificationListDto
{
    public string? Id { get; init; }
    public string? Title { get; init; }
    public string? Message { get; init; }
    public string? Type { get; init; }
    public string? Url { get; init; }
    public bool IsRead { get; init; }
    public DateTime? CreatedAtUtc { get; init; }
}

public class GetNotificationListResult
{
    public List<GetNotificationListDto>? Data { get; init; }
    public int UnreadCount { get; init; }
}

public class GetNotificationListRequest : IRequest<GetNotificationListResult>
{
    /// <summary>Filled by the controller from the signed-in user.</summary>
    public string? UserId { get; set; }
    public bool UnreadOnly { get; init; } = false;
    public int Top { get; init; } = 50;
}

public class GetNotificationListHandler : IRequestHandler<GetNotificationListRequest, GetNotificationListResult>
{
    private readonly IQueryContext _context;

    public GetNotificationListHandler(IQueryContext context)
    {
        _context = context;
    }

    public async Task<GetNotificationListResult> Handle(GetNotificationListRequest request, CancellationToken cancellationToken)
    {
        var query = _context.Notification
            .AsNoTracking()
            .IsDeletedEqualTo()
            .Where(x => x.UserId == null || x.UserId == request.UserId);

        var unread = await query.CountAsync(x => !x.IsRead, cancellationToken);

        if (request.UnreadOnly)
        {
            query = query.Where(x => !x.IsRead);
        }

        var top = Math.Clamp(request.Top, 1, 500);

        var data = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(top)
            .Select(x => new GetNotificationListDto
            {
                Id = x.Id,
                Title = x.Title,
                Message = x.Message,
                Type = x.Type,
                Url = x.Url,
                IsRead = x.IsRead,
                CreatedAtUtc = x.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return new GetNotificationListResult
        {
            Data = data,
            UnreadCount = unread
        };
    }
}
