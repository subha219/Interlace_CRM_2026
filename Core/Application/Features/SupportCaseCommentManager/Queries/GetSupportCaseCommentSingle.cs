using Application.Common.CQS.Queries;
using AutoMapper;
using Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.SupportCaseCommentManager.Queries;

public class GetSupportCaseCommentSingleProfile : Profile
{
    public GetSupportCaseCommentSingleProfile()
    {
    }
}

public class GetSupportCaseCommentSingleResult
{
    public SupportCaseComment? Data { get; init; }
}

public class GetSupportCaseCommentSingleRequest : IRequest<GetSupportCaseCommentSingleResult>
{
    public string? Id { get; init; }
}

public class GetSupportCaseCommentSingleValidator : AbstractValidator<GetSupportCaseCommentSingleRequest>
{
    public GetSupportCaseCommentSingleValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class GetSupportCaseCommentSingleHandler : IRequestHandler<GetSupportCaseCommentSingleRequest, GetSupportCaseCommentSingleResult>
{
    private readonly IQueryContext _context;

    public GetSupportCaseCommentSingleHandler(
        IQueryContext context
        )
    {
        _context = context;
    }

    public async Task<GetSupportCaseCommentSingleResult> Handle(GetSupportCaseCommentSingleRequest request, CancellationToken cancellationToken)
    {
        var query = _context
            .SupportCaseComment
            .AsNoTracking()
            .Include(x => x.SupportCase)
            .Where(x => x.Id == request.Id)
            .AsQueryable();

        var entity = await query.SingleOrDefaultAsync(cancellationToken);

        return new GetSupportCaseCommentSingleResult
        {
            Data = entity
        };
    }
}
