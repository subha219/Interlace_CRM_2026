using Application.Features.TelephonyManager;

namespace InterlaceUI.Tests;

public class CallRulesTests
{
    [Theory]
    [InlineData("completed", "Completed")]
    [InlineData("no-answer", "NoAnswer")]
    [InlineData("busy", "Busy")]
    [InlineData("canceled", "Failed")]
    [InlineData("in-progress", "InProgress")]
    [InlineData("queued", "Ringing")]
    [InlineData(null, "Ringing")]
    public void Status_is_mapped(string? given, string expected) => Assert.Equal(expected, CallRules.MapStatus(given));

    [Fact]
    public void Finished_statuses() { Assert.True(CallRules.IsFinished("Busy")); Assert.False(CallRules.IsFinished("Ringing")); }

    [Fact]
    public void Duration_is_minutes_and_seconds() => Assert.Equal("2:05", CallRules.FormatDuration(125));

    [Theory]
    [InlineData("https://recordings.example.com/a.mp3", true)]
    [InlineData("http://recordings.example.com/a.mp3", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Recording_urls_must_be_https(string? url, bool ok) => Assert.Equal(ok, CallRules.SafeRecordingUrl(url) != null);

    [Fact]
    public void Key_check_fails_closed()
    {
        Assert.True(CallRules.KeyMatches("abc", "abc"));
        Assert.False(CallRules.KeyMatches("abd", "abc"));
        Assert.False(CallRules.KeyMatches("", ""));
        Assert.False(CallRules.KeyMatches("abc", null));
    }
}
