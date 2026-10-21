using System.Text.Json;
using Application.Features.SsoManager;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Memory;
using Application.Common.Services.IntegrationManager;

namespace Infrastructure.SsoManager;

public class SsoException : Exception
{
    public string Code { get; }
    public SsoException(string code, string message) : base(message) { Code = code; }
}

public record SsoProviderInfo(string Id, string Name);
public record SsoIdentity(string Email, string FirstName, string LastName, string Provider);

/// <summary>
/// OpenID Connect sign-in (authorization code flow with PKCE, state and nonce). Providers: Google (Sso:Google:ClientId/ClientSecret,
/// falling back to Google:ClientId/ClientSecret) and Microsoft Entra (Sso:Microsoft:ClientId/ClientSecret/TenantId).
/// Only e-mail domains listed in Sso:AllowedDomains may sign in.
/// </summary>
public class SsoService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private record Def(string Id, string Name, string ClientId, string ClientSecret, string Authorize, string Token, string[] Issuers, bool RequireVerified, string? Tenant);

    private readonly IConnectionSettings _config;
    private readonly ITimeLimitedDataProtector _state;
    private readonly IMemoryCache _cache;

    public SsoService(IConnectionSettings config, IDataProtectionProvider provider, IMemoryCache cache)
    {
        _config = config;
        _state = provider.CreateProtector("Interlace.Sso.State").ToTimeLimitedDataProtector();
        _cache = cache;
    }

    private List<Def> Defs()
    {
        var list = new List<Def>();
        var gId = _config["Sso:Google:ClientId"] ?? _config["Google:ClientId"];
        var gSecret = _config["Sso:Google:ClientSecret"] ?? _config["Google:ClientSecret"];
        if (!string.IsNullOrWhiteSpace(gId) && !string.IsNullOrWhiteSpace(gSecret))
            list.Add(new Def("google", "Google", gId, gSecret, "https://accounts.google.com/o/oauth2/v2/auth", "https://oauth2.googleapis.com/token",
                new[] { "https://accounts.google.com", "accounts.google.com" }, true, null));

        var mId = _config["Sso:Microsoft:ClientId"];
        var mSecret = _config["Sso:Microsoft:ClientSecret"];
        var tenant = _config["Sso:Microsoft:TenantId"];
        // a tenant id is required: the "common" endpoint would let any Microsoft account in
        if (!string.IsNullOrWhiteSpace(mId) && !string.IsNullOrWhiteSpace(mSecret) && Guid.TryParse(tenant, out _))
            list.Add(new Def("microsoft", "Microsoft", mId, mSecret, $"https://login.microsoftonline.com/{tenant}/oauth2/v2.0/authorize",
                $"https://login.microsoftonline.com/{tenant}/oauth2/v2.0/token", new[] { $"https://login.microsoftonline.com/{tenant}/v2.0" }, false, tenant));
        return list;
    }

    public IReadOnlyList<SsoProviderInfo> Providers() =>
        string.IsNullOrWhiteSpace(_config["Sso:AllowedDomains"]) ? new List<SsoProviderInfo>() : Defs().Select(d => new SsoProviderInfo(d.Id, d.Name)).ToList();

    public bool PasswordLoginEnabled => !string.Equals(_config["Sso:DisablePasswordLogin"], "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>The address administrators register at Google and Microsoft.</summary>
    public static string RedirectUri(string scheme, string host) => $"{scheme}://{host}/api/Sso/Callback";

    public (string Url, string Bind) Start(string providerId, string redirectUri)
    {
        var def = Defs().FirstOrDefault(d => d.Id == providerId);
        if (def == null || Providers().All(p => p.Id != providerId)) throw new SsoException("notconfigured", "This sign-in method is not set up.");

        var bind = SsoRules.NewRandom();
        var nonce = SsoRules.NewRandom();
        var verifier = SsoRules.NewVerifier();
        var state = _state.Protect(JsonSerializer.Serialize(new { p = def.Id, n = nonce, v = verifier, b = SsoRules.Hash(bind) }), TimeSpan.FromMinutes(10));

        var url = def.Authorize +
            $"?client_id={Uri.EscapeDataString(def.ClientId)}&redirect_uri={Uri.EscapeDataString(redirectUri)}&response_type=code" +
            $"&scope={Uri.EscapeDataString("openid email profile")}&state={Uri.EscapeDataString(state)}&nonce={Uri.EscapeDataString(nonce)}" +
            $"&code_challenge={SsoRules.Challenge(verifier)}&code_challenge_method=S256&prompt=select_account";
        return (url, bind);
    }

    public async Task<SsoIdentity> CompleteAsync(string? code, string? state, string? bind, string redirectUri, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state)) throw new SsoException("denied", "Sign-in was cancelled.");

        string provider, nonce, verifier, bindHash;
        try
        {
            using var s = JsonDocument.Parse(_state.Unprotect(state));
            provider = s.RootElement.GetProperty("p").GetString()!;
            nonce = s.RootElement.GetProperty("n").GetString()!;
            verifier = s.RootElement.GetProperty("v").GetString()!;
            bindHash = s.RootElement.GetProperty("b").GetString()!;
        }
        catch { throw new SsoException("expired", "The sign-in took too long. Please try again."); }

        // the state must belong to the browser that started the sign-in (stops login-CSRF)
        if (string.IsNullOrEmpty(bind) || !SsoRules.FixedEquals(SsoRules.Hash(bind), bindHash)) throw new SsoException("expired", "The sign-in took too long. Please try again.");

        var def = Defs().FirstOrDefault(d => d.Id == provider) ?? throw new SsoException("notconfigured", "This sign-in method is not set up.");

        string? idToken;
        using (var response = await Http.PostAsync(def.Token, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = code, ["client_id"] = def.ClientId, ["client_secret"] = def.ClientSecret, ["redirect_uri"] = redirectUri,
            ["grant_type"] = "authorization_code", ["code_verifier"] = verifier
        }), ct))
        {
            var text = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode) throw new SsoException("failed", "The provider refused the sign-in.");
            using var doc = JsonDocument.Parse(text);
            idToken = doc.RootElement.TryGetProperty("id_token", out var t) ? t.GetString() : null;
        }

        var claims = SsoRules.ParseIdToken(idToken);
        var error = SsoRules.Validate(claims, def.Issuers, def.ClientId, nonce, DateTime.UtcNow, def.RequireVerified, def.Tenant);
        if (error != null) throw new SsoException("failed", error);
        if (!SsoRules.DomainAllowed(claims!.Email, _config["Sso:AllowedDomains"])) throw new SsoException("domain", "This e-mail domain is not allowed to sign in.");

        var (first, last) = SsoRules.SplitName(claims);
        return new SsoIdentity(claims.Email!, first, last, def.Name);
    }

    /// <summary>The browser gets a one-time code (valid 60 seconds) instead of tokens in the address bar.</summary>
    public string CreateHandoff(SsoIdentity identity)
    {
        var code = SsoRules.NewRandom();
        _cache.Set("sso-handoff:" + SsoRules.Hash(code), identity, TimeSpan.FromSeconds(60));
        return code;
    }

    public SsoIdentity? Redeem(string? code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length > 100) return null;
        var key = "sso-handoff:" + SsoRules.Hash(code);
        if (!_cache.TryGetValue(key, out SsoIdentity? identity)) return null;
        _cache.Remove(key);
        return identity;
    }
}
