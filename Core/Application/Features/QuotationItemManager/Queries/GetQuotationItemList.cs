using Application.Common.CQS.Queries;
using Application.Common.Extensions;
using AutoMapper;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.QuotationItemManager.Queries;

public record GetQuotationItemListDto
{
    public string? Id { get; init; }
    public string? QuotationId { get; init; }
    public string? QuotationNumber { get; init; }
    public string? CustomerName { get; init; }
    public string? ProductId { get; init; }
    public string? ProductName { get; init; }
    public string? ProductNumber { get; init; }
    public string? Summary { get; init; }
    public double? UnitPrice { get; init; }
    public double? Quantity { get; init; }
    public double? Total { get; init; }
    public DateTime? CreatedAtUtc { get; init; }
}

public class GetQuotationItemListProfile : Profile
{
    public GetQuotationItemListProfile()
    {
        CreateMap<QuotationItem, GetQuotationItemListDto>()
            .ForMember(
                dest => dest.QuotationNumber,
                opt => opt.MapFrom(src => src.Quotation != null ? src.Quotation.Number : string.Empty)
            )
            .ForMember(
                dest => dest.CustomerName,
                opt => opt.MapFrom(src => src.Quotation!.Customer != null ? src.Quotation.Customer.Name : string.Empty)
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

public class GetQuotationItemListResult
{
    public List<GetQuotationItemListDto>? Data { get; init; }
}

public class GetQuotationItemListRequest : IRequest<GetQuotationItemListResult>
{
    public bool IsDeleted { get; init; } = false;
}


public class GetQuotationItemListHandler : IRequestHandler<GetQuotationItemListRequest, GetQuotationItemListResult>
{
    private readonly IMapper _mapper;
    private readonly IQueryContext _context;

    public GetQuotationItemListHandler(IMapper mapper, IQueryContext context)
    {
        _mapper = mapper;
        _context = context;
    }

    public async Task<GetQuotationItemListResult> Handle(GetQuotationItemListRequest request, CancellationToken cancellationToken)
    {
        var query = _context
            .QuotationItem
            .AsNoTracking()
            .IsDeletedEqualTo(request.IsDeleted)
            .Include(x => x.Quotation)
                .ThenInclude(x => x!.Customer)
            .Include(x => x.Product)
            .AsQueryable();

        var entities = await query.ToListAsync(cancellationToken);

        var dtos = _mapper.Map<List<GetQuotationItemListDto>>(entities);

        return new GetQuotationItemListResult
        {
            Data = dtos
        };
    }


}



