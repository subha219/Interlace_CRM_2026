using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Application.Features.MessagingManager;
using Application.Common.Services.IntegrationManager;

namespace Infrastructure.MessagingManager;

/// <summary>WhatsApp Cloud API. Settings: WhatsApp:PhoneNumberId, WhatsApp:AccessToken (plus VerifyToken and AppSecret for the webhook).</summary>
public class WhatsAppCloudProvider : IChannelProvider
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly IConnectionSettings _settings;
    private string? _phoneId => _settings["WhatsApp:PhoneNumberId"];
    private string? _token => _settings["WhatsApp:AccessToken"];

    public WhatsAppCloudProvider(IConnectionSettings settings) { _settings = settings; }

    public string Channel => ChannelRules.WhatsApp;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_phoneId) && !string.IsNullOrWhiteSpace(_token);

    public async Task<ChannelSendResult> SendAsync(ChannelSendRequest request, CancellationToken cancellationToken)
    {
        if (!IsConfigured) return new ChannelSendResult(false, null, "WhatsApp is not configured.");

        object payload;
        if (!string.IsNullOrEmpty(request.ProviderTemplateId))
        {
            var components = request.Variables.Count == 0 ? Array.Empty<object>() : new object[]
            {
                new { type = "body", parameters = request.Variables.Select(v => new { type = "text", text = v.Value }).ToArray() }
            };
            payload = new
            {
                messaging_product = "whatsapp", to = request.To, type = "template",
                template = new { name = request.ProviderTemplateId, language = new { code = string.IsNullOrWhiteSpace(request.Language) ? "en" : request.Language }, components }
            };
        }
        else
        {
            payload = new { messaging_product = "whatsapp", to = request.To, type = "text", text = new { body = request.Text, preview_url = false } };
        }

        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, $"https://graph.facebook.com/v21.0/{_phoneId}/messages")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
            using var response = await Http.SendAsync(message, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            if (response.IsSuccessStatusCode && doc.RootElement.TryGetProperty("messages", out var msgs) && msgs.GetArrayLength() > 0)
                return new ChannelSendResult(true, msgs[0].GetProperty("id").GetString(), null);

            var err = doc.RootElement.TryGetProperty("error", out var e) && e.TryGetProperty("message", out var m) ? m.GetString() : $"WhatsApp returned {(int)response.StatusCode}.";
            return new ChannelSendResult(false, null, err);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return new ChannelSendResult(false, null, "Could not reach WhatsApp: " + ex.Message);
        }
    }
}

/// <summary>MSG91 SMS through a DLT-registered flow template. Setting: Msg91:AuthKey.</summary>
public class Msg91SmsProvider : IChannelProvider
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly IConnectionSettings _settings;
    private string? _authKey => _settings["Msg91:AuthKey"];

    public Msg91SmsProvider(IConnectionSettings settings) { _settings = settings; }

    public string Channel => ChannelRules.Sms;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_authKey);

    public async Task<ChannelSendResult> SendAsync(ChannelSendRequest request, CancellationToken cancellationToken)
    {
        if (!IsConfigured) return new ChannelSendResult(false, null, "SMS (MSG91) is not configured.");
        if (string.IsNullOrEmpty(request.ProviderTemplateId)) return new ChannelSendResult(false, null, "SMS needs a template.");

        var recipient = new Dictionary<string, string> { ["mobiles"] = request.To };
        foreach (var v in request.Variables) recipient[v.Key] = v.Value;
        var payload = new { template_id = request.ProviderTemplateId, short_url = "0", recipients = new[] { recipient } };

        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, "https://control.msg91.com/api/v5/flow/")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            message.Headers.Add("authkey", _authKey);
            using var response = await Http.SendAsync(message, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            var type = doc.RootElement.TryGetProperty("type", out var t) ? t.GetString() : null;
            var text = doc.RootElement.TryGetProperty("message", out var m) ? m.GetString() : null;
            return response.IsSuccessStatusCode && string.Equals(type, "success", StringComparison.OrdinalIgnoreCase)
                ? new ChannelSendResult(true, text, null)
                : new ChannelSendResult(false, null, text ?? $"MSG91 returned {(int)response.StatusCode}.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return new ChannelSendResult(false, null, "Could not reach MSG91: " + ex.Message);
        }
    }
}
