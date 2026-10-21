using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Application.Features.AssistantManager;
using Application.Common.Services.IntegrationManager;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.AssistantManager;

/// <summary>
/// Calls the Anthropic Messages API when "Assistant:ApiKey" is set. Without a key the assistant uses its built-in rules.
/// Settings: Assistant:ApiKey, Assistant:Model (default claude-sonnet-5-5), Assistant:Endpoint.
/// Only an aggregated snapshot of CRM numbers is sent, never raw tables.
/// </summary>
public class ClaudeAssistantLlm : IAssistantLlm
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(40) };

    private readonly IConnectionSettings _settings;
    private string? _apiKey => _settings["Assistant:ApiKey"];
    private string _model => _settings["Assistant:Model"] is { Length: > 0 } m ? m : "claude-sonnet-5-5";
    private string _endpoint => _settings["Assistant:Endpoint"] is { Length: > 0 } e ? e : "https://api.anthropic.com/v1/messages";

    public ClaudeAssistantLlm(IConnectionSettings settings) { _settings = settings; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

    public async Task<string?> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default, int maxTokens = 700)
    {
        if (!IsConfigured)
        {
            return null;
        }

        var payload = JsonSerializer.Serialize(new
        {
            model = _model,
            max_tokens = maxTokens,
            system = systemPrompt,
            messages = new[] { new { role = "user", content = userPrompt } }
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-api-key", _apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");

        using var response = await Http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        if (doc.RootElement.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
        {
            var sb = new StringBuilder();
            foreach (var part in content.EnumerateArray())
            {
                if (part.TryGetProperty("text", out var text))
                {
                    sb.Append(text.GetString());
                }
            }
            return sb.Length == 0 ? null : sb.ToString();
        }

        return null;
    }
}
