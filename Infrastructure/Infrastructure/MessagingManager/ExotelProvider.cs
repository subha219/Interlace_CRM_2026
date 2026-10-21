using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Application.Features.MessagingManager;
using Application.Features.TelephonyManager;
using Application.Common.Services.IntegrationManager;

namespace Infrastructure.MessagingManager;

/// <summary>
/// Exotel click-to-call: Exotel rings the agent first, then the customer. Settings: Exotel:AccountSid, Exotel:ApiKey,
/// Exotel:ApiToken, Exotel:CallerId (the ExoPhone), optional Exotel:Subdomain (default api.exotel.com).
/// </summary>
public class ExotelCallProvider : ICallProvider
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly IConnectionSettings _c;
    private string? _sid => _c["Exotel:AccountSid"];
    private string? _key => _c["Exotel:ApiKey"];
    private string? _token => _c["Exotel:ApiToken"];
    private string? _callerId => _c["Exotel:CallerId"];
    private string? _host => string.IsNullOrWhiteSpace(_c["Exotel:Subdomain"]) ? "api.exotel.com" : _c["Exotel:Subdomain"]!.Trim();

    public ExotelCallProvider(IConnectionSettings c) { _c = c; }

    public bool IsConfigured => new[] { _sid, _key, _token, _callerId }.All(s => !string.IsNullOrWhiteSpace(s));

    public async Task<ChannelSendResult> StartAsync(string agentNumber, string customerNumber, string? statusCallbackUrl, CancellationToken cancellationToken)
    {
        if (!IsConfigured) return new ChannelSendResult(false, null, "Telephony (Exotel) is not configured.");
        // the host comes from settings; refuse anything that is not a plain host name so the API key is never sent elsewhere
        if (!System.Text.RegularExpressions.Regex.IsMatch(_host!, @"^[a-z0-9.-]+\.exotel\.com$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            return new ChannelSendResult(false, null, "Exotel:Subdomain must be an exotel.com host.");

        var form = new Dictionary<string, string>
        {
            ["From"] = "+" + agentNumber, ["To"] = "+" + customerNumber, ["CallerId"] = _callerId!, ["CallType"] = "trans"
        };
        if (!string.IsNullOrEmpty(statusCallbackUrl)) { form["StatusCallback"] = statusCallbackUrl; form["StatusCallbackEvents[0]"] = "terminal"; }

        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, $"https://{_host}/v1/Accounts/{Uri.EscapeDataString(_sid!)}/Calls/connect.json") { Content = new FormUrlEncodedContent(form) };
            message.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_key}:{_token}")));
            using var response = await Http.SendAsync(message, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            if (response.IsSuccessStatusCode && doc.RootElement.TryGetProperty("Call", out var call) && call.TryGetProperty("Sid", out var sid))
                return new ChannelSendResult(true, sid.GetString(), null);

            string? err = null;
            if (doc.RootElement.TryGetProperty("RestException", out var ex) && ex.TryGetProperty("Message", out var m)) err = m.GetString();
            return new ChannelSendResult(false, null, err ?? $"Exotel returned {(int)response.StatusCode}.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return new ChannelSendResult(false, null, "Could not reach Exotel: " + ex.Message);
        }
    }
}
