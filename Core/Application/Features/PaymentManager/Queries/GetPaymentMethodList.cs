using Application.Common.Extensions;
using AutoMapper;
using Domain.Enums;
using MediatR;

namespace Application.Features.PaymentManager.Queries;

public record GetPaymentMethodListDto
{
    public string? Id { get; init; }
    public string? Name { get; init; }
}

public class GetPaymentMethodListProfile : Profile
{
    public GetPaymentMethodListProfile()
    {
    }
}

public class GetPaymentMethodListResult
{
    public List<GetPaymentMethodListDto>? Data { get; init; }
}

public class GetPaymentMethodListRequest : IRequest<GetPaymentMethodListResult>
{
}

public class GetPaymentMethodListHandler : IRequestHandler<GetPaymentMethodListRequest, GetPaymentMethodListResult>
{

    public GetPaymentMethodListHandler()
    {
    }

    public async Task<GetPaymentMethodListResult> Handle(GetPaymentMethodListRequest request, CancellationToken cancellationToken)
    {
        var items = Enum.GetValues(typeof(PaymentMethod))
            .Cast<PaymentMethod>()
            .Select(item => new GetPaymentMethodListDto
            {
                Id = ((int)item).ToString(),
                Name = item.ToFriendlyName()
            })
            .ToList();

        await Task.CompletedTask;

        return new GetPaymentMethodListResult
        {
            Data = items
        };
    }
}
