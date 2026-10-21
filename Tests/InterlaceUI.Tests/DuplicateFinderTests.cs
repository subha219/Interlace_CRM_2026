using Application.Features.DuplicateManager.Queries;

namespace InterlaceUI.Tests;

public class DuplicateFinderTests
{
    private static DuplicateItemDto Item(string id, string? name, string? email, string? phone) =>
        new() { Id = id, Name = name, Email = email, Phone = phone, CreatedAtUtc = DateTime.UtcNow };

    [Fact]
    public void Same_email_ignoring_case_is_grouped()
    {
        var groups = DuplicateFinder.Group(new()
        {
            Item("1", "Acme", "Sales@Acme.com", null),
            Item("2", "Other", " sales@acme.com ", null),
            Item("3", "Unrelated", "x@y.com", null)
        });

        var g = Assert.Single(groups);
        Assert.Equal(2, g.Items.Count);
        Assert.Contains("Same e-mail", g.Reasons);
    }

    [Fact]
    public void Phone_numbers_match_on_last_ten_digits()
    {
        var groups = DuplicateFinder.Group(new()
        {
            Item("1", "A One", null, "+91 98765-43210"),
            Item("2", "B Two", null, "09876543210")
        });

        Assert.Single(groups);
    }

    [Fact]
    public void Links_are_transitive()
    {
        var groups = DuplicateFinder.Group(new()
        {
            Item("1", "Globex Corp", "a@globex.com", null),
            Item("2", "Globex Corp.", "b@globex.com", "5551234567"),
            Item("3", "Other Name", "c@x.com", "(555) 123-4567")
        });

        var g = Assert.Single(groups);
        Assert.Equal(3, g.Items.Count);
    }

    [Fact]
    public void Short_or_empty_values_never_match()
    {
        var groups = DuplicateFinder.Group(new()
        {
            Item("1", "ab", "", "123"),
            Item("2", "ab", "", "123")
        });

        Assert.Empty(groups);
    }
}
