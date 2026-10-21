using Application.Common.Extensions;
using AutoMapper;
using Domain.Enums;
using MediatR;

namespace Application.Features.SupportCaseManager.Queries;

public record GetSupportCasePriorityListDto
{
    public string? Id { get; init; }
    public string? Name { get; init; }
}

public class GetSupportCasePriorityListProfile : Profile
{
    public GetSupportCasePriorityListProfile()
    {
    }
}

public class GetSupportCasePriorityListResult
{
    public List<GetSupportCasePriorityListDto>? Data { get; init; }
}

public class GetSupportCasePriorityListRequest : IRequest<GetSupportCasePriorityListResult>
{
}

public class GetSupportCasePriorityListHandler : IRequestHandler<GetSupportCasePriorityListRequest, GetSupportCasePriorityListResult>
{

    public GetSupportCasePriorityListHandler()
    {
    }

    public async Task<GetSupportCasePriorityListResult> Handle(GetSupportCasePriorityListRequest request, CancellationToken cancellationToken)
    {
        var items = Enum.GetValues(typeof(CasePriority))
            .Cast<CasePriority>()
            .Select(item => new GetSupportCasePriorityListDto
            {
                Id = ((int)item).ToString(),
                Name = item.ToFriendlyName()
            })
            .ToList();

        await Task.CompletedTask;

        return new GetSupportCasePriorityListResult
        {
            Data = items
        };
    }
}
