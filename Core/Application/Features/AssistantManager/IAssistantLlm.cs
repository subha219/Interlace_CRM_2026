namespace Application.Features.AssistantManager;

/// <summary>
/// Optional large-language-model backend. When no model is configured the assistant answers
/// from built-in rules over live CRM data, so nothing leaves the server.
/// </summary>
public interface IAssistantLlm
{
    bool IsConfigured { get; }

    Task<string?> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default, int maxTokens = 700);
}
