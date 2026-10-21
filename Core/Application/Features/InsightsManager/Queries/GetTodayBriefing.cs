using MediatR;

namespace Application.Features.InsightsManager.Queries;

public class GetTodayBriefingRequest : IRequest<TodayBriefingResult>
{
}

public class GetTodayBriefingHandler : IRequestHandler<GetTodayBriefingRequest, TodayBriefingResult>
{
    private readonly InsightsService _insights;

    public GetTodayBriefingHandler(InsightsService insights)
    {
        _insights = insights;
    }

    public Task<TodayBriefingResult> Handle(GetTodayBriefingRequest request, CancellationToken cancellationToken)
    {
        return _insights.GetBriefingAsync(cancellationToken);
    }
}
