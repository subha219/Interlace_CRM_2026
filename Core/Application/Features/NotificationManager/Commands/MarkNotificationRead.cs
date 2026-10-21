using Application.Common.Repositories;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.NotificationManager.Commands;

public class MarkNotificationReadResult
{
    public int Updated { get; set; }
}

public class MarkNotificationReadRequest : IRequest<MarkNotificationReadResult>
{
    /// <summary>One notification id. Leave empty together with MarkAll to clear every unread one.</summary>
    public string? Id { get; init; }
    public bool MarkAll { get; init; }

    /// <summary>Filled by the controller from the signed-in user.</summary>
    public string? UserId { get; set; }
}

public class MarkNotificationReadHandler : IRequestHandler<MarkNotificationReadRequest, MarkNotificationReadResult>
{
    private readonly ICommandRepository<Notification> _repository;
    private readonly IUnitOfWork _unitOfWork;

    public MarkNotificationReadHandler(ICommandRepository<Notification> repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<MarkNotificationReadResult> Handle(MarkNotificationReadRequest request, CancellationToken cancellationToken)
    {
        var query = _repository.GetQuery()
            .Where(x => !x.IsDeleted && !x.IsRead && (x.UserId == null || x.UserId == request.UserId));

        if (!request.MarkAll)
        {
            query = query.Where(x => x.Id == request.Id);
        }

        var items = await query.ToListAsync(cancellationToken);

        foreach (var item in items)
        {
            item.IsRead = true;
            item.UpdatedById = request.UserId;
            _repository.Update(item);
        }

        await _unitOfWork.SaveAsync(cancellationToken);

        return new MarkNotificationReadResult { Updated = items.Count };
    }
}
