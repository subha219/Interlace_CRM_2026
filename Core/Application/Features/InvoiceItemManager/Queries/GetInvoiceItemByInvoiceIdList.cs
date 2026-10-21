using Application.Common.CQS.Queries;
using Application.Common.Extensions;
using AutoMapper;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.InvoiceItemManager.Queries;

public record GetInvoiceItemByInvoiceIdListDto
{
    public string? Id { get; init; }
    public string? InvoiceId { get; init; }
    public string? InvoiceNumber { get; init; }
    public string? ProductId { get; init; }
    public string? ProductName { get; init; }
    public string? ProductNumber { get; init; }
    public string? Summary { get; init; }
    public double? UnitPrice { get; init; }
    public double? Quantity { get; init; }
    public double? Total { get; init; }
    public DateTime? CreatedAtUtc { get; init; }
}

public class GetInvoiceItemByInvoiceIdListProfile : Profile
{
    public GetInvoiceItemByInvoiceIdListProfile()
    {
        CreateMap<InvoiceItem, GetInvoiceItemByInvoiceIdListDto>()
            .ForMember(
                dest => dest.InvoiceNumber,
                opt => opt.MapFrom(src => src.Invoice != null ? src.Invoice.Number : string.Empty)
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

public class GetInvoiceItemByInvoiceIdListResult
{
    public List<GetInvoiceItemByInvoiceIdListDto>? Data { get; init; }
}

public class GetInvoiceItemByInvoiceIdListRequest : IRequest<GetInvoiceItemByInvoiceIdListResult>
{
    public string? InvoiceId { get; init; }
}


public class GetInvoiceItemByInvoiceIdListHandler : IRequestHandler<GetInvoiceItemByInvoiceIdListRequest, GetInvoiceItemByInvoiceIdListResult>
{
    private readonly IMapper _mapper;
    private readonly IQueryContext _context;

    public GetInvoiceItemByInvoiceIdListHandler(IMapper mapper, IQueryContext context)
    {
        _mapper = mapper;
        _context = context;
    }

    public async Task<GetInvoiceItemByInvoiceIdListResult> Handle(GetInvoiceItemByInvoiceIdListRequest request, CancellationToken cancellationToken)
    {
        var query = _context
            .InvoiceItem
            .AsNoTracking()
            .IsDeletedEqualTo(false)
            .Include(x => x.Invoice)
            .Include(x => x.Product)
            .Where(x => x.InvoiceId == request.InvoiceId)
            .AsQueryable();

        var entities = await query.ToListAsync(cancellationToken);

        var dtos = _mapper.Map<List<GetInvoiceItemByInvoiceIdListDto>>(entities);

        return new GetInvoiceItemByInvoiceIdListResult
        {
            Data = dtos
        };
    }


}



