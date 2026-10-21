using Application.Features.SalesTargetManager;

namespace InterlaceUI.Tests;

public class LeaderboardRangeTests
{
    [Fact]
    public void Quarter_covers_three_months()
    {
        var (from, to, m1, m2, label) = GetLeaderboardHandler.Range(2026, "Quarter", 2);
        Assert.Equal(new DateTime(2026, 4, 1), from);
        Assert.Equal(new DateTime(2026, 7, 1), to);
        Assert.Equal((4, 6), (m1, m2));
        Assert.Equal("Q2 2026", label);
    }

    [Fact]
    public void Year_covers_all_months()
    {
        var (from, to, m1, m2, _) = GetLeaderboardHandler.Range(2026, "Year", 0);
        Assert.Equal(new DateTime(2027, 1, 1), to);
        Assert.Equal((1, 12), (m1, m2));
        Assert.Equal(new DateTime(2026, 1, 1), from);
    }

    [Theory]
    [InlineData(13, 12)]
    [InlineData(0, 1)]
    public void Month_is_clamped(int input, int expected)
    {
        var (_, _, m1, m2, _) = GetLeaderboardHandler.Range(2026, "Month", input);
        Assert.Equal(expected, m1);
        Assert.Equal(expected, m2);
    }
}
