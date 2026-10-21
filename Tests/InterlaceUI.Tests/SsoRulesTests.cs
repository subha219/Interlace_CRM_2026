using System.Text;
using System.Text.Json;
using Application.Features.SsoManager;

namespace InterlaceUI.Tests;

public class SsoRulesTests
{
    private static readonly DateTime Now = new(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc);
    private static long Unix(DateTime d) => new DateTimeOffset(d).ToUnixTimeSeconds();
    private static readonly string[] Issuers = { "https://accounts.google.com" };

    private static IdClaims Good() => new("https://accounts.google.com", new() { "cid" }, Unix(Now.AddMinutes(30)), "n1", "asha@acme.com", true, "Asha", "Rao", "Asha Rao", null);

    private static string Token(object payload)
    {
        string B(string s) => Convert.ToBase64String(Encoding.UTF8.GetBytes(s)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return B("{\"alg\":\"none\"}") + "." + B(JsonSerializer.Serialize(payload)) + ".sig";
    }

    [Fact]
    public void A_good_token_passes() => Assert.Null(SsoRules.Validate(Good(), Issuers, "cid", "n1", Now, true));

    [Fact]
    public void Wrong_issuer_audience_nonce_or_expiry_are_refused()
    {
        Assert.NotNull(SsoRules.Validate(Good() with { Iss = "https://evil.example" }, Issuers, "cid", "n1", Now, true));
        Assert.NotNull(SsoRules.Validate(Good() with { Aud = new() { "other" } }, Issuers, "cid", "n1", Now, true));
        Assert.NotNull(SsoRules.Validate(Good(), Issuers, "cid", "different", Now, true));
        Assert.NotNull(SsoRules.Validate(Good() with { Exp = Unix(Now.AddMinutes(-5)) }, Issuers, "cid", "n1", Now, true));
        Assert.NotNull(SsoRules.Validate(Good() with { Nonce = null }, Issuers, "cid", "n1", Now, true));
        Assert.NotNull(SsoRules.Validate(null, Issuers, "cid", "n1", Now, true));
    }

    [Fact]
    public void Unverified_or_missing_email_is_refused_when_required()
    {
        Assert.NotNull(SsoRules.Validate(Good() with { EmailVerified = false }, Issuers, "cid", "n1", Now, true));
        Assert.Null(SsoRules.Validate(Good() with { EmailVerified = false }, Issuers, "cid", "n1", Now, false));
        Assert.NotNull(SsoRules.Validate(Good() with { Email = null }, Issuers, "cid", "n1", Now, false));
    }

    [Fact]
    public void Tenant_must_match_when_given()
    {
        Assert.Null(SsoRules.Validate(Good() with { Tid = "T1" }, Issuers, "cid", "n1", Now, true, "t1"));
        Assert.NotNull(SsoRules.Validate(Good() with { Tid = "T2" }, Issuers, "cid", "n1", Now, true, "t1"));
        Assert.NotNull(SsoRules.Validate(Good(), Issuers, "cid", "n1", Now, true, "t1"));
    }

    [Theory]
    [InlineData("asha@acme.com", "acme.com", true)]
    [InlineData("asha@ACME.com", "@acme.com, other.org", true)]
    [InlineData("asha@sub.acme.com", "acme.com", false)]
    [InlineData("asha@evil.com", "acme.com", false)]
    [InlineData("asha@acme.com", "", false)]
    [InlineData("asha@acme.com", null, false)]
    [InlineData("no-at-sign", "acme.com", false)]
    public void Domain_allow_list_fails_closed(string email, string? allowed, bool ok) => Assert.Equal(ok, SsoRules.DomainAllowed(email, allowed));

    [Fact]
    public void Token_is_parsed()
    {
        var c = SsoRules.ParseIdToken(Token(new { iss = "https://accounts.google.com", aud = "cid", exp = Unix(Now), nonce = "n1", email = "Asha@Acme.com", email_verified = true, given_name = "Asha", family_name = "Rao" }));
        Assert.NotNull(c);
        Assert.Equal("asha@acme.com", c!.Email);
        Assert.True(c.EmailVerified);
        Assert.Equal(new[] { "cid" }, c.Aud);
    }

    [Fact]
    public void Microsoft_style_token_uses_preferred_username_and_audience_list()
    {
        var c = SsoRules.ParseIdToken(Token(new { iss = "x", aud = new[] { "a", "b" }, exp = 1, nonce = "n", preferred_username = "Bob@Acme.com", tid = "T" }));
        Assert.Equal("bob@acme.com", c!.Email);
        Assert.Equal(2, c.Aud.Count);
        Assert.False(c.EmailVerified);
    }

    [Fact]
    public void Garbage_tokens_give_null()
    {
        Assert.Null(SsoRules.ParseIdToken(null));
        Assert.Null(SsoRules.ParseIdToken("abc"));
        Assert.Null(SsoRules.ParseIdToken("a.b.c"));
    }

    [Fact]
    public void Pkce_challenge_matches_the_rfc_example() =>
        Assert.Equal("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM", SsoRules.Challenge("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk"));

    [Fact]
    public void Verifiers_are_random_and_long_enough()
    {
        var a = SsoRules.NewVerifier();
        Assert.NotEqual(a, SsoRules.NewVerifier());
        Assert.True(a.Length >= 43);
    }

    [Fact]
    public void Names_fall_back_sensibly()
    {
        Assert.Equal(("Asha", "Rao"), SsoRules.SplitName(Good()));
        Assert.Equal(("Asha", "Rao Nair"), SsoRules.SplitName(Good() with { GivenName = null, FamilyName = null, Name = "Asha Rao Nair" }));
        Assert.Equal(("asha", ""), SsoRules.SplitName(Good() with { GivenName = null, FamilyName = null, Name = null }));
    }
}
