using Application.Common.CQS.Queries;
using Application.Common.Extensions;
using AutoMapper;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.QuotationItemManager.Queries;

public record GetQuotationItemByQuotationIdListDto
{
    public string? Id { get; init; }
    public string? QuotationId { get; init; }
    public string? QuotationNumber { get; init; }
    public string? ProductId { get; init; }
    public string? ProductName { get; init; }
    public string? ProductNumber { get; init; }
    public string? Summary { get; init; }
    public double? UnitPrice { get; init; }
    public double? Quantity { get; init; }
    public double? Total { get; init; }
    public DateTime? CreatedAtUtc { get; init; }
}

public class GetQuotationItemByQuotationIdListProfile : Profile
{
    public GetQuotationItemByQuotationIdListProfile()
    {
        CreateMap<QuotationItem, GetQuotationItemByQuotationIdListDto>()
            .ForMember(
                dest => dest.QuotationNumber,
                opt => opt.MapFrom(src => src.Quotation != null ? src.Quotation.Number : string.Empty)
            )
            .ForMember(
                dest => dest.ProductName,
                opt => opt.MapFrom(src => src.Product != null ? src.Product.Name : string.Empty)
            )
            .ForMember(
                dest => dest.ProductNumber,
                opt => opt.MapFrom(src => src.Product != null ? src.Product.Number : string.Empty)
            );

    }
}

public class GetQuotationItemByQuotationIdListResult
{
    public List<GetQuotationItemByQuotationIdListDto>? Data { get; init; }
}

public class GetQuotationItemByQuotationIdListRequest : IRequest<GetQuotationItemByQuotationIdListResult>
{
    public string? QuotationId { get; init; }
}


public class GetQuotationItemByQuotationIdListHandler : IRequestHandler<GetQuotationItemByQuotationIdListRequest, GetQuotationItemByQuotationIdListResult>
{
    private readonly IMapper _mapper;
    private readonly IQueryContext _context;

    public GetQuotationItemByQuotationIdListHandler(IMapper mapper, IQueryContext context)
    {
        _mapper = mapper;
        _context = context;
    }

    public async Task<GetQuotationItemByQuotationIdListResult> Handle(GetQuotationItemByQuotationIdListRequest request, CancellationToken cancellationToken)
    {
        var query = _context
            .QuotationItem
            .AsNoTracking()
            .IsDeletedEqualTo(false)
            .Include(x => x.Quotation)
            .Include(x => x.Product)
            .Where(x => x.QuotationId == request.QuotationId)
            .AsQueryable();

        var entities = await query.ToListAsync(cancellationToken);

        var dtos = _mapper.Map<List<GetQuotationItemByQuotationIdListDto>>(entities);

        return new GetQuotationItemByQuotationIdListResult
        {
            Data = dtos
        };
    }


}



