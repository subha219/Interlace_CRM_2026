using Application.Features.MailSyncManager;

namespace InterlaceUI.Tests;

public class MailRulesTests
{
    [Fact]
    public void Addresses_are_read_from_headers()
    {
        var list = MailRules.ParseAddresses("Asha Rao <Asha@Acme.com>, \"Bob, Jr\" <bob@y.co.in>, asha@acme.com");
        Assert.Equal(new[] { "asha@acme.com", "bob@y.co.in" }, list);
        Assert.Empty(MailRules.ParseAddresses(null));
    }

    [Fact]
    public void Direction_follows_the_sender()
    {
        Assert.Equal("Out", MailRules.Direction("Me <me@us.com>", "me@us.com"));
        Assert.Equal("In", MailRules.Direction("Asha <asha@acme.com>", "me@us.com"));
    }

    [Fact]
    public void Counterparties_never_include_the_mailbox_owner()
    {
        Assert.Equal(new[] { "a@x.com", "c@y.com" }, MailRules.Counterparties("Out", "me@us.com", "a@x.com, me@us.com", "c@y.com", "me@us.com"));
        Assert.Equal(new[] { "a@x.com" }, MailRules.Counterparties("In", "A <a@x.com>", "me@us.com", null, "me@us.com"));
    }

    [Fact]
    public void Junk_labels_are_skipped()
    {
        Assert.True(MailRules.IsSkippable(new[] { "INBOX", "SPAM" }));
        Assert.True(MailRules.IsSkippable(new[] { "CATEGORY_PROMOTIONS" }));
        Assert.False(MailRules.IsSkippable(new[] { "INBOX", "IMPORTANT" }));
    }

    [Fact]
    public void Clip_decodes_and_limits()
    {
        Assert.Equal("It's fine", MailRules.Clip("It&#39;s\nfine", 50));
        Assert.Equal("abc", MailRules.Clip("abcdef", 3));
    }

    [Fact]
    public void Activity_subject_is_readable_and_short()
    {
        Assert.Equal("Email to Acme: Quote", MailRules.ActivitySubject("Out", "Acme", "Quote"));
        Assert.True(MailRules.ActivitySubject("In", "Acme", new string('x', 500)).Length <= 200);
    }

    [Fact]
    public void Internal_date_is_milliseconds() =>
        Assert.Equal(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), MailRules.FromInternalDate("1767225600000"));

    [Fact]
    public void Query_goes_back_five_minutes_and_thirty_days_first()
    {
        var now = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        Assert.Equal(new DateTimeOffset(now).ToUnixTimeSeconds() - 30L * 86400 - 300, MailRules.QueryAfterSeconds(null, now));
        Assert.Equal(1000 - 300, MailRules.QueryAfterSeconds(1_000_000, now));
    }
}
