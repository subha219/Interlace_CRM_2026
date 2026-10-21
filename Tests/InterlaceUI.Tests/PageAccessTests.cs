using Infrastructure.SecurityManager.NavigationMenu;
using Infrastructure.SecurityManager.Roles;
using InterlaceUI.BackEnd.Common.Security;

namespace InterlaceUI.Tests;

public class PageAccessTests
{
    private static readonly string[] Known = NavigationTreeStructure.GetCompleteFirstMenuNavigationSegment().ToArray();

    [Fact]
    public void Page_access_lists_use_real_roles_and_real_pages()
    {
        var urls = NavigationTreeStructure.GetCompleteMenuNavigationTreeNode().Select(n => n.NavURL).Where(u => u != null).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var (url, roles) in PageAccess.Pages)
        {
            Assert.Contains(url, urls);
            Assert.All(roles, r => Assert.Contains(r, Known));
        }
    }

    [Fact]
    public void Menu_nodes_carry_the_extra_roles()
    {
        var node = NavigationTreeStructure.GetCompleteMenuNavigationTreeNode().Single(n => n.NavURL == "/Dashboards/SupportDashboard");
        Assert.Equal(new[] { "SupportCases" }, node.Roles);
    }

    [Fact]
    public void Every_job_profile_has_a_dashboard_and_none_sees_all_of_them()
    {
        var pages = new[] { "/Dashboards/InsightsDashboard", "/Dashboards/DefaultDashboard", "/Dashboards/SupportDashboard", "/Dashboards/MyWork" };
        bool Can(JobProfile p, string url) => p.Roles.Contains("Dashboards") && (!PageAccess.Pages.TryGetValue(url, out var need) || need.Intersect(p.Roles).Any());
        foreach (var p in JobProfiles.All)
            Assert.Contains(pages, u => Can(p, u));
        Assert.Contains(JobProfiles.All, p => !Can(p, "/Dashboards/SupportDashboard"));
        Assert.Contains(JobProfiles.All, p => !Can(p, "/Dashboards/DefaultDashboard") || !Can(p, "/Dashboards/InsightsDashboard"));
    }

    [Theory]
    [InlineData("GetCRMDashboard"), InlineData("GetCardsDashboard"), InlineData("GetSalesDashboard"), InlineData("GetPurchaseDashboard"),
     InlineData("GetLeadPipelineFunnel"), InlineData("GetSalesTeamLeadClosing"), InlineData("GetCampaignByStatus"), InlineData("GetLeadActivityByType")]
    public void Dashboard_data_needs_a_module_role_not_just_a_login(string action)
    {
        var roles = ModulePermissions.RequiredRoles("Dashboard", action, "GET");
        Assert.NotNull(roles);
        Assert.DoesNotContain("Dashboards", roles!);
    }

    [Fact]
    public void A_support_agent_cannot_read_sales_or_purchase_dashboards()
    {
        var agent = JobProfiles.All.Single(p => p.Key == "supportagent").Roles;
        foreach (var action in new[] { "GetSalesDashboard", "GetPurchaseDashboard", "GetCardsDashboard", "GetCRMDashboard" })
            Assert.Empty(ModulePermissions.RequiredRoles("Dashboard", action, "GET")!.Intersect(agent));
    }
}
