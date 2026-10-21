using Infrastructure.SecurityManager.AspNetIdentity;
using Infrastructure.SecurityManager.Roles;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.SeedManager.Systems;

/// <summary>
/// Creates one sample login per job profile (salesrep@root.com, supportagent@root.com, ...) so each role set can be tried out.
/// Only runs when Seed:SampleLogins is true, and the password comes from Seed:SampleUserPassword, never from code.
/// It adds missing roles to those sample users but never removes any.
/// </summary>
public class JobProfileSeeder
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IConfiguration _configuration;
    private readonly ILogger<JobProfileSeeder> _logger;

    public JobProfileSeeder(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, IConfiguration configuration, ILogger<JobProfileSeeder> logger)
    {
        _userManager = userManager; _roleManager = roleManager; _configuration = configuration; _logger = logger;
    }

    public async Task GenerateDataAsync()
    {
        if (!string.Equals(_configuration["Seed:SampleLogins"], "true", StringComparison.OrdinalIgnoreCase)) return;
        var password = _configuration["Seed:SampleUserPassword"];
        if (string.IsNullOrWhiteSpace(password))
        {
            _logger.LogWarning("Seed:SampleLogins is on but Seed:SampleUserPassword is empty, so no sample logins were created.");
            return;
        }

        foreach (var profile in JobProfiles.All)
        {
            var user = await _userManager.FindByEmailAsync(profile.Email);
            if (user == null)
            {
                user = new ApplicationUser(profile.Email, profile.FirstName, profile.LastName, "", "seed") { EmailConfirmed = true };
                var created = await _userManager.CreateAsync(user, password);
                if (!created.Succeeded)
                {
                    _logger.LogWarning("Could not create sample login {Email}: {Errors}", profile.Email, string.Join(" ", created.Errors.Select(e => e.Description)));
                    continue;
                }
            }

            foreach (var role in profile.Roles)
            {
                if (await _roleManager.RoleExistsAsync(role) && !await _userManager.IsInRoleAsync(user, role))
                    await _userManager.AddToRoleAsync(user, role);
            }
        }
    }
}
