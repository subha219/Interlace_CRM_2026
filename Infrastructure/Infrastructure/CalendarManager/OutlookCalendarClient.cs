using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Application.Common.Services.IntegrationManager;

namespace Infrastructure.CalendarManager;

/// <summary>
/// Minimal Outlook / Microsoft 365 calendar client (OAuth 2 + Microsoft Graph events).
/// Uses the Microsoft application saved under Settings, Connections (Sso:Microsoft:*). The application needs the
/// delegated permission Calendars.ReadWrite and the redirect address /api/Calendar/OutlookCallback.
/// </summary>
public class OutlookCalendarClient
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };
    public const string Scope = "offline_access openid email profile Calendars.ReadWrite";
    private const string Graph = "https://graph.microsoft.com/v1.0/me/events";

    private readonly IConnectionSettings _settings;
    private string? _clientId => _settings["Sso:Microsoft:ClientId"];
    private string? _clientSecret => _settings["Sso:Microsoft:ClientSecret"];
    private string _tenant => Guid.TryParse(_settings["Sso:Microsoft:TenantId"], out var g) ? g.ToString() : "common";

    public OutlookCalendarClient(IConnectionSettings settings) { _settings = settings; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_clientId) && !string.IsNullOrWhiteSpace(_clientSecret);

    public string BuildAuthUrl(string redirectUri, string state) =>
        $"https://login.microsoftonline.com/{_tenant}/oauth2/v2.0/authorize" +
        $"?client_id={Uri.EscapeDataString(_clientId ?? "")}&redirect_uri={Uri.EscapeDataString(redirectUri)}&response_type=code&response_mode=query" +
        $"&scope={Uri.EscapeDataString(Scope)}&prompt=select_account&state={Uri.EscapeDataString(state)}";

    private async Task<JsonDocument> TokenCallAsync(Dictionary<string, string> form, CancellationToken ct)
    {
        form["scope"] = Scope;
        using var response = await Http.PostAsync($"https://login.microsoftonline.com/{_tenant}/oauth2/v2.0/token", new FormUrlEncodedContent(form), ct);
        var text = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            if (text.Contains("invalid_grant") || text.Contains("interaction_required")) throw new OutlookAuthException("Access was revoked or expired. Please connect again.");
            throw new Exception($"Microsoft token request failed ({(int)response.StatusCode}).");
        }
        return JsonDocument.Parse(text);
    }

    public async Task<(string RefreshToken, string? Email)> ExchangeCodeAsync(string code, string redirectUri, CancellationToken ct)
    {
        using var doc = await TokenCallAsync(new()
        {
            ["code"] = code, ["client_id"] = _clientId!, ["client_secret"] = _clientSecret!, ["redirect_uri"] = redirectUri, ["grant_type"] = "authorization_code"
        }, ct);
        var root = doc.RootElement;
        var refresh = root.TryGetProperty("refresh_token", out var r) ? r.GetString() : null;
        if (string.IsNullOrEmpty(refresh)) throw new Exception("Microsoft did not return offline access.");
        return (refresh, EmailFromIdToken(root.TryGetProperty("id_token", out var t) ? t.GetString() : null));
    }

    /// <summary>The id token comes straight from Microsoft over TLS in the exchange above, so reading its payload is enough here.</summary>
    public static string? EmailFromIdToken(string? idToken)
    {
        try
        {
            var parts = (idToken ?? "").Split('.');
            if (parts.Length < 2) return null;
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            using var doc = JsonDocument.Parse(Convert.FromBase64String(payload));
            foreach (var name in new[] { "email", "preferred_username" })
                if (doc.RootElement.TryGetProperty(name, out var e) && e.ValueKind == JsonValueKind.String) return e.GetString();
            return null;
        }
        catch { return null; }
    }

    /// <summary>Microsoft may hand back a new refresh token each time; the caller must store it when it does.</summary>
    public async Task<(string AccessToken, string? NewRefreshToken)> AccessTokenAsync(string refreshToken, CancellationToken ct)
    {
        using var doc = await TokenCallAsync(new()
        {
            ["refresh_token"] = refreshToken, ["client_id"] = _clientId!, ["client_secret"] = _clientSecret!, ["grant_type"] = "refresh_token"
        }, ct);
        var root = doc.RootElement;
        var next = root.TryGetProperty("refresh_token", out var r) ? r.GetString() : null;
        return (root.GetProperty("access_token").GetString()!, string.IsNullOrEmpty(next) || next == refreshToken ? null : next);
    }

    private static HttpRequestMessage Request(HttpMethod method, string url, string accessToken, object? body)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (body != null) req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        return req;
    }

    private static void ThrowIfAuth(HttpResponseMessage r)
    {
        if (r.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) throw new OutlookAuthException("Microsoft refused access. Please connect again.");
    }

    /// <summary>Creates the event, or updates it when an id is given (and creates it again if it was deleted in Outlook).</summary>
    public async Task<string> UpsertEventAsync(string accessToken, string? eventId, object body, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(eventId))
        {
            using var patch = await Http.SendAsync(Request(HttpMethod.Patch, $"{Graph}/{Uri.EscapeDataString(eventId)}", accessToken, body), ct);
            ThrowIfAuth(patch);
            if (patch.IsSuccessStatusCode) return eventId;
            if (patch.StatusCode != HttpStatusCode.NotFound && patch.StatusCode != HttpStatusCode.Gone) throw new Exception($"Outlook calendar update failed ({(int)patch.StatusCode}).");
        }
        using var post = await Http.SendAsync(Request(HttpMethod.Post, Graph, accessToken, body), ct);
        ThrowIfAuth(post);
        var text = await post.Content.ReadAsStringAsync(ct);
        if (!post.IsSuccessStatusCode) throw new Exception($"Outlook calendar create failed ({(int)post.StatusCode}).");
        using var doc = JsonDocument.Parse(text);
        return doc.RootElement.GetProperty("id").GetString()!;
    }

    public async Task DeleteEventAsync(string accessToken, string eventId, CancellationToken ct)
    {
        using var del = await Http.SendAsync(Request(HttpMethod.Delete, $"{Graph}/{Uri.EscapeDataString(eventId)}", accessToken, null), ct);
        ThrowIfAuth(del);
        if (!del.IsSuccessStatusCode && del.StatusCode != HttpStatusCode.NotFound && del.StatusCode != HttpStatusCode.Gone)
            throw new Exception($"Outlook calendar delete failed ({(int)del.StatusCode}).");
    }
}

public class OutlookAuthException : GoogleAuthException { public OutlookAuthException(string message) : base(message) { } }
