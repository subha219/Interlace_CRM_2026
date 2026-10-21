using Application.Common.Extensions;
using AutoMapper;
using Domain.Enums;
using MediatR;

namespace Application.Features.QuotationManager.Queries;

public record GetQuotationStatusListDto
{
    public string? Id { get; init; }
    public string? Name { get; init; }
}

public class GetQuotationStatusListProfile : Profile
{
    public GetQuotationStatusListProfile()
    {
    }
}

public class GetQuotationStatusListResult
{
    public List<GetQuotationStatusListDto>? Data { get; init; }
}

public class GetQuotationStatusListRequest : IRequest<GetQuotationStatusListResult>
{
}


public class GetQuotationStatusListHandler : IRequestHandler<GetQuotationStatusListRequest, GetQuotationStatusListResult>
{

    public GetQuotationStatusListHandler()
    {
    }

    public async Task<GetQuotationStatusListResult> Handle(GetQuotationStatusListRequest request, CancellationToken cancellationToken)
    {
        var statuses = Enum.GetValues(typeof(QuotationStatus))
            .Cast<QuotationStatus>()
            .Select(status => new GetQuotationStatusListDto
            {
                Id = ((int)status).ToString(),
                Name = status.ToFriendlyName()
            })
            .ToList();

        await Task.CompletedTask;

        return new GetQuotationStatusListResult
        {
            Data = statuses
        };
    }


}



