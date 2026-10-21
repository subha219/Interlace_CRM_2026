using Application.Common.CQS.Queries;
using AutoMapper;
using Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.PaymentManager.Queries;

public class GetPaymentSingleProfile : Profile
{
    public GetPaymentSingleProfile()
    {
    }
}

public class GetPaymentSingleResult
{
    public Payment? Data { get; init; }
}

public class GetPaymentSingleRequest : IRequest<GetPaymentSingleResult>
{
    public string? Id { get; init; }
}

public class GetPaymentSingleValidator : AbstractValidator<GetPaymentSingleRequest>
{
    public GetPaymentSingleValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class GetPaymentSingleHandler : IRequestHandler<GetPaymentSingleRequest, GetPaymentSingleResult>
{
    private readonly IQueryContext _context;

    public GetPaymentSingleHandler(
        IQueryContext context
        )
    {
        _context = context;
    }

    public async Task<GetPaymentSingleResult> Handle(GetPaymentSingleRequest request, CancellationToken cancellationToken)
    {
        var query = _context
            .Payment
            .AsNoTracking()
            .Include(x => x.Invoice)
            .Where(x => x.Id == request.Id)
            .AsQueryable();

        var entity = await query.SingleOrDefaultAsync(cancellationToken);

        return new GetPaymentSingleResult
        {
            Data = entity
        };
    }
}
