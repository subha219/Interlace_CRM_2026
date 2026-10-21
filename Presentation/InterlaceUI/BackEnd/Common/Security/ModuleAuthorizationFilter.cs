using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace InterlaceUI.BackEnd.Common.Security;

/// <summary>
/// Server-side permission check: the signed-in user must hold one of the module roles that
/// <see cref="ModulePermissions"/> lists for the controller/action. The browser-side page check
/// stays as a convenience, but this is the enforcement point.
/// </summary>
public class ModuleAuthorizationFilter : IAsyncAuthorizationFilter
{
    private readonly ILogger<ModuleAuthorizationFilter> _logger;

    public ModuleAuthorizationFilter(ILogger<ModuleAuthorizationFilter> logger)
    {
        _logger = logger;
    }

    private static bool HasRole(System.Security.Claims.ClaimsPrincipal user, string role)
    {
        if (user.IsInRole(role)) return true;
        return user.Claims.Any(c =>
            (c.Type == "role" || c.Type == "roles" || c.Type == System.Security.Claims.ClaimTypes.Role) &&
            string.Equals(c.Value, role, StringComparison.OrdinalIgnoreCase));
    }

    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;

        //>>> anonymous callers are handled by [Authorize] / [AllowAnonymous]
        if (user.Identity?.IsAuthenticated != true)
        {
            return Task.CompletedTask;
        }

        if (context.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any())
        {
            return Task.CompletedTask;
        }

        if (context.ActionDescriptor is not ControllerActionDescriptor descriptor)
        {
            return Task.CompletedTask;
        }

        var required = ModulePermissions.RequiredRoles(
            descriptor.ControllerName,
            descriptor.ActionName,
            context.HttpContext.Request.Method);

        if (required == null || required.Any(r => HasRole(user, r)))
        {
            return Task.CompletedTask;
        }

        _logger.LogWarning(
            "Access denied: user {UserId} called {Controller}.{Action} ({Method}) without any of the roles [{Roles}]",
            user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            descriptor.ControllerName,
            descriptor.ActionName,
            context.HttpContext.Request.Method,
            string.Join(", ", required));

        context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
        return Task.CompletedTask;
    }
}
