using Application.Features.SignatureManager;
using Domain.Entities;

namespace InterlaceUI.Tests;

public class SignatureRulesTests
{
    private static readonly DateTime Now = new(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Otp_is_six_digits()
    {
        for (var i = 0; i < 50; i++) Assert.Matches("^[0-9]{6}$", SignatureRules.NewOtp());
    }

    [Fact]
    public void Otp_hash_is_tied_to_the_request()
    {
        Assert.Equal(SignatureRules.OtpHash("a", "123456"), SignatureRules.OtpHash("a", " 123456 "));
        Assert.NotEqual(SignatureRules.OtpHash("a", "123456"), SignatureRules.OtpHash("b", "123456"));
    }

    [Fact]
    public void Tokens_are_random() => Assert.NotEqual(SignatureRules.NewToken(), SignatureRules.NewToken());

    [Theory]
    [InlineData("Sent", 1, "Sent")]
    [InlineData("Viewed", -1, "Expired")]
    [InlineData("Signed", -1, "Signed")]
    [InlineData("Declined", 5, "Declined")]
    public void Effective_status(string status, int daysFromNow, string expected) =>
        Assert.Equal(expected, SignatureRules.Effective(status, Now.AddDays(daysFromNow), Now));

    [Fact]
    public void Only_open_requests_can_be_signed()
    {
        Assert.True(SignatureRules.CanSign("Sent", Now.AddDays(1), Now));
        Assert.True(SignatureRules.CanSign("Viewed", Now.AddDays(1), Now));
        Assert.False(SignatureRules.CanSign("Sent", Now.AddDays(-1), Now));
        Assert.False(SignatureRules.CanSign("Signed", Now.AddDays(1), Now));
        Assert.False(SignatureRules.CanSign("Cancelled", Now.AddDays(1), Now));
    }

    [Fact]
    public void Otp_is_limited_by_time_and_attempts()
    {
        Assert.True(SignatureRules.OtpUsable("x", Now.AddMinutes(5), 0, Now));
        Assert.False(SignatureRules.OtpUsable("x", Now.AddMinutes(-1), 0, Now));
        Assert.False(SignatureRules.OtpUsable("x", Now.AddMinutes(5), SignatureRules.MaxOtpAttempts, Now));
        Assert.False(SignatureRules.OtpUsable(null, Now.AddMinutes(5), 0, Now));
    }

    [Fact]
    public void Contract_text_is_stable_so_the_fingerprint_can_be_checked()
    {
        var c = new Contract { Number = "CTR-1", Title = "Support plan", Value = 1200, BillingCycle = "Yearly", StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 12, 31), Notes = "Net 30" };
        var a = SignatureRules.ContractText(c, "Acme");
        Assert.Equal(a, SignatureRules.ContractText(c, "Acme"));
        Assert.Contains("Acme", a);
        Assert.Contains("Net 30", a);
        Assert.NotEqual(SignatureRules.Hash(a), SignatureRules.Hash(a + " "));
    }

    [Theory]
    [InlineData("a@b.com", true)]
    [InlineData("a b@c.com", false)]
    [InlineData("", false)]
    public void Email_check(string e, bool ok) => Assert.Equal(ok, SignatureRules.LooksLikeEmail(e));

    [Fact]
    public void Fixed_equals() { Assert.True(SignatureRules.FixedEquals("a", "a")); Assert.False(SignatureRules.FixedEquals("a", "b")); }

    [Fact]
    public void Quotation_text_lists_items_and_totals_and_is_stable()
    {
        var q = new Quotation { Number = "QUO-1", QuotationDate = new DateTime(2026, 1, 5), ValidUntil = new DateTime(2026, 2, 5), BeforeTaxAmount = 90, DiscountPercent = 10, DiscountAmount = 10, TaxAmount = 9, AfterTaxAmount = 99 };
        var items = new[] { ("Widget", (string?)"blue", 2.0, 50.0, 100.0) };
        var a = SignatureRules.QuotationText(q, "Acme", items);
        Assert.Equal(a, SignatureRules.QuotationText(q, "Acme", items));
        Assert.Contains("QUOTATION QUO-1", a);
        Assert.Contains("Widget - blue: 2 x 50.00 = 100.00", a);
        Assert.Contains("Discount: 10%", a);
        Assert.Contains("Total: 99.00", a);
    }

    [Theory]
    [InlineData("Required", true)]
    [InlineData("Pending", true)]
    [InlineData("Rejected", true)]
    [InlineData("Approved", false)]
    [InlineData(null, false)]
    public void Unapproved_quotations_cannot_be_sent_for_signature(string? approval, bool blocked) =>
        Assert.Equal(blocked, SignatureRules.QuotationBlocker(new Quotation { ApprovalStatus = approval }) != null);

    [Fact]
    public void Closed_quotations_cannot_be_sent_for_signature() =>
        Assert.NotNull(SignatureRules.QuotationBlocker(new Quotation { QuotationStatus = Domain.Enums.QuotationStatus.Converted }));
}
