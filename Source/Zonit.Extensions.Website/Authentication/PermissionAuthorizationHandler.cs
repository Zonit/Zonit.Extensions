using Microsoft.AspNetCore.Authorization;

namespace Zonit.Extensions.Website.Authentication;

/// <summary>
/// Authorizes <see cref="RequirePermissionAttribute"/> requirements by inspecting
/// <see cref="IdentityClaimsBuilder.PermissionClaimType"/> claims on the current principal
/// and evaluating wildcards via <see cref="Permission.Implies"/>.
/// </summary>
public sealed class PermissionAuthorizationHandler
    : AuthorizationHandler<RequirePermissionAttribute>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        RequirePermissionAttribute requirement)
    {
        if (context.User?.Identity is not { IsAuthenticated: true })
            return Task.CompletedTask;

        foreach (var claim in context.User.FindAll(IdentityClaimsBuilder.PermissionClaimType))
        {
            if (!Permission.TryCreate(claim.Value, out var granted))
                continue;

            if (granted.Implies(requirement.Permission))
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }
        }

        return Task.CompletedTask;
    }
}
