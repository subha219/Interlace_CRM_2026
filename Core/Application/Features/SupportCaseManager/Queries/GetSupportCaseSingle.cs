using Application.Common.CQS.Queries;
using AutoMapper;
using Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.SupportCaseManager.Queries;

public class GetSupportCaseSingleProfile : Profile
{
    public GetSupportCaseSingleProfile()
    {
    }
}

public class GetSupportCaseSingleResult
{
    public SupportCase? Data { get; init; }
}

public class GetSupportCaseSingleRequest : IRequest<GetSupportCaseSingleResult>
{
    public string? Id { get; init; }
}

public class GetSupportCaseSingleValidator : AbstractValidator<GetSupportCaseSingleRequest>
{
    public GetSupportCaseSingleValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class GetSupportCaseSingleHandler : IRequestHandler<GetSupportCaseSingleRequest, GetSupportCaseSingleResult>
{
    private readonly IQueryContext _context;

    public GetSupportCaseSingleHandler(
        IQueryContext context
        )
    {
        _context = context;
    }

    public async Task<GetSupportCaseSingleResult> Handle(GetSupportCaseSingleRequest request, CancellationToken cancellationToken)
    {
        var query = _context
            .SupportCase
            .AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.SalesRepresentative)
            .Where(x => x.Id == request.Id)
            .AsQueryable();

        var entity = await query.SingleOrDefaultAsync(cancellationToken);

        return new GetSupportCaseSingleResult
        {
            Data = entity
        };
    }
}
