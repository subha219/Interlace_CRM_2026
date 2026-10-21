using Application.Common.Extensions;
using AutoMapper;
using Domain.Enums;
using MediatR;

namespace Application.Features.SupportCaseManager.Queries;

public record GetSupportCaseChannelListDto
{
    public string? Id { get; init; }
    public string? Name { get; init; }
}

public class GetSupportCaseChannelListProfile : Profile
{
    public GetSupportCaseChannelListProfile()
    {
    }
}

public class GetSupportCaseChannelListResult
{
    public List<GetSupportCaseChannelListDto>? Data { get; init; }
}

public class GetSupportCaseChannelListRequest : IRequest<GetSupportCaseChannelListResult>
{
}

public class GetSupportCaseChannelListHandler : IRequestHandler<GetSupportCaseChannelListRequest, GetSupportCaseChannelListResult>
{

    public GetSupportCaseChannelListHandler()
    {
    }

    public async Task<GetSupportCaseChannelListResult> Handle(GetSupportCaseChannelListRequest request, CancellationToken cancellationToken)
    {
        var items = Enum.GetValues(typeof(CaseChannel))
            .Cast<CaseChannel>()
            .Select(item => new GetSupportCaseChannelListDto
            {
                Id = ((int)item).ToString(),
                Name = item.ToFriendlyName()
            })
            .ToList();

        await Task.CompletedTask;

        return new GetSupportCaseChannelListResult
        {
            Data = items
        };
    }
}
