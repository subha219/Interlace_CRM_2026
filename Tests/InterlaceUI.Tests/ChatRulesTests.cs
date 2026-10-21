using Application.Features.ChatManager;

namespace InterlaceUI.Tests;

public class ChatRulesTests
{
    [Fact]
    public void Tokens_are_random_and_hashed()
    {
        var a = ChatRules.NewToken();
        Assert.NotEqual(a, ChatRules.NewToken());
        Assert.True(a.Length >= 40);
        Assert.NotEqual(a, ChatRules.Hash(a));
        Assert.Equal(ChatRules.Hash(a), ChatRules.Hash(a));
    }

    [Fact]
    public void Clean_removes_control_characters_and_limits_length()
    {
        Assert.Equal("hi", ChatRules.Clean("  hi\u0007 ", 100));
        Assert.Equal("abc", ChatRules.Clean("abcdef", 3));
        Assert.Equal("", ChatRules.Clean(null, 10));
        Assert.Equal("a\nb", ChatRules.Clean("a\nb", 10));
    }

    [Theory]
    [InlineData("a@b.com", true)]
    [InlineData("a b@c.com", false)]
    [InlineData("@b.com", false)]
    [InlineData("nope", false)]
    [InlineData("", false)]
    public void Email_check(string? e, bool ok) => Assert.Equal(ok, ChatRules.LooksLikeEmail(e));

    [Fact]
    public void Stale_chats()
    {
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        Assert.True(ChatRules.IsStale(now.AddHours(-13), now));
        Assert.False(ChatRules.IsStale(now.AddHours(-1), now));
        Assert.False(ChatRules.IsStale(null, now));
    }
}
