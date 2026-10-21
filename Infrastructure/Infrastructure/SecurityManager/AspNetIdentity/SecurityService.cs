using Application.Common.Services.EmailManager;
using Application.Common.Services.SecurityManager;
using Domain.Entities;
using Infrastructure.DataAccessManager.EFCore.Contexts;
using Infrastructure.SecurityManager.NavigationMenu;
using Infrastructure.SecurityManager.Roles;
using Infrastructure.SecurityManager.Tokens;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System.Data;
using System.Text;
using System.Text.Encodings.Web;
using static Domain.Common.Constants;

namespace Infrastructure.SecurityManager.AspNetIdentity;

public class SecurityService : ISecurityService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ITokenService _tokenService;
    private readonly DataContext _context;
    private readonly IdentitySettings _identitySettings;
    private readonly IEmailService _emailService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IConfiguration _configuration;
    private readonly Application.Common.Services.IntegrationManager.IConnectionSettings _connectionSettings;

    public SecurityService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ITokenService tokenService,
        DataContext context,
        IOptions<IdentitySettings> identitySettings,
        IEmailService emailService,
        IHttpContextAccessor httpContextAccessor,
        RoleManager<IdentityRole> roleManager,
        IConfiguration configuration,
        Application.Common.Services.IntegrationManager.IConnectionSettings connectionSettings
        )
    {
        _connectionSettings = connectionSettings;
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
        _context = context;
        _identitySettings = identitySettings.Value;
        _emailService = emailService;
        _httpContextAccessor = httpContextAccessor;
        _roleManager = roleManager;
        _configuration = configuration;
    }

    public async Task<LoginResultDto> LoginAsync(
        string email,
        string password,
        string? twoFactorCode = null,
        bool sendEmailCode = false,
        CancellationToken cancellationToken = default
        )
    {
        if (string.Equals(_connectionSettings["Sso:DisablePasswordLogin"], "true", StringComparison.OrdinalIgnoreCase)
            && !string.Equals((email ?? "").Trim(), _identitySettings.DefaultAdmin.Email, StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception("Password sign-in is turned off. Please use single sign-on.");
        }

        var user = await _userManager.FindByEmailAsync(email);

        if (user == null)
        {
            throw new Exception("Invalid login credentials.");
        }

        if (user.IsBlocked == true)
        {
            throw new Exception($"User is blocked. {email}");
        }

        if (user.IsDeleted == true)
        {
            throw new Exception($"User already deleted. {email}");
        }

        var result = await _signInManager.PasswordSignInAsync(user, password, true, lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            throw new Exception("Invalid login credentials. IsLockedOut.");
        }

        if (!result.Succeeded && !result.RequiresTwoFactor)
        {
            await WriteSecurityEventAsync(user, "LoginFailed", cancellationToken);
            throw new Exception("Invalid login credentials. NotSucceeded.");
        }

        if (result.RequiresTwoFactor || user.TwoFactorEnabled)
        {
            var code = (twoFactorCode ?? "").Replace(" ", "").Replace("-", "");
            if (string.IsNullOrEmpty(code))
            {
                if (sendEmailCode)
                {
                    var emailCode = await _userManager.GenerateTwoFactorTokenAsync(user, TokenOptions.DefaultEmailProvider);
                    await _emailService.SendEmailAsync(user.Email!, "Your verification code",
                        $"<p>Your sign-in verification code is <b>{emailCode}</b>. It expires in a few minutes. If this was not you, change your password.</p>");
                }
                return new LoginResultDto { RequiresTwoFactor = true, Email = user.Email };
            }

            var valid =
                await _userManager.VerifyTwoFactorTokenAsync(user, _userManager.Options.Tokens.AuthenticatorTokenProvider, code) ||
                await _userManager.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultEmailProvider, code) ||
                (await _userManager.RedeemTwoFactorRecoveryCodeAsync(user, twoFactorCode!.Trim())).Succeeded;

            if (!valid)
            {
                await _userManager.AccessFailedAsync(user);
                await WriteSecurityEventAsync(user, "TwoFactorFailed", cancellationToken);
                throw new Exception("Invalid verification code.");
            }
        }

        await _userManager.ResetAccessFailedCountAsync(user);
        await WriteSecurityEventAsync(user, "LoginSucceeded", cancellationToken);

        return await IssueTokensAsync(user, cancellationToken);
    }

    private async Task<LoginResultDto> IssueTokensAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var accessToken = _tokenService.GenerateToken(user, roles.Select(r => new System.Security.Claims.Claim("role", r)).ToList());
        var refreshToken = _tokenService.GenerateRefreshToken();

        var tokens = await _context.Token.Where(x => x.UserId == user.Id).ToListAsync(cancellationToken);
        foreach (var item in tokens)
        {
            _context.Remove(item);
        }

        var token = new Token();
        token.UserId = user.Id;
        token.RefreshToken = refreshToken;
        token.ExpiryDate = DateTime.UtcNow.AddDays(TokenConsts.ExpiryInDays);
        token.IsDeleted = false;
        token.CreatedAtUtc = DateTime.UtcNow;
        token.CreatedById = user.Id;
        await _context.AddAsync(token, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        return new LoginResultDto
        {
            UserId = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            CompanyName = user.CompanyName,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            MenuNavigation = NavigationTreeStructure.GetCompleteMenuNavigationTreeNode(),
            Roles = roles.ToList(),
            Avatar = user.ProfilePictureName
        };
    }

    public async Task<LoginResultDto> LoginExternalAsync(string email, string firstName, string lastName, string provider, CancellationToken cancellationToken = default)
    {
        email = (email ?? "").Trim();
        var user = await _userManager.FindByEmailAsync(email);

        if (user == null)
        {
            user = new ApplicationUser(email, string.IsNullOrWhiteSpace(firstName) ? email.Split('@')[0] : firstName, lastName ?? "", "", "sso");
            // nobody ever knows this password; the account is entered through the provider (or a password reset by an administrator)
            var password = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24)) + "aA1!";
            var created = await _userManager.CreateAsync(user, password);
            if (!created.Succeeded) throw new Exception("Could not create the account: " + string.Join(" ", created.Errors.Select(x => x.Description)));
            await _userManager.AddToRoleAsync(user, RoleHelper.GetProfileRole());
            await WriteSecurityEventAsync(user, $"SsoUserCreated ({provider})", cancellationToken);
        }

        if (user.IsBlocked == true) throw new Exception($"User is blocked. {email}");
        if (user.IsDeleted == true) throw new Exception($"User already deleted. {email}");
        if (await _userManager.IsLockedOutAsync(user)) throw new Exception("This account is locked.");

        if (!user.EmailConfirmed) { user.EmailConfirmed = true; await _userManager.UpdateAsync(user); }
        await WriteSecurityEventAsync(user, $"SsoLoginSucceeded ({provider})", cancellationToken);
        return await IssueTokensAsync(user, cancellationToken);
    }

    private async Task WriteSecurityEventAsync(ApplicationUser user, string action, CancellationToken cancellationToken)
    {
        var ip = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
        _context.AuditLog.Add(new AuditLog
        {
            EntityName = "Security",
            EntityId = user.Id,
            Action = action,
            UserId = user.Id,
            UserName = user.Email,
            Changes = ip == null ? null : $"[{{\"field\":\"ip\",\"old\":null,\"new\":\"{ip}\"}}]",
            CreatedAtUtc = DateTime.UtcNow,
            CreatedById = user.Id,
            IsDeleted = false
        });
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<TwoFactorSetupDto> GetTwoFactorSetupAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId) ?? throw new Exception("User not found.");
        if (user.TwoFactorEnabled)
        {
            return new TwoFactorSetupDto { Enabled = true };
        }

        var key = await _userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(key))
        {
            await _userManager.ResetAuthenticatorKeyAsync(user);
            key = await _userManager.GetAuthenticatorKeyAsync(user);
        }

        var issuer = Uri.EscapeDataString("Interlace");
        var uri = $"otpauth://totp/{issuer}:{Uri.EscapeDataString(user.Email ?? user.UserName ?? "user")}?secret={key}&issuer={issuer}&digits=6";
        return new TwoFactorSetupDto { Enabled = false, SharedKey = key, AuthenticatorUri = uri };
    }

    public async Task<List<string>> EnableTwoFactorAsync(string userId, string code, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId) ?? throw new Exception("User not found.");
        var clean = code.Replace(" ", "").Replace("-", "");
        var ok = await _userManager.VerifyTwoFactorTokenAsync(user, _userManager.Options.Tokens.AuthenticatorTokenProvider, clean);
        if (!ok)
        {
            throw new Exception("Invalid verification code.");
        }

        await _userManager.SetTwoFactorEnabledAsync(user, true);
        var codes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
        await WriteSecurityEventAsync(user, "TwoFactorEnabled", cancellationToken);
        return codes?.ToList() ?? new List<string>();
    }

    public async Task DisableTwoFactorAsync(string userId, string password, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId) ?? throw new Exception("User not found.");
        if (!await _userManager.CheckPasswordAsync(user, password))
        {
            throw new Exception("Invalid password.");
        }

        await _userManager.SetTwoFactorEnabledAsync(user, false);
        await _userManager.ResetAuthenticatorKeyAsync(user);
        await WriteSecurityEventAsync(user, "TwoFactorDisabled", cancellationToken);
    }

    public async Task<LogoutResultDto> LogoutAsync(
        string userId,
        CancellationToken cancellationToken = default
        )
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user != null)
        {
            var tokens = await _context.Token.Where(x => x.UserId == user.Id).ToListAsync(cancellationToken);
            foreach (var item in tokens)
            {
                _context.Remove(item);
            }
            await _context.SaveChangesAsync(cancellationToken);
        }

        return new LogoutResultDto
        {
            UserId = user?.Id,
            Email = user?.Email,
            FirstName = user?.FirstName,
            LastName = user?.LastName,
            CompanyName = user?.CompanyName,
            UserClaims = null,
            AccessToken = null,
            RefreshToken = null,

        };
    }
    public async Task<RegisterResultDto> RegisterAsync(
        string email,
        string password,
        string confirmPassword,
        string firstName,
        string lastName,
        string companyName = "",
        CancellationToken cancellationToken = default
        )
    {
        if (!password.Equals(confirmPassword))
        {
            throw new Exception($"Password and ConfirmPassword is different.");
        }

        var user = new ApplicationUser(
            email,
            firstName,
            lastName,
            companyName
        );

        user.EmailConfirmed = !_identitySettings.SignIn.RequireConfirmedEmail;
        var result = await _userManager.CreateAsync(user, password);

        if (!result.Succeeded)
        {
            throw new Exception(string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        if (!await _userManager.IsInRoleAsync(user, RoleHelper.GetProfileRole()))
        {
            await _userManager.AddToRoleAsync(user, RoleHelper.GetProfileRole());
        }

        var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

        if (_identitySettings.SignIn.RequireConfirmedEmail)
        {
            var request = _httpContextAccessor?.HttpContext?.Request;
            var callbackUrl = $"{request?.Scheme}://{request?.Host}/Accounts/EmailConfirm?email={user.Email}&code={code}";
            var encodeCallbackUrl = $"{HtmlEncoder.Default.Encode(callbackUrl)}";

            var emailSubject = $"Confirm your email";
            var emailMessage = $"Please confirm your account by <a href='{encodeCallbackUrl}'>clicking here</a>.";

            await _emailService.SendEmailAsync(user.Email ?? "", emailSubject, emailMessage);

        }

        return new RegisterResultDto
        {
            UserId = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            CompanyName = user.CompanyName
        };
    }

    public async Task<string> ConfirmEmailAsync(
        string email,
        string code,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(email);

        if (user == null)
        {
            throw new Exception($"Unable to load user with email: {email}");
        }

        code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
        var result = await _userManager.ConfirmEmailAsync(user, code);

        if (!result.Succeeded)
        {
            throw new Exception($"Error confirming your email: {email}");
        }

        return email;
    }

    public async Task<string> ForgotPasswordAsync(
        string email,
        CancellationToken cancellationToken = default
        )
    {
        var user = await _userManager.FindByEmailAsync(email);

        if (user == null)
        {
            throw new Exception($"Unable to load user with email: {email}");
        }

        var code = await _userManager.GeneratePasswordResetTokenAsync(user);
        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

        var textTempPassword = Guid.NewGuid().ToString().Substring(0, _identitySettings.Password.RequiredLength);
        var encryptedTempPassword = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(textTempPassword));

        var request = _httpContextAccessor?.HttpContext?.Request;
        var callbackUrl = $"{request?.Scheme}://{request?.Host}/Accounts/ForgotPasswordConfirmation?email={user.Email}&code={code}&tempPassword={encryptedTempPassword}";
        var encodeCallbackUrl = $"{HtmlEncoder.Default.Encode(callbackUrl)}";

        var emailSubject = $"Forgot password confirmation";
        var emailMessage = $"Your temporary password is: <strong>{textTempPassword}</strong>. Please confirm reset your password by <a href='{encodeCallbackUrl}'>clicking here</a>.";

        await _emailService.SendEmailAsync(user.Email ?? "", emailSubject, emailMessage);

        return "A temporary password has been sent to the registered email address.";

    }

    public async Task<string> ForgotPasswordConfirmationAsync(
        string email,
        string tempPassword,
        string code,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(email);

        if (user == null)
        {
            throw new Exception($"Unable to load user with email: {email}");
        }

        code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
        tempPassword = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(tempPassword));
        var result = await _userManager.ResetPasswordAsync(user, code, tempPassword);

        if (!result.Succeeded)
        {
            throw new Exception($"Error resetting your password");
        }

        return email;
    }

    public async Task<RefreshTokenResultDto> RefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken
        )
    {
        var registeredToken = await _context.Token.SingleOrDefaultAsync(x => x.RefreshToken == refreshToken, cancellationToken);
        if (registeredToken == null)
        {
            throw new Exception("Refresh token invalid, please re-login");
        }
        var user = await _userManager.FindByIdAsync(registeredToken?.UserId ?? "");
        if (user == null)
        {
            throw new Exception("Refresh token invalid, please re-login");
        }
        _context.Token.Remove(registeredToken!);

        var roles = await _userManager.GetRolesAsync(user);
        var newAccessToken = _tokenService.GenerateToken(user, roles.Select(r => new System.Security.Claims.Claim("role", r)).ToList());
        var newRefreshToken = _tokenService.GenerateRefreshToken();

        var token = new Token();
        token.UserId = user.Id;
        token.RefreshToken = newRefreshToken;
        token.ExpiryDate = DateTime.UtcNow.AddDays(TokenConsts.ExpiryInDays);
        token.IsDeleted = false;
        token.CreatedAtUtc = DateTime.UtcNow;
        token.CreatedById = user.Id;
        await _context.AddAsync(token, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        return new RefreshTokenResultDto
        {
            UserId = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            CompanyName = user.CompanyName,
            AccessToken = newAccessToken,
            RefreshToken = newRefreshToken,
            MenuNavigation = NavigationTreeStructure.GetCompleteMenuNavigationTreeNode(),
            Roles = roles.ToList(),
            Avatar = user.ProfilePictureName
        };
    }

    public async Task<List<GetMyProfileListResultDto>> GetMyProfileListAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var profiles = await _context.Users
            .Where(x => x.Id == userId)
            .Select(x => new GetMyProfileListResultDto
            {
                Id = x.Id,
                FirstName = x.FirstName,
                LastName = x.LastName,
                CompanyName = x.CompanyName
            })
            .ToListAsync(cancellationToken);

        return profiles;
    }

    public async Task UpdateMyProfileAsync(
        string userId,
        string firstName,
        string lastName,
        string companyName,
        CancellationToken cancellationToken
        )
    {
        var user = await _context.Users.Where(x => x.Id == userId).SingleOrDefaultAsync(cancellationToken);

        if (user == null)
        {
            throw new Exception($"Unable to load user with id: {userId}");
        }

        user.FirstName = firstName;
        user.LastName = lastName;
        user.CompanyName = companyName;

        _context.Update(user);
        await _context.SaveChangesAsync();
    }
    public async Task ChangePasswordAsync(
        string userId,
        string oldPassword,
        string newPassword,
        string confirmNewPassword,
        CancellationToken cancellationToken
    )
    {
        if (newPassword != confirmNewPassword)
        {
            throw new Exception("New password and confirm password do not match.");
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            throw new Exception($"Unable to load user with id: {userId}");
        }

        var result = await _userManager.ChangePasswordAsync(user, oldPassword, newPassword);

        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new Exception($"Password change failed: {errors}");
        }

        var isDemoVersion = _configuration.GetValue<bool>("IsDemoVersion");
        if (isDemoVersion && user.Email == _identitySettings.DefaultAdmin.Email)
        {
            throw new Exception($"Update default admin password is not allowed in demo version.");
        }
    }

    public async Task<List<GetRoleListResultDto>> GetRoleListAsync(
        CancellationToken cancellationToken
    )
    {
        var roles = await _roleManager.Roles
            .Select(x => new GetRoleListResultDto
            {
                Id = x.Id,
                Name = x.Name ?? string.Empty
            })
            .ToListAsync(cancellationToken);

        return roles;
    }

    public async Task<List<GetUserListResultDto>> GetUserListAsync(
        CancellationToken cancellationToken
        )
    {
        var users = await _userManager.Users
            .Select(x => new GetUserListResultDto
            {
                Id = x.Id,
                FirstName = x.FirstName,
                LastName = x.LastName,
                Email = x.Email,
                IsBlocked = x.IsBlocked,
                IsDeleted = x.IsDeleted,
                EmailConfirmed = x.EmailConfirmed,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return users;
    }

    public async Task<CreateUserResultDto> CreateUserAsync(
        string email,
        string password,
        string confirmPassword,
        string firstName,
        string lastName,
        bool emailConfirmed = true,
        bool isBlocked = false,
        bool isDeleted = false,
        string createdById = "",
        CancellationToken cancellationToken = default
        )
    {
        if (!password.Equals(confirmPassword))
        {
            throw new Exception($"Password and ConfirmPassword is different.");
        }

        var user = new ApplicationUser(
            email,
            firstName,
            lastName
        );

        user.EmailConfirmed = emailConfirmed;
        user.IsBlocked = isBlocked;
        user.IsDeleted = isDeleted;
        user.CreatedById = createdById;

        var result = await _userManager.CreateAsync(user, password);

        if (!result.Succeeded)
        {
            throw new Exception(string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        if (!await _userManager.IsInRoleAsync(user, RoleHelper.GetProfileRole()))
        {
            await _userManager.AddToRoleAsync(user, RoleHelper.GetProfileRole());
        }

        return new CreateUserResultDto
        {
            UserId = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            EmailConfirmed = user.EmailConfirmed,
            IsBlocked = user.IsBlocked,
            IsDeleted = user.IsDeleted,
        };
    }

    public async Task<UpdateUserResultDto> UpdateUserAsync(
        string userId,
        string firstName,
        string lastName,
        bool emailConfirmed = true,
        bool isBlocked = false,
        bool isDeleted = false,
        string updatedById = "",
        CancellationToken cancellationToken = default
        )
    {
        var user = await _context.Users.Where(x => x.Id == userId).SingleOrDefaultAsync(cancellationToken);

        if (user == null)
        {
            throw new Exception($"Unable to load user with id: {userId}");
        }

        if (user.Email == _identitySettings.DefaultAdmin.Email)
        {
            throw new Exception($"Update default admin is not allowed.");
        }

        user.FirstName = firstName;
        user.LastName = lastName;
        user.EmailConfirmed = emailConfirmed;
        user.IsBlocked = isBlocked;
        user.IsDeleted = isDeleted;
        user.UpdatedById = updatedById;

        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            throw new Exception(string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        return new UpdateUserResultDto
        {
            UserId = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            EmailConfirmed = user.EmailConfirmed,
            IsBlocked = user.IsBlocked,
            IsDeleted = user.IsDeleted,
        };
    }

    public async Task<DeleteUserResultDto> DeleteUserAsync(
        string userId,
        string deletedById = "",
        CancellationToken cancellationToken = default
        )
    {
        var user = await _context.Users.Where(x => x.Id == userId).SingleOrDefaultAsync(cancellationToken);

        if (user == null)
        {
            throw new Exception($"Unable to load user with id: {userId}");
        }

        if (user.Email == _identitySettings.DefaultAdmin.Email)
        {
            throw new Exception($"Update default admin is not allowed.");
        }

        user.IsDeleted = true;
        user.UpdatedById = deletedById;

        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            throw new Exception(string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        return new DeleteUserResultDto
        {
            UserId = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            EmailConfirmed = user.EmailConfirmed,
            IsBlocked = user.IsBlocked,
            IsDeleted = user.IsDeleted,
        };
    }

    public async Task UpdatePasswordUserAsync(
        string userId,
        string newPassword,
        CancellationToken cancellationToken
        )
    {

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            throw new Exception($"Unable to load user with id: {userId}");
        }

        var isDemoVersion = _configuration.GetValue<bool>("IsDemoVersion");
        if (isDemoVersion && user.Email == _identitySettings.DefaultAdmin.Email)
        {
            throw new Exception($"Update default admin password is not allowed in demo version.");
        }

        var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);

        var result = await _userManager.ResetPasswordAsync(user, resetToken, newPassword);

        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new Exception($"Password change failed: {errors}");
        }
    }

    public async Task<List<string>> GetUserRolesAsync(
        string userId,
        CancellationToken cancellationToken = default
        )
    {
        var user = await _userManager.FindByIdAsync(userId);

        if (user == null)
        {
            throw new Exception($"Unable to load user with id: {userId}");
        }

        var roles = await _userManager.GetRolesAsync(user);
        return roles.ToList();
    }

    public async Task<List<string>> UpdateUserRoleAsync(
            string userId,
            string roleName,
            bool accessGranted,
            CancellationToken cancellationToken = default
        )
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            throw new Exception($"Unable to load user with id: {userId}");
        }

        if (user.Email == _identitySettings.DefaultAdmin.Email)
        {
            throw new Exception($"Update default admin is not allowed.");
        }

        var currentRoles = await _userManager.GetRolesAsync(user);
        if (accessGranted)
        {
            if (!currentRoles.Contains(roleName))
            {
                var result = await _userManager.AddToRoleAsync(user, roleName);
                if (!result.Succeeded)
                {
                    throw new Exception($"Failed to add role '{roleName}' to user with id: {userId}. Errors: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                }
            }
        }
        else
        {
            if (currentRoles.Contains(roleName))
            {
                var result = await _userManager.RemoveFromRoleAsync(user, roleName);
                if (!result.Succeeded)
                {
                    throw new Exception($"Failed to remove role '{roleName}' from user with id: {userId}. Errors: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                }
            }
        }

        var updatedRoles = await _userManager.GetRolesAsync(user);
        return updatedRoles.ToList();
    }



    public async Task ChangeAvatarAsync(
        string userId,
        string avatar,
        CancellationToken cancellationToken
        )
    {
        var user = await _context.Users.Where(x => x.Id == userId).SingleOrDefaultAsync(cancellationToken);

        if (user == null)
        {
            throw new Exception($"Unable to load user with id: {userId}");
        }

        user.ProfilePictureName = avatar;

        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            throw new Exception(string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }

}
