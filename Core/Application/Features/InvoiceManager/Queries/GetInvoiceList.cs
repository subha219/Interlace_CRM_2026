using Application.Common.CQS.Queries;
using Application.Common.Extensions;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.InvoiceManager.Queries;

public record GetInvoiceListDto
{
    public string? Id { get; init; }
    public string? Number { get; init; }
    public DateTime? InvoiceDate { get; init; }
    public InvoiceStatus? InvoiceStatus { get; init; }
    public string? InvoiceStatusName { get; init; }
    public DateTime? DueDate { get; init; }
    public string? QuotationId { get; init; }
    public string? Description { get; init; }
    public string? CustomerId { get; init; }
    public string? CustomerName { get; init; }
    public string? TaxId { get; init; }
    public string? TaxName { get; init; }
    public double? BeforeTaxAmount { get; init; }
    public double? TaxAmount { get; init; }
    public double? AfterTaxAmount { get; init; }
    public double? AmountPaid { get; init; }
    public double? BalanceDue { get; init; }
    public bool? IsOverdue { get; init; }
    public DateTime? CreatedAtUtc { get; init; }
}

public class GetInvoiceListProfile : Profile
{
    public GetInvoiceListProfile()
    {
        CreateMap<Invoice, GetInvoiceListDto>()
            .ForMember(
                dest => dest.CustomerName,
                opt => opt.MapFrom(src => src.Customer != null ? src.Customer.Name : string.Empty)
            )
            .ForMember(
                dest => dest.TaxName,
                opt => opt.MapFrom(src => src.Tax != null ? src.Tax.Name : string.Empty)
            )
            .ForMember(
                dest => dest.InvoiceStatusName,
                opt => opt.MapFrom(src => src.InvoiceStatus.HasValue ? src.InvoiceStatus.Value.ToFriendlyName() : string.Empty)
            )
            .ForMember(
                dest => dest.IsOverdue,
                opt => opt.MapFrom(src => src.DueDate.HasValue && src.DueDate.Value < DateTime.Now && src.BalanceDue > 0 && src.InvoiceStatus != InvoiceStatus.Cancelled && src.InvoiceStatus != InvoiceStatus.Draft)
            );

    }
}

public class GetInvoiceListResult
{
    public List<GetInvoiceListDto>? Data { get; init; }
}

public class GetInvoiceListRequest : IRequest<GetInvoiceListResult>
{
    public bool IsDeleted { get; init; } = false;
}


public class GetInvoiceListHandler : IRequestHandler<GetInvoiceListRequest, GetInvoiceListResult>
{
    private readonly IMapper _mapper;
    private readonly IQueryContext _context;

    public GetInvoiceListHandler(IMapper mapper, IQueryContext context)
    {
        _mapper = mapper;
        _context = context;
    }

    public async Task<GetInvoiceListResult> Handle(GetInvoiceListRequest request, CancellationToken cancellationToken)
    {
        var query = _context
            .Invoice
            .AsNoTracking()
            .IsDeletedEqualTo(request.IsDeleted)
            .Include(x => x.Customer)
            .Include(x => x.Tax)
            .AsQueryable();

        var entities = await query.ToListAsync(cancellationToken);

        var dtos = _mapper.Map<List<GetInvoiceListDto>>(entities);

        return new GetInvoiceListResult
        {
            Data = dtos
        };
    }


}



