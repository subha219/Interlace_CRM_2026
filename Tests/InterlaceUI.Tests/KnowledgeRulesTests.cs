using Application.Features.KnowledgeManager;

namespace InterlaceUI.Tests;

public class KnowledgeRulesTests
{
    [Fact]
    public void Words_are_lower_cased_unique_and_limited()
    {
        var w = KnowledgeRules.Words("Reset, RESET password? a b c d e f g h");
        Assert.Equal("reset", w[0]);
        Assert.Equal(1, w.Count(x => x == "reset"));
        Assert.DoesNotContain("a", w);
        Assert.True(w.Count <= 6);
    }

    [Fact]
    public void Empty_search_has_no_words() => Assert.Empty(KnowledgeRules.Words("  "));
}
