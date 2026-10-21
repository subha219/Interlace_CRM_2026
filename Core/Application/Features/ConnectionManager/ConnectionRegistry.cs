using System.Text.RegularExpressions;

namespace Application.Features.ConnectionManager;

public record ConnectionField(string Key, string Label, bool Secret = false, string Kind = "text", string? Hint = null);
public record ConnectionGroup(string Id, string Title, string Help, List<ConnectionField> Fields);

/// <summary>The settings an administrator may save, with the rules each value must follow.</summary>
public static class ConnectionRegistry
{
    public const int MaxLength = 500;

    public static readonly IReadOnlyList<ConnectionGroup> Groups = new List<ConnectionGroup>
    {
        new("google", "Google (Calendar and Gmail)",
            "Create an OAuth client of type Web application in Google Cloud, enable the Calendar API and the Gmail API, and register the redirect addresses shown below.",
            new() { new("Google:ClientId", "Client ID"), new("Google:ClientSecret", "Client secret", true) }),
        new("sso", "Single sign-on",
            "Lets staff sign in with Google or Microsoft. Only e-mail addresses from the allowed domains can sign in. Google falls back to the client above when no separate one is given.",
            new()
            {
                new("Sso:AllowedDomains", "Allowed e-mail domains", false, "text", "For example: yourcompany.com, partner.org (comma separated)"),
                new("Sso:Google:ClientId", "Google client ID (optional, if different)"), new("Sso:Google:ClientSecret", "Google client secret (optional)", true),
                new("Sso:Microsoft:ClientId", "Microsoft application (client) ID"), new("Sso:Microsoft:ClientSecret", "Microsoft client secret", true),
                new("Sso:Microsoft:TenantId", "Microsoft directory (tenant) ID", false, "text", "A GUID. Required, so that only your own organisation can sign in."),
                new("Sso:DisablePasswordLogin", "Turn off password sign-in", false, "bool", "Only works once single sign-on is set up, so nobody is locked out."),
            }),
        new("whatsapp", "WhatsApp Business (Cloud API)",
            "From Meta for Developers. Register the webhook address shown below and use the verify token you enter here.",
            new()
            {
                new("WhatsApp:PhoneNumberId", "Phone number ID"), new("WhatsApp:AccessToken", "Access token", true),
                new("WhatsApp:VerifyToken", "Webhook verify token", true, "secret", "Any long random text. Enter the same text at Meta."),
                new("WhatsApp:AppSecret", "App secret", true, "secret", "Used to check that incoming messages really come from Meta."),
            }),
        new("sms", "SMS (MSG91)", "Your MSG91 authentication key. Messages are sent through DLT-registered templates.",
            new() { new("Msg91:AuthKey", "Auth key", true) }),
        new("telephony", "Calls (Exotel)",
            "Click-to-call: Exotel rings the agent first, then the customer. Add the status address shown below at Exotel if you want call results.",
            new()
            {
                new("Exotel:AccountSid", "Account SID"), new("Exotel:ApiKey", "API key"), new("Exotel:ApiToken", "API token", true),
                new("Exotel:CallerId", "Caller ID (ExoPhone number)"),
                new("Exotel:Subdomain", "API host (optional)", false, "text", "Default api.exotel.com. Must end in .exotel.com."),
                new("Exotel:WebhookKey", "Status callback key", true, "secret", "Any long random text. It becomes part of the status address."),
                new("Exotel:CallbackBaseUrl", "Public address of this CRM (optional)", false, "text", "For example https://crm.yourcompany.com. Used when the CRM sits behind a proxy."),
            }),
        new("ai", "AI model",
            "An Anthropic API key switches on the assistant and the AI features. Without it they use simple built-in rules and nothing leaves the server.",
            new()
            {
                new("Assistant:ApiKey", "API key", true), new("Assistant:Model", "Model (optional)", false, "text", "Leave empty for the default."),
                new("Assistant:Endpoint", "API address (optional)", false, "text", "Leave empty for the default. Must start with https://."),
            }),
    };

    public static ConnectionField? Find(string? key) => Groups.SelectMany(g => g.Fields).FirstOrDefault(f => string.Equals(f.Key, key, StringComparison.OrdinalIgnoreCase));

    private static readonly Regex DomainRx = new(@"^[a-z0-9]([a-z0-9-]*[a-z0-9])?(\.[a-z0-9]([a-z0-9-]*[a-z0-9])?)*\.[a-z]{2,}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ExotelHostRx = new(@"^[a-z0-9.-]+\.exotel\.com$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ModelRx = new(@"^[A-Za-z0-9._:-]{1,100}$", RegexOptions.Compiled);

    /// <summary>Returns what is wrong with the value, or null when it is acceptable. Empty is always acceptable (it removes the saved value).</summary>
    public static string? Validate(ConnectionField field, string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        if (value.Length > MaxLength) return $"{field.Label}: too long (at most {MaxLength} characters).";
        if (value.Any(char.IsControl)) return $"{field.Label}: contains characters that are not allowed.";
        switch (field.Key)
        {
            case "Exotel:Subdomain":
                return ExotelHostRx.IsMatch(value) ? null : $"{field.Label}: must be a host name ending in .exotel.com.";
            case "Sso:Microsoft:TenantId":
                return Guid.TryParse(value, out _) ? null : $"{field.Label}: must be a GUID such as 11111111-2222-3333-4444-555555555555.";
            case "Sso:DisablePasswordLogin":
                return value is "true" or "false" ? null : $"{field.Label}: must be true or false.";
            case "Sso:AllowedDomains":
                return ParseDomains(value).All(d => DomainRx.IsMatch(d)) && ParseDomains(value).Count > 0 ? null : $"{field.Label}: use plain domains such as yourcompany.com, separated by commas.";
            case "Assistant:Endpoint":
                return Uri.TryCreate(value, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps ? null : $"{field.Label}: must be an https:// address.";
            case "Exotel:CallbackBaseUrl":
                return Uri.TryCreate(value, UriKind.Absolute, out var b) && (b.Scheme == Uri.UriSchemeHttps || b.Scheme == Uri.UriSchemeHttp) ? null : $"{field.Label}: must be a full web address.";
            case "Assistant:Model":
                return ModelRx.IsMatch(value) ? null : $"{field.Label}: only letters, digits and . _ : - are allowed.";
        }
        return null;
    }

    public static List<string> ParseDomains(string? value) =>
        (value ?? "").Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(d => d.Trim().TrimStart('@')).Where(d => d.Length > 0).ToList();

    /// <summary>Turning off password sign-in without a working single sign-on would lock everyone out.</summary>
    public static string? LockoutProblem(Func<string, string?> effective)
    {
        if (!string.Equals(effective("Sso:DisablePasswordLogin"), "true", StringComparison.OrdinalIgnoreCase)) return null;
        bool Has(params string[] keys) => keys.All(k => !string.IsNullOrWhiteSpace(effective(k)));
        var google = Has("Sso:Google:ClientId", "Sso:Google:ClientSecret") || Has("Google:ClientId", "Google:ClientSecret");
        var microsoft = Has("Sso:Microsoft:ClientId", "Sso:Microsoft:ClientSecret") && Guid.TryParse(effective("Sso:Microsoft:TenantId"), out _);
        if (ParseDomains(effective("Sso:AllowedDomains")).Count == 0 || !(google || microsoft))
            return "Password sign-in can only be turned off once single sign-on works: set the allowed domains and a complete Google or Microsoft client first.";
        return null;
    }
}
