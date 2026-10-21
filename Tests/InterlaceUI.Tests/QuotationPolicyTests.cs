using Application.Features.QuotationManager;
using Domain.Entities;
using Domain.Enums;

namespace InterlaceUI.Tests;

public class QuotationPolicyTests
{
    private static Quotation Quote(string? approval) => new() { ApprovalStatus = approval };

    [Theory]
    [InlineData("Required")]
    [InlineData("Pending")]
    [InlineData("Rejected")]
    public void Unapproved_discount_blocks_sending_and_converting(string status)
    {
        Assert.Throws<Exception>(() => QuotationPolicy.EnsureCanProceed(Quote(status), QuotationStatus.Sent));
        Assert.Throws<Exception>(() => QuotationPolicy.EnsureCanProceed(Quote(status), QuotationStatus.Converted));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("None")]
    [InlineData("Approved")]
    public void Approved_or_plain_quotes_can_proceed(string? status)
    {
        QuotationPolicy.EnsureCanProceed(Quote(status), QuotationStatus.Sent);
        QuotationPolicy.EnsureCanProceed(Quote(status), QuotationStatus.Converted);
    }

    [Fact]
    public void Draft_is_always_allowed()
    {
        QuotationPolicy.EnsureCanProceed(Quote("Required"), QuotationStatus.Draft);
    }

    [Fact]
    public void Net_factor_reflects_the_discount()
    {
        var q = new Quotation { BeforeTaxAmount = 90 };
        q.QuotationItemList.Add(new QuotationItem { Total = 100 });
        Assert.Equal(0.9, QuotationPolicy.NetFactor(q), 5);
    }

    [Fact]
    public void Net_factor_is_one_without_items()
    {
        Assert.Equal(1, QuotationPolicy.NetFactor(new Quotation { BeforeTaxAmount = 50 }));
    }
}
