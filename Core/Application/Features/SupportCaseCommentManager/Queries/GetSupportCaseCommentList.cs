using Application.Common.CQS.Queries;
using Application.Common.Extensions;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.SupportCaseCommentManager.Queries;

public record GetSupportCaseCommentListDto
{
    public string? Id { get; init; }
    public string? SupportCaseId { get; init; }
    public string? SupportCaseNumber { get; init; }
    public string? Comment { get; init; }
    public DateTime? CreatedAtUtc { get; init; }
}

public class GetSupportCaseCommentListProfile : Profile
{
    public GetSupportCaseCommentListProfile()
    {
        CreateMap<SupportCaseComment, GetSupportCaseCommentListDto>()
            .ForMember(
                dest => dest.SupportCaseNumber,
                opt => opt.MapFrom(src => src.SupportCase != null ? src.SupportCase.Number : string.Empty)
            );
    }
}

public class GetSupportCaseCommentListResult
{
    public List<GetSupportCaseCommentListDto>? Data { get; init; }
}

public class GetSupportCaseCommentListRequest : IRequest<GetSupportCaseCommentListResult>
{
    public bool IsDeleted { get; init; } = false;
}

public class GetSupportCaseCommentListHandler : IRequestHandler<GetSupportCaseCommentListRequest, GetSupportCaseCommentListResult>
{
    private readonly IMapper _mapper;
    private readonly IQueryContext _context;

    public GetSupportCaseCommentListHandler(IMapper mapper, IQueryContext context)
    {
        _mapper = mapper;
        _context = context;
    }

    public async Task<GetSupportCaseCommentListResult> Handle(GetSupportCaseCommentListRequest request, CancellationToken cancellationToken)
    {
        var query = _context
            .SupportCaseComment
            .AsNoTracking()
            .IsDeletedEqualTo(request.IsDeleted)
            .Include(x => x.SupportCase)
            .AsQueryable();

        var entities = await query.ToListAsync(cancellationToken);

        var dtos = _mapper.Map<List<GetSupportCaseCommentListDto>>(entities);

        return new GetSupportCaseCommentListResult
        {
            Data = dtos
        };
    }
}
