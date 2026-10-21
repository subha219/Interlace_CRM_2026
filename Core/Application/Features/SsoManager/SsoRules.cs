using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Application.Features.SsoManager;

public record IdClaims(string? Iss, List<string> Aud, long Exp, string? Nonce, string? Email, bool EmailVerified, string? GivenName, string? FamilyName, string? Name, string? Tid);

public static class SsoRules
{
    public static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    public static string NewVerifier() => Base64Url(RandomNumberGenerator.GetBytes(32));
    public static string Challenge(string verifier) => Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
    public static string NewRandom() => Base64Url(RandomNumberGenerator.GetBytes(24));
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    public static bool FixedEquals(string? a, string? b) =>
        !string.IsNullOrEmpty(a) && !string.IsNullOrEmpty(b) && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));

    public static List<string> Domains(string? csv) =>
        (csv ?? "").Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(d => d.TrimStart('@').ToLowerInvariant()).Where(d => d.Length > 0).Distinct().ToList();

    /// <summary>Fails closed: with no domains configured nobody may sign in through SSO.</summary>
    public static bool DomainAllowed(string? email, string? allowedCsv)
    {
        var allowed = Domains(allowedCsv);
        var at = (email ?? "").LastIndexOf('@');
        if (allowed.Count == 0 || at < 1) return false;
        return allowed.Contains(email![(at + 1)..].ToLowerInvariant());
    }

    /// <summary>
    /// Checks an ID token that came straight from the provider's token endpoint over TLS (OpenID Connect Core 3.1.3.7 allows
    /// TLS server validation instead of a signature check). Returns an error text, or null when everything is in order.
    /// </summary>
    public static string? Validate(IdClaims? c, IEnumerable<string> issuers, string clientId, string nonce, DateTime nowUtc, bool requireVerifiedEmail, string? tenantId = null)
    {
        if (c == null) return "The sign-in response could not be read.";
        if (c.Iss == null || !issuers.Contains(c.Iss)) return "The sign-in response came from an unexpected issuer.";
        if (!c.Aud.Contains(clientId)) return "The sign-in response was meant for another application.";
        if (DateTimeOffset.FromUnixTimeSeconds(c.Exp).UtcDateTime.AddSeconds(60) < nowUtc) return "The sign-in response has expired.";
        if (!FixedEquals(c.Nonce, nonce)) return "The sign-in response does not match this request.";
        if (string.IsNullOrWhiteSpace(c.Email) || !c.Email.Contains('@')) return "The provider did not share an e-mail address.";
        if (requireVerifiedEmail && !c.EmailVerified) return "The e-mail address is not verified with the provider.";
        if (!string.IsNullOrEmpty(tenantId) && !string.Equals(c.Tid, tenantId, StringComparison.OrdinalIgnoreCase)) return "The account belongs to another organisation.";
        return null;
    }

    public static IdClaims? ParseIdToken(string? idToken)
    {
        try
        {
            var parts = (idToken ?? "").Split('.');
            if (parts.Length < 2) return null;
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            using var doc = JsonDocument.Parse(Convert.FromBase64String(payload));
            var r = doc.RootElement;
            string? S(string n) => r.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

            var aud = new List<string>();
            if (r.TryGetProperty("aud", out var a))
            {
                if (a.ValueKind == JsonValueKind.String) aud.Add(a.GetString()!);
                else if (a.ValueKind == JsonValueKind.Array) aud.AddRange(a.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!));
            }
            var verified = r.TryGetProperty("email_verified", out var ev) && (ev.ValueKind == JsonValueKind.True || (ev.ValueKind == JsonValueKind.String && ev.GetString() == "true"));
            var exp = r.TryGetProperty("exp", out var e) && e.ValueKind == JsonValueKind.Number ? e.GetInt64() : 0;
            var email = S("email");
            if (string.IsNullOrWhiteSpace(email) && (S("preferred_username") ?? "").Contains('@')) email = S("preferred_username");
            return new IdClaims(S("iss"), aud, exp, S("nonce"), email?.Trim().ToLowerInvariant(), verified, S("given_name"), S("family_name"), S("name"), S("tid"));
        }
        catch { return null; }
    }

    public static (string First, string Last) SplitName(IdClaims c)
    {
        var first = (c.GivenName ?? "").Trim();
        var last = (c.FamilyName ?? "").Trim();
        if (first.Length == 0 && last.Length == 0)
        {
            var parts = (c.Name ?? "").Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0) first = parts[0];
            if (parts.Length > 1) last = parts[1];
        }
        if (first.Length == 0) first = (c.Email ?? "user").Split('@')[0];
        return (first.Length > 100 ? first[..100] : first, last.Length > 100 ? last[..100] : last);
    }
}
