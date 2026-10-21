using Application.Common.CQS.Queries;
using Application.Common.Extensions;
using AutoMapper;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.InvoiceItemManager.Queries;

public record GetInvoiceItemListDto
{
    public string? Id { get; init; }
    public string? InvoiceId { get; init; }
    public string? InvoiceNumber { get; init; }
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

public class GetInvoiceItemListProfile : Profile
{
    public GetInvoiceItemListProfile()
    {
        CreateMap<InvoiceItem, GetInvoiceItemListDto>()
            .ForMember(
                dest => dest.InvoiceNumber,
                opt => opt.MapFrom(src => src.Invoice != null ? src.Invoice.Number : string.Empty)
            )
            .ForMember(
                dest => dest.CustomerName,
                opt => opt.MapFrom(src => src.Invoice!.Customer != null ? src.Invoice.Customer.Name : string.Empty)
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

public class GetInvoiceItemListResult
{
    public List<GetInvoiceItemListDto>? Data { get; init; }
}

public class GetInvoiceItemListRequest : IRequest<GetInvoiceItemListResult>
{
    public bool IsDeleted { get; init; } = false;
}


public class GetInvoiceItemListHandler : IRequestHandler<GetInvoiceItemListRequest, GetInvoiceItemListResult>
{
    private readonly IMapper _mapper;
    private readonly IQueryContext _context;

    public GetInvoiceItemListHandler(IMapper mapper, IQueryContext context)
    {
        _mapper = mapper;
        _context = context;
    }

    public async Task<GetInvoiceItemListResult> Handle(GetInvoiceItemListRequest request, CancellationToken cancellationToken)
    {
        var query = _context
            .InvoiceItem
            .AsNoTracking()
            .IsDeletedEqualTo(request.IsDeleted)
            .Include(x => x.Invoice)
                .ThenInclude(x => x!.Customer)
            .Include(x => x.Product)
            .AsQueryable();

        var entities = await query.ToListAsync(cancellationToken);

        var dtos = _mapper.Map<List<GetInvoiceItemListDto>>(entities);

        return new GetInvoiceItemListResult
        {
            Data = dtos
        };
    }


}



