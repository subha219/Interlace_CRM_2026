using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Application.Common.Services.IntegrationManager;

namespace Infrastructure.CalendarManager;

public class GoogleAuthException : Exception { public GoogleAuthException(string message) : base(message) { } }

/// <summary>
/// Minimal Google Calendar client (OAuth 2 + events). Settings: Google:ClientId, Google:ClientSecret.
/// The redirect URI to register in Google Cloud is shown on the "Google Calendar" page.
/// </summary>
public class GoogleCalendarClient
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };
    public const string Scope = "openid email https://www.googleapis.com/auth/calendar.events";

    private readonly IConnectionSettings _settings;
    private string? _clientId => _settings["Google:ClientId"];
    private string? _clientSecret => _settings["Google:ClientSecret"];

    public GoogleCalendarClient(IConnectionSettings settings)
    {
        _settings = settings;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_clientId) && !string.IsNullOrWhiteSpace(_clientSecret);

    public string BuildAuthUrl(string redirectUri, string state) => BuildAuthUrl(redirectUri, state, Scope);

    public string BuildAuthUrl(string redirectUri, string state, string scope) =>
        "https://accounts.google.com/o/oauth2/v2/auth" +
        $"?client_id={Uri.EscapeDataString(_clientId ?? "")}&redirect_uri={Uri.EscapeDataString(redirectUri)}&response_type=code" +
        $"&scope={Uri.EscapeDataString(scope)}&access_type=offline&prompt=consent&state={Uri.EscapeDataString(state)}";

    private async Task<JsonDocument> TokenCallAsync(Dictionary<string, string> form, CancellationToken ct)
    {
        using var response = await Http.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(form), ct);
        var text = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            if (text.Contains("invalid_grant")) throw new GoogleAuthException("Access was revoked or expired. Please connect again.");
            throw new Exception($"Google token request failed ({(int)response.StatusCode}).");
        }
        return JsonDocument.Parse(text);
    }

    /// <summary>Trades the one-time code for a refresh token and reads the account e-mail from the id token.</summary>
    public async Task<(string RefreshToken, string? Email)> ExchangeCodeAsync(string code, string redirectUri, CancellationToken ct)
    {
        using var doc = await TokenCallAsync(new()
        {
            ["code"] = code, ["client_id"] = _clientId!, ["client_secret"] = _clientSecret!, ["redirect_uri"] = redirectUri, ["grant_type"] = "authorization_code"
        }, ct);
        var root = doc.RootElement;
        var refresh = root.TryGetProperty("refresh_token", out var r) ? r.GetString() : null;
        if (string.IsNullOrEmpty(refresh)) throw new Exception("Google did not return offline access. Remove the app under your Google account permissions and connect again.");
        return (refresh, EmailFromIdToken(root.TryGetProperty("id_token", out var t) ? t.GetString() : null));
    }

    /// <summary>The id token comes straight from Google over TLS in the exchange above, so reading its payload is enough here.</summary>
    public static string? EmailFromIdToken(string? idToken)
    {
        try
        {
            var parts = (idToken ?? "").Split('.');
            if (parts.Length < 2) return null;
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            using var doc = JsonDocument.Parse(Convert.FromBase64String(payload));
            return doc.RootElement.TryGetProperty("email", out var e) ? e.GetString() : null;
        }
        catch { return null; }
    }

    public async Task<string> AccessTokenAsync(string refreshToken, CancellationToken ct)
    {
        using var doc = await TokenCallAsync(new()
        {
            ["refresh_token"] = refreshToken, ["client_id"] = _clientId!, ["client_secret"] = _clientSecret!, ["grant_type"] = "refresh_token"
        }, ct);
        return doc.RootElement.GetProperty("access_token").GetString()!;
    }

    /// <summary>GET a Google API url and read the JSON. Null when it is not found.</summary>
    public async Task<JsonDocument?> GetJsonAsync(string accessToken, string url, CancellationToken ct)
    {
        using var response = await Http.SendAsync(Request(HttpMethod.Get, url, accessToken, null), ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) throw new GoogleAuthException("Google refused access. Please connect again.");
        if (!response.IsSuccessStatusCode) throw new Exception($"Google request failed ({(int)response.StatusCode}).");
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
    }

    private static HttpRequestMessage Request(HttpMethod method, string url, string accessToken, object? body)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (body != null) req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        return req;
    }

    /// <summary>Creates the event, or updates it when an id is given (and creates it again if it was deleted in Google).</summary>
    public async Task<string> UpsertEventAsync(string accessToken, string? eventId, object body, CancellationToken ct)
    {
        const string baseUrl = "https://www.googleapis.com/calendar/v3/calendars/primary/events";
        if (!string.IsNullOrEmpty(eventId))
        {
            using var put = await Http.SendAsync(Request(HttpMethod.Put, $"{baseUrl}/{Uri.EscapeDataString(eventId)}", accessToken, body), ct);
            if (put.IsSuccessStatusCode) return eventId;
            if (put.StatusCode != HttpStatusCode.NotFound && put.StatusCode != HttpStatusCode.Gone) throw new Exception($"Google calendar update failed ({(int)put.StatusCode}).");
        }
        using var post = await Http.SendAsync(Request(HttpMethod.Post, baseUrl, accessToken, body), ct);
        var text = await post.Content.ReadAsStringAsync(ct);
        if (!post.IsSuccessStatusCode) throw new Exception($"Google calendar create failed ({(int)post.StatusCode}).");
        using var doc = JsonDocument.Parse(text);
        return doc.RootElement.GetProperty("id").GetString()!;
    }

    public async Task DeleteEventAsync(string accessToken, string eventId, CancellationToken ct)
    {
        using var del = await Http.SendAsync(Request(HttpMethod.Delete, $"https://www.googleapis.com/calendar/v3/calendars/primary/events/{Uri.EscapeDataString(eventId)}", accessToken, null), ct);
        if (!del.IsSuccessStatusCode && del.StatusCode != HttpStatusCode.NotFound && del.StatusCode != HttpStatusCode.Gone)
            throw new Exception($"Google calendar delete failed ({(int)del.StatusCode}).");
    }
}
