using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace Zonit.Extensions.Website.Authentication;

/// <summary>
/// Authorizes <see cref="RequireRoleAttribute"/> by inspecting <see cref="ClaimTypes.Role"/>
/// claims on the current principal.
/// </summary>
public sealed class RoleAuthorizationHandler
    : AuthorizationHandler<RequireRoleAttribute>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        RequireRoleAttribute requirement)
    {
        if (context.User?.Identity is not { IsAuthenticated: true })
            return Task.CompletedTask;

        var target = requirement.Role.Value;

        foreach (var claim in context.User.FindAll(ClaimTypes.Role))
        {
            if (Role.TryCreate(claim.Value, out var r) && r.Value == target)
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }
        }

        return Task.CompletedTask;
    }
}
