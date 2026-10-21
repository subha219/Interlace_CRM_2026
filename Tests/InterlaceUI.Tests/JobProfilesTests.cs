using Infrastructure.SecurityManager.NavigationMenu;
using Infrastructure.SecurityManager.Roles;

namespace InterlaceUI.Tests;

public class JobProfilesTests
{
    [Fact]
    public void Every_role_in_a_profile_exists_in_the_menu()
    {
        var known = NavigationTreeStructure.GetCompleteFirstMenuNavigationSegment().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unknown = JobProfiles.All.SelectMany(p => p.Roles.Select(r => (p.Key, Role: r))).Where(x => !known.Contains(x.Role)).Select(x => $"{x.Key}:{x.Role}").ToList();
        Assert.True(unknown.Count == 0, "Unknown roles: " + string.Join(", ", unknown));
    }

    [Fact]
    public void Profiles_have_unique_logins_and_no_administration_roles()
    {
        Assert.Equal(JobProfiles.All.Count, JobProfiles.All.Select(p => p.Email).Distinct().Count());
        var admin = new[] { "Users", "Roles", "Settings", "AuditLogs", "Integrations", "AiGovernance", "NumberSequences", "Companies" };
        Assert.All(JobProfiles.All, p => Assert.Empty(p.Roles.Intersect(admin)));
    }

    [Fact]
    public void Everybody_gets_the_basics() =>
        Assert.All(JobProfiles.All, p => Assert.Empty(JobProfiles.Basics.Except(p.Roles)));
}
