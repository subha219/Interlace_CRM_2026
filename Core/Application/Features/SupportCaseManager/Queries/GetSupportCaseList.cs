using Application.Common.CQS.Queries;
using Application.Common.Extensions;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.SupportCaseManager.Queries;

public record GetSupportCaseListDto
{
    public string? Id { get; init; }
    public string? Number { get; init; }
    public string? Title { get; init; }
    public CaseStatus? Status { get; init; }
    public string? StatusName { get; init; }
    public CasePriority? Priority { get; init; }
    public string? PriorityName { get; init; }
    public CaseChannel? Channel { get; init; }
    public string? ChannelName { get; init; }
    public string? CustomerId { get; init; }
    public string? CustomerName { get; init; }
    public string? SalesRepresentativeId { get; init; }
    public string? SalesRepresentativeName { get; init; }
    public DateTime? DueDate { get; init; }
    public string? Description { get; init; }
    public string? Resolution { get; init; }
    public DateTime? ResolvedAtUtc { get; init; }
    public bool? IsOverdue { get; init; }
    public DateTime? CreatedAtUtc { get; init; }
}

public class GetSupportCaseListProfile : Profile
{
    public GetSupportCaseListProfile()
    {
        CreateMap<SupportCase, GetSupportCaseListDto>()
            .ForMember(
                dest => dest.StatusName,
                opt => opt.MapFrom(src => src.Status.HasValue ? src.Status.Value.ToFriendlyName() : string.Empty)
            )
            .ForMember(
                dest => dest.PriorityName,
                opt => opt.MapFrom(src => src.Priority.HasValue ? src.Priority.Value.ToFriendlyName() : string.Empty)
            )
            .ForMember(
                dest => dest.ChannelName,
                opt => opt.MapFrom(src => src.Channel.HasValue ? src.Channel.Value.ToFriendlyName() : string.Empty)
            )
            .ForMember(
                dest => dest.CustomerName,
                opt => opt.MapFrom(src => src.Customer != null ? src.Customer.Name : string.Empty)
            )
            .ForMember(
                dest => dest.SalesRepresentativeName,
                opt => opt.MapFrom(src => src.SalesRepresentative != null ? src.SalesRepresentative.Name : string.Empty)
            )
            .ForMember(
                dest => dest.IsOverdue,
                opt => opt.MapFrom(src => src.DueDate.HasValue && src.DueDate.Value < DateTime.UtcNow && src.Status != CaseStatus.Resolved && src.Status != CaseStatus.Closed)
            );
    }
}

public class GetSupportCaseListResult
{
    public List<GetSupportCaseListDto>? Data { get; init; }
}

public class GetSupportCaseListRequest : IRequest<GetSupportCaseListResult>
{
    public bool IsDeleted { get; init; } = false;
}

public class GetSupportCaseListHandler : IRequestHandler<GetSupportCaseListRequest, GetSupportCaseListResult>
{
    private readonly IMapper _mapper;
    private readonly IQueryContext _context;

    public GetSupportCaseListHandler(IMapper mapper, IQueryContext context)
    {
        _mapper = mapper;
        _context = context;
    }

    public async Task<GetSupportCaseListResult> Handle(GetSupportCaseListRequest request, CancellationToken cancellationToken)
    {
        var query = _context
            .SupportCase
            .AsNoTracking()
            .IsDeletedEqualTo(request.IsDeleted)
            .Include(x => x.Customer)
            .Include(x => x.SalesRepresentative)
            .AsQueryable();

        var entities = await query.ToListAsync(cancellationToken);

        var dtos = _mapper.Map<List<GetSupportCaseListDto>>(entities);

        return new GetSupportCaseListResult
        {
            Data = dtos
        };
    }
}
