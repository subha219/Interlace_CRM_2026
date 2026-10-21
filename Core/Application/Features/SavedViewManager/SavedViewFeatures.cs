using Application.Common.CQS.Queries;
using Application.Common.Repositories;
using Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.SavedViewManager;

public record SavedViewDto(string? Id, string? Name, string? StateJson, bool IsShared, bool IsMine, string? OwnerName);

public class GetSavedViewListResult { public List<SavedViewDto>? Data { get; init; } }

public class GetSavedViewListRequest : IRequest<GetSavedViewListResult>
{
    public string? Page { get; init; }
    public string? UserId { get; set; }
}

public class GetSavedViewListHandler : IRequestHandler<GetSavedViewListRequest, GetSavedViewListResult>
{
    private readonly IQueryContext _context;
    public GetSavedViewListHandler(IQueryContext context) { _context = context; }

    public async Task<GetSavedViewListResult> Handle(GetSavedViewListRequest request, CancellationToken cancellationToken)
    {
        var rows = await _context.SavedView.AsNoTracking()
            .Where(x => !x.IsDeleted && x.Page == request.Page && (x.OwnerUserId == request.UserId || x.IsShared))
            .OrderBy(x => x.Name).ToListAsync(cancellationToken);

        return new GetSavedViewListResult
        {
            Data = rows.Select(x => new SavedViewDto(x.Id, x.Name, x.StateJson, x.IsShared, x.OwnerUserId == request.UserId, x.OwnerName)).ToList()
        };
    }
}

public class SaveSavedViewResult { public SavedViewDto? Data { get; init; } }

public class SaveSavedViewRequest : IRequest<SaveSavedViewResult>
{
    public string? Page { get; init; }
    public string? Name { get; init; }
    public string? StateJson { get; init; }
    public bool IsShared { get; init; }
    public string? UserId { get; set; }
    public string? UserName { get; set; }
}

public class SaveSavedViewValidator : AbstractValidator<SaveSavedViewRequest>
{
    public SaveSavedViewValidator()
    {
        RuleFor(x => x.Page).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.StateJson).NotEmpty().MaximumLength(200_000);
    }
}

/// <summary>Saving under an existing name of the same user and page replaces that view.</summary>
public class SaveSavedViewHandler : IRequestHandler<SaveSavedViewRequest, SaveSavedViewResult>
{
    private readonly ICommandRepository<SavedView> _repository;
    private readonly IUnitOfWork _unitOfWork;

    public SaveSavedViewHandler(ICommandRepository<SavedView> repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<SaveSavedViewResult> Handle(SaveSavedViewRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name!.Trim();
        var entity = await _repository.GetQuery()
            .FirstOrDefaultAsync(x => !x.IsDeleted && x.Page == request.Page && x.OwnerUserId == request.UserId && x.Name == name, cancellationToken);

        var creating = entity == null;
        entity ??= new SavedView { CreatedById = request.UserId, Page = request.Page, OwnerUserId = request.UserId };

        entity.Name = name;
        entity.StateJson = request.StateJson;
        entity.IsShared = request.IsShared;
        entity.OwnerName = request.UserName;

        if (creating) await _repository.CreateAsync(entity, cancellationToken);
        else { entity.UpdatedById = request.UserId; _repository.Update(entity); }

        await _unitOfWork.SaveAsync(cancellationToken);
        return new SaveSavedViewResult { Data = new SavedViewDto(entity.Id, entity.Name, entity.StateJson, entity.IsShared, true, entity.OwnerName) };
    }
}

public class DeleteSavedViewResult { public bool Deleted { get; init; } }

public class DeleteSavedViewRequest : IRequest<DeleteSavedViewResult>
{
    public string? Id { get; init; }
    public string? UserId { get; set; }
}

public class DeleteSavedViewValidator : AbstractValidator<DeleteSavedViewRequest>
{
    public DeleteSavedViewValidator() { RuleFor(x => x.Id).NotEmpty(); }
}

public class DeleteSavedViewHandler : IRequestHandler<DeleteSavedViewRequest, DeleteSavedViewResult>
{
    private readonly ICommandRepository<SavedView> _repository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteSavedViewHandler(ICommandRepository<SavedView> repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<DeleteSavedViewResult> Handle(DeleteSavedViewRequest request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetAsync(request.Id!, cancellationToken) ?? throw new Exception("View not found.");
        if (entity.OwnerUserId != request.UserId) throw new Exception("Only the owner can delete a view.");

        entity.UpdatedById = request.UserId;
        _repository.Delete(entity);
        await _unitOfWork.SaveAsync(cancellationToken);
        return new DeleteSavedViewResult { Deleted = true };
    }
}
