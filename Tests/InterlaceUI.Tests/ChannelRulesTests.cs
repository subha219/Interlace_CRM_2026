using System.Security.Cryptography;
using System.Text;
using Application.Features.MessagingManager;
using Xunit;

namespace InterlaceUI.Tests;

public class ChannelRulesTests
{
    [Theory]
    [InlineData("98765 43210", "919876543210")]
    [InlineData("+91 98765-43210", "919876543210")]
    [InlineData("09876543210", "919876543210")]
    [InlineData("0044 7911 123456", "447911123456")]
    [InlineData("+1 415 555 2671", "14155552671")]
    public void NormalizePhone_gives_digits_with_country_code(string raw, string expected) =>
        Assert.Equal(expected, ChannelRules.NormalizePhone(raw));

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("12345")]
    [InlineData("abc")]
    public void NormalizePhone_rejects_bad_numbers(string? raw) => Assert.Null(ChannelRules.NormalizePhone(raw));

    [Fact]
    public void LastTen_matches_any_format() =>
        Assert.Equal(ChannelRules.LastTen("+91 98765 43210"), ChannelRules.LastTen("9876543210"));

    [Fact]
    public void Session_window_is_24_hours()
    {
        var now = new DateTime(2026, 1, 2, 12, 0, 0, DateTimeKind.Utc);
        Assert.True(ChannelRules.SessionOpen(now.AddHours(-23), now));
        Assert.False(ChannelRules.SessionOpen(now.AddHours(-25), now));
        Assert.False(ChannelRules.SessionOpen(null, now));
    }

    [Theory]
    [InlineData("STOP", true)]
    [InlineData(" stop. ", true)]
    [InlineData("Unsubscribe", true)]
    [InlineData("please don't stop the delivery", false)]
    [InlineData("", false)]
    public void Stop_keyword(string text, bool expected) => Assert.Equal(expected, ChannelRules.IsStopKeyword(text));

    [Fact]
    public void Render_fills_placeholders() =>
        Assert.Equal("Hi Asha, 500 due", ChannelRules.Render("Hi {{name}}, {{amount}} due", new[] { new KeyValuePair<string, string>("name", "Asha"), new KeyValuePair<string, string>("amount", "500") }));

    [Fact]
    public void Signature_is_checked()
    {
        var body = Encoding.UTF8.GetBytes("{\"a\":1}");
        using var h = new HMACSHA256(Encoding.UTF8.GetBytes("secret"));
        var good = "sha256=" + Convert.ToHexString(h.ComputeHash(body)).ToLowerInvariant();
        Assert.True(ChannelRules.VerifySignature(body, good, "secret"));
        Assert.False(ChannelRules.VerifySignature(body, good, "other"));
        Assert.False(ChannelRules.VerifySignature(body, "sha256=00", "secret"));
        Assert.False(ChannelRules.VerifySignature(body, null, "secret"));
        Assert.False(ChannelRules.VerifySignature(body, good, ""));
    }

    [Fact]
    public void Parses_messages_and_statuses()
    {
        const string json = """
        {"entry":[{"changes":[{"value":{
          "contacts":[{"wa_id":"919876543210","profile":{"name":"Asha"}}],
          "messages":[{"from":"919876543210","id":"wamid.1","timestamp":"1767355200","type":"text","text":{"body":"Hello"}}],
          "statuses":[{"id":"wamid.0","status":"read"},{"id":"wamid.9","status":"failed","errors":[{"title":"Not allowed"}]}]
        }}]}]}
        """;
        var e = ChannelRules.ParseWhatsApp(json);
        Assert.Single(e.Messages);
        Assert.Equal("Asha", e.Messages[0].Name);
        Assert.Equal("Hello", e.Messages[0].Text);
        Assert.Equal(2, e.Statuses.Count);
        Assert.Equal("Read", e.Statuses[0].Status);
        Assert.Equal("Failed", e.Statuses[1].Status);
        Assert.Equal("Not allowed", e.Statuses[1].Error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not json")]
    [InlineData("{\"entry\":5}")]
    public void Bad_webhook_bodies_give_nothing(string? json)
    {
        var e = ChannelRules.ParseWhatsApp(json);
        Assert.Empty(e.Messages);
        Assert.Empty(e.Statuses);
    }
}
