using Infrastructure.SecurityManager.AspNetIdentity;
using Infrastructure.SecurityManager.Roles;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Infrastructure.SeedManager.Systems;

/// <summary>
/// Keeps roles in sync with the navigation menu. Roles are created from the menu only when the database is
/// empty, so modules added later (for example Support, Quotations, Invoices) would otherwise have no role
/// and nobody could open their pages. Runs on every startup and only adds what is missing.
/// </summary>
public class RoleSyncSeeder
{
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IdentitySettings _identitySettings;

    public RoleSyncSeeder(
        RoleManager<IdentityRole> roleManager,
        UserManager<ApplicationUser> userManager,
        IOptions<IdentitySettings> identitySettings
        )
    {
        _roleManager = roleManager;
        _userManager = userManager;
        _identitySettings = identitySettings.Value;
    }

    public async Task GenerateDataAsync()
    {
        var roles = RoleHelper.GetAdminRoles();

        foreach (var role in roles)
        {
            if (!await _roleManager.RoleExistsAsync(role))
            {
                await _roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        var admin = await _userManager.FindByEmailAsync(_identitySettings.DefaultAdmin.Email);
        if (admin == null)
        {
            return;
        }

        foreach (var role in roles)
        {
            if (!await _userManager.IsInRoleAsync(admin, role))
            {
                await _userManager.AddToRoleAsync(admin, role);
            }
        }
    }
}
