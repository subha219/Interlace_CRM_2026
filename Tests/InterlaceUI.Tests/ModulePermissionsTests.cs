using System.Reflection;
using InterlaceUI.BackEnd.Common.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace InterlaceUI.Tests;

public class ModulePermissionsTests
{
    private static IEnumerable<Type> Controllers() =>
        typeof(ModulePermissions).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t) && t.Name.EndsWith("Controller"));

    private static string Name(Type t) => t.Name[..^"Controller".Length];

    [Fact]
    public void Every_controller_has_a_permission_rule()
    {
        var missing = Controllers()
            .Select(Name)
            .Where(n => !ModulePermissions.ControllerRules.ContainsKey(n))
            .ToList();

        Assert.True(missing.Count == 0, "Controllers without a ModulePermissions rule: " + string.Join(", ", missing));
    }

    [Fact]
    public void Permission_rules_do_not_point_to_removed_controllers()
    {
        var names = Controllers().Select(Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var stale = ModulePermissions.ControllerRules.Keys.Where(k => !names.Contains(k)).ToList();

        Assert.True(stale.Count == 0, "Rules for controllers that no longer exist: " + string.Join(", ", stale));
    }

    [Fact]
    public void Every_rule_role_is_a_known_module_role()
    {
        var known = Infrastructure.SecurityManager.NavigationMenu.NavigationTreeStructure
            .GetCompleteFirstMenuNavigationSegment()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var unknown = ModulePermissions.ControllerRules.Values
            .Concat(ModulePermissions.ActionRules.Values)
            .SelectMany(r => r.Roles)
            .Where(r => !known.Contains(r))
            .Distinct()
            .ToList();

        Assert.True(unknown.Count == 0, "Roles that are not menu modules: " + string.Join(", ", unknown));
    }

    [Fact]
    public void Action_rules_point_to_real_actions()
    {
        var actions = Controllers()
            .SelectMany(c => c.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.GetCustomAttributes().Any(a => a is HttpMethodAttribute))
                .Select(m => $"{Name(c)}.{m.Name.Replace("Async", "")}"))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var stale = ModulePermissions.ActionRules.Keys.Where(k => !actions.Contains(k)).ToList();

        Assert.True(stale.Count == 0, "Action rules without a matching action: " + string.Join(", ", stale));
    }

    [Fact]
    public void Sensitive_security_actions_are_never_anonymous()
    {
        var sensitive = new[] { "GetUserRoles", "UpdateUserRole", "UpdateMyProfileAvatar", "CreateUser", "UpdateUser", "DeleteUser" };
        var controller = typeof(ModulePermissions).Assembly.GetTypes().Single(t => t.Name == "SecurityController");

        foreach (var name in sensitive)
        {
            var method = controller.GetMethod(name + "Async")!;
            Assert.DoesNotContain(method.GetCustomAttributes(), a => a is AllowAnonymousAttribute);
        }
    }

    [Theory]
    [InlineData("Lead", "Create", "POST")]
    [InlineData("Quotation", "Update", "POST")]
    [InlineData("Security", "UpdateUserRole", "POST")]
    [InlineData("AuditLog", "GetAuditLogList", "GET")]
    public void Restricted_calls_require_roles(string controller, string action, string method)
    {
        var roles = ModulePermissions.RequiredRoles(controller, action, method);
        Assert.NotNull(roles);
        Assert.NotEmpty(roles!);
    }

    [Theory]
    [InlineData("Lead", "GetLeadList", "GET")]
    [InlineData("Notification", "GetNotificationList", "GET")]
    [InlineData("Security", "Login", "POST")]
    [InlineData("Security", "GetMyProfileList", "GET")]
    public void Open_calls_need_no_module_role(string controller, string action, string method)
    {
        Assert.Null(ModulePermissions.RequiredRoles(controller, action, method));
    }

    [Fact]
    public void Unmapped_controller_writes_fail_closed()
    {
        var roles = ModulePermissions.RequiredRoles("DoesNotExist", "Create", "POST");
        Assert.NotNull(roles);
        Assert.DoesNotContain(roles!, r => r == "Leads");
    }
}
