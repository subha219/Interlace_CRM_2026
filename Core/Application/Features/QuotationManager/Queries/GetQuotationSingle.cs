using Application.Common.CQS.Queries;
using AutoMapper;
using Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.QuotationManager.Queries;


public class GetQuotationSingleProfile : Profile
{
    public GetQuotationSingleProfile()
    {
    }
}

public class GetQuotationSingleResult
{
    public Quotation? Data { get; init; }
}

public class GetQuotationSingleRequest : IRequest<GetQuotationSingleResult>
{
    public string? Id { get; init; }
}

public class GetQuotationSingleValidator : AbstractValidator<GetQuotationSingleRequest>
{
    public GetQuotationSingleValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class GetQuotationSingleHandler : IRequestHandler<GetQuotationSingleRequest, GetQuotationSingleResult>
{
    private readonly IQueryContext _context;

    public GetQuotationSingleHandler(
        IQueryContext context
        )
    {
        _context = context;
    }

    public async Task<GetQuotationSingleResult> Handle(GetQuotationSingleRequest request, CancellationToken cancellationToken)
    {
        var query = _context
            .Quotation
            .AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.Tax)
            .Include(x => x.QuotationItemList.Where(item => !item.IsDeleted))
                .ThenInclude(x => x.Product)
            .Where(x => x.Id == request.Id)
            .AsQueryable();

        var entity = await query.SingleOrDefaultAsync(cancellationToken);

        return new GetQuotationSingleResult
        {
            Data = entity
        };
    }
}