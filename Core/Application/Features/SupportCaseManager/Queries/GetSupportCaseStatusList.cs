using Application.Common.Extensions;
using AutoMapper;
using Domain.Enums;
using MediatR;

namespace Application.Features.SupportCaseManager.Queries;

public record GetSupportCaseStatusListDto
{
    public string? Id { get; init; }
    public string? Name { get; init; }
}

public class GetSupportCaseStatusListProfile : Profile
{
    public GetSupportCaseStatusListProfile()
    {
    }
}

public class GetSupportCaseStatusListResult
{
    public List<GetSupportCaseStatusListDto>? Data { get; init; }
}

public class GetSupportCaseStatusListRequest : IRequest<GetSupportCaseStatusListResult>
{
}

public class GetSupportCaseStatusListHandler : IRequestHandler<GetSupportCaseStatusListRequest, GetSupportCaseStatusListResult>
{

    public GetSupportCaseStatusListHandler()
    {
    }

    public async Task<GetSupportCaseStatusListResult> Handle(GetSupportCaseStatusListRequest request, CancellationToken cancellationToken)
    {
        var items = Enum.GetValues(typeof(CaseStatus))
            .Cast<CaseStatus>()
            .Select(item => new GetSupportCaseStatusListDto
            {
                Id = ((int)item).ToString(),
                Name = item.ToFriendlyName()
            })
            .ToList();

        await Task.CompletedTask;

        return new GetSupportCaseStatusListResult
        {
            Data = items
        };
    }
}
