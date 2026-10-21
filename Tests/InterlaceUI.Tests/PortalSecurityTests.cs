using Application.Features.PortalManager;

namespace InterlaceUI.Tests;

public class PortalSecurityTests
{
    [Fact]
    public void Tokens_are_random_and_only_their_hash_is_comparable()
    {
        var a = PortalSecurity.NewToken();
        var b = PortalSecurity.NewToken();
        Assert.NotEqual(a, b);
        Assert.Equal(PortalSecurity.Hash(a), PortalSecurity.Hash(a));
        Assert.NotEqual(a, PortalSecurity.Hash(a));
    }

    [Fact]
    public void A_login_link_works_once_and_only_before_it_expires()
    {
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        Assert.True(PortalSecurity.LoginUsable(null, now.AddMinutes(10), now));
        Assert.False(PortalSecurity.LoginUsable(now.AddMinutes(-1), now.AddMinutes(10), now));
        Assert.False(PortalSecurity.LoginUsable(null, now.AddMinutes(-1), now));
        Assert.False(PortalSecurity.LoginUsable(null, null, now));
    }

    [Fact]
    public void Sessions_end_at_their_expiry()
    {
        var now = DateTime.UtcNow;
        Assert.True(PortalSecurity.SessionUsable(now.AddHours(1), now));
        Assert.False(PortalSecurity.SessionUsable(now, now));
        Assert.False(PortalSecurity.SessionUsable(null, now));
    }
}
