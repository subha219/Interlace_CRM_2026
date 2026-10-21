using Application.Common.CQS.Queries;
using Application.Common.Extensions;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.PaymentManager.Queries;

public record GetPaymentListDto
{
    public string? Id { get; init; }
    public string? Number { get; init; }
    public string? InvoiceId { get; init; }
    public string? InvoiceNumber { get; init; }
    public DateTime? PaymentDate { get; init; }
    public double? Amount { get; init; }
    public PaymentMethod? Method { get; init; }
    public string? MethodName { get; init; }
    public string? Reference { get; init; }
    public string? Description { get; init; }
    public DateTime? CreatedAtUtc { get; init; }
}

public class GetPaymentListProfile : Profile
{
    public GetPaymentListProfile()
    {
        CreateMap<Payment, GetPaymentListDto>()
            .ForMember(
                dest => dest.InvoiceNumber,
                opt => opt.MapFrom(src => src.Invoice != null ? src.Invoice.Number : string.Empty)
            )
            .ForMember(
                dest => dest.MethodName,
                opt => opt.MapFrom(src => src.Method.HasValue ? src.Method.Value.ToFriendlyName() : string.Empty)
            );
    }
}

public class GetPaymentListResult
{
    public List<GetPaymentListDto>? Data { get; init; }
}

public class GetPaymentListRequest : IRequest<GetPaymentListResult>
{
    public bool IsDeleted { get; init; } = false;
}

public class GetPaymentListHandler : IRequestHandler<GetPaymentListRequest, GetPaymentListResult>
{
    private readonly IMapper _mapper;
    private readonly IQueryContext _context;

    public GetPaymentListHandler(IMapper mapper, IQueryContext context)
    {
        _mapper = mapper;
        _context = context;
    }

    public async Task<GetPaymentListResult> Handle(GetPaymentListRequest request, CancellationToken cancellationToken)
    {
        var query = _context
            .Payment
            .AsNoTracking()
            .IsDeletedEqualTo(request.IsDeleted)
            .Include(x => x.Invoice)
            .AsQueryable();

        var entities = await query.ToListAsync(cancellationToken);

        var dtos = _mapper.Map<List<GetPaymentListDto>>(entities);

        return new GetPaymentListResult
        {
            Data = dtos
        };
    }
}
