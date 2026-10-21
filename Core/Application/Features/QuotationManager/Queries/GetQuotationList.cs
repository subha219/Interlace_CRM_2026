using Application.Common.CQS.Queries;
using Application.Common.Extensions;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.QuotationManager.Queries;

public record GetQuotationListDto
{
    public string? Id { get; init; }
    public string? Number { get; init; }
    public DateTime? QuotationDate { get; init; }
    public QuotationStatus? QuotationStatus { get; init; }
    public string? QuotationStatusName { get; init; }
    public DateTime? ValidUntil { get; init; }
    public string? ConvertedInvoiceId { get; init; }
    public string? ConvertedSalesOrderId { get; init; }
    public string? Description { get; init; }
    public string? CustomerId { get; init; }
    public string? CustomerName { get; init; }
    public string? TaxId { get; init; }
    public string? TaxName { get; init; }
    public double? BeforeTaxAmount { get; init; }
    public double? TaxAmount { get; init; }
    public double? AfterTaxAmount { get; init; }
    public DateTime? CreatedAtUtc { get; init; }
}

public class GetQuotationListProfile : Profile
{
    public GetQuotationListProfile()
    {
        CreateMap<Quotation, GetQuotationListDto>()
            .ForMember(
                dest => dest.CustomerName,
                opt => opt.MapFrom(src => src.Customer != null ? src.Customer.Name : string.Empty)
            )
            .ForMember(
                dest => dest.TaxName,
                opt => opt.MapFrom(src => src.Tax != null ? src.Tax.Name : string.Empty)
            )
            .ForMember(
                dest => dest.QuotationStatusName,
                opt => opt.MapFrom(src => src.QuotationStatus.HasValue ? src.QuotationStatus.Value.ToFriendlyName() : string.Empty)
            );

    }
}

public class GetQuotationListResult
{
    public List<GetQuotationListDto>? Data { get; init; }
}

public class GetQuotationListRequest : IRequest<GetQuotationListResult>
{
    public bool IsDeleted { get; init; } = false;
}


public class GetQuotationListHandler : IRequestHandler<GetQuotationListRequest, GetQuotationListResult>
{
    private readonly IMapper _mapper;
    private readonly IQueryContext _context;

    public GetQuotationListHandler(IMapper mapper, IQueryContext context)
    {
        _mapper = mapper;
        _context = context;
    }

    public async Task<GetQuotationListResult> Handle(GetQuotationListRequest request, CancellationToken cancellationToken)
    {
        var query = _context
            .Quotation
            .AsNoTracking()
            .IsDeletedEqualTo(request.IsDeleted)
            .Include(x => x.Customer)
            .Include(x => x.Tax)
            .AsQueryable();

        var entities = await query.ToListAsync(cancellationToken);

        var dtos = _mapper.Map<List<GetQuotationListDto>>(entities);

        return new GetQuotationListResult
        {
            Data = dtos
        };
    }


}



