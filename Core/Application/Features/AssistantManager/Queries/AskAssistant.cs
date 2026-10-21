using MediatR;

namespace Application.Features.AssistantManager.Queries;

public class AskAssistantResult
{
    public AssistantAnswer? Data { get; init; }
}

public class AskAssistantRequest : IRequest<AskAssistantResult>
{
    public string? Question { get; init; }

    /// <summary>Filled by the controller; used for AI limits and the usage log.</summary>
    public string? UserId { get; set; }
}

public class AskAssistantHandler : IRequestHandler<AskAssistantRequest, AskAssistantResult>
{
    private readonly AssistantService _assistant;

    public AskAssistantHandler(AssistantService assistant)
    {
        _assistant = assistant;
    }

    public async Task<AskAssistantResult> Handle(AskAssistantRequest request, CancellationToken cancellationToken)
    {
        return new AskAssistantResult
        {
            Data = await _assistant.AskAsync(request.Question, request.UserId, cancellationToken)
        };
    }
}
