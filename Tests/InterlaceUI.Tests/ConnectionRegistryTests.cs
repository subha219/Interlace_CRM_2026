using Application.Features.ConnectionManager;

namespace InterlaceUI.Tests;

public class ConnectionRegistryTests
{
    private static string? Check(string key, string? value) => ConnectionRegistry.Validate(ConnectionRegistry.Find(key)!, value);

    [Fact]
    public void Keys_are_unique_and_known()
    {
        var keys = ConnectionRegistry.Groups.SelectMany(g => g.Fields).Select(f => f.Key).ToList();
        Assert.Equal(keys.Count, keys.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(keys, k => Assert.NotNull(ConnectionRegistry.Find(k)));
        Assert.Null(ConnectionRegistry.Find("Nope:Key"));
    }

    [Theory]
    [InlineData("Google:ClientSecret")]
    [InlineData("Sso:Google:ClientSecret")]
    [InlineData("Sso:Microsoft:ClientSecret")]
    [InlineData("WhatsApp:AccessToken")]
    [InlineData("WhatsApp:VerifyToken")]
    [InlineData("WhatsApp:AppSecret")]
    [InlineData("Msg91:AuthKey")]
    [InlineData("Exotel:ApiToken")]
    [InlineData("Exotel:WebhookKey")]
    [InlineData("Assistant:ApiKey")]
    public void Secrets_are_marked_secret(string key) => Assert.True(ConnectionRegistry.Find(key)!.Secret);

    [Theory]
    [InlineData("Google:ClientId")]
    [InlineData("WhatsApp:PhoneNumberId")]
    [InlineData("Sso:AllowedDomains")]
    public void Plain_settings_are_not_secret(string key) => Assert.False(ConnectionRegistry.Find(key)!.Secret);

    [Theory]
    [InlineData("Exotel:Subdomain", "api.exotel.com", true)]
    [InlineData("Exotel:Subdomain", "evil.com", false)]
    [InlineData("Exotel:Subdomain", "api.exotel.com.evil.com", false)]
    [InlineData("Sso:Microsoft:TenantId", "11111111-2222-3333-4444-555555555555", true)]
    [InlineData("Sso:Microsoft:TenantId", "common", false)]
    [InlineData("Sso:DisablePasswordLogin", "true", true)]
    [InlineData("Sso:DisablePasswordLogin", "yes", false)]
    [InlineData("Sso:AllowedDomains", "acme.com, partner.org", true)]
    [InlineData("Sso:AllowedDomains", "@acme.com", true)]
    [InlineData("Sso:AllowedDomains", "acme", false)]
    [InlineData("Sso:AllowedDomains", "*", false)]
    [InlineData("Assistant:Endpoint", "https://api.anthropic.com/v1/messages", true)]
    [InlineData("Assistant:Endpoint", "http://api.anthropic.com/v1/messages", false)]
    [InlineData("Assistant:Endpoint", "ftp://x", false)]
    [InlineData("Exotel:CallbackBaseUrl", "https://crm.acme.com", true)]
    [InlineData("Exotel:CallbackBaseUrl", "crm.acme.com", false)]
    [InlineData("Assistant:Model", "claude-sonnet-5-5", true)]
    [InlineData("Assistant:Model", "model with spaces", false)]
    public void Values_are_checked(string key, string value, bool ok) => Assert.Equal(ok, Check(key, value) == null);

    [Fact]
    public void Empty_is_allowed_and_long_or_control_characters_are_not()
    {
        Assert.Null(Check("Google:ClientId", ""));
        Assert.Null(Check("Google:ClientId", null));
        Assert.NotNull(Check("Google:ClientId", new string('a', 501)));
        Assert.NotNull(Check("Google:ClientId", "a\nb"));
    }

    private static Func<string, string?> Settings(params (string, string)[] pairs)
    {
        var d = pairs.ToDictionary(p => p.Item1, p => p.Item2);
        return k => d.GetValueOrDefault(k);
    }

    [Fact]
    public void Password_login_cannot_be_turned_off_without_working_sso()
    {
        Assert.Null(ConnectionRegistry.LockoutProblem(Settings(("Sso:DisablePasswordLogin", "false"))));
        Assert.NotNull(ConnectionRegistry.LockoutProblem(Settings(("Sso:DisablePasswordLogin", "true"))));
        Assert.NotNull(ConnectionRegistry.LockoutProblem(Settings(("Sso:DisablePasswordLogin", "true"), ("Sso:AllowedDomains", "acme.com"))));
        Assert.NotNull(ConnectionRegistry.LockoutProblem(Settings(("Sso:DisablePasswordLogin", "true"), ("Google:ClientId", "a"), ("Google:ClientSecret", "b"))));
        Assert.Null(ConnectionRegistry.LockoutProblem(Settings(("Sso:DisablePasswordLogin", "true"), ("Sso:AllowedDomains", "acme.com"), ("Google:ClientId", "a"), ("Google:ClientSecret", "b"))));
        Assert.NotNull(ConnectionRegistry.LockoutProblem(Settings(("Sso:DisablePasswordLogin", "true"), ("Sso:AllowedDomains", "acme.com"), ("Sso:Microsoft:ClientId", "a"), ("Sso:Microsoft:ClientSecret", "b"), ("Sso:Microsoft:TenantId", "common"))));
        Assert.Null(ConnectionRegistry.LockoutProblem(Settings(("Sso:DisablePasswordLogin", "true"), ("Sso:AllowedDomains", "acme.com"), ("Sso:Microsoft:ClientId", "a"), ("Sso:Microsoft:ClientSecret", "b"), ("Sso:Microsoft:TenantId", "11111111-2222-3333-4444-555555555555"))));
    }
}
