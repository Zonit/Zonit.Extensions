using Microsoft.AspNetCore.Components.Authorization;
using Zonit.Extensions.Auth.Authorization;

namespace Zonit.Extensions.Auth.Services;

/// <summary>
/// Blazor <see cref="AuthenticationStateProvider"/> backed by the scoped
/// <see cref="IAuthenticatedProvider"/> (filled by <see cref="Middlewares.SessionMiddleware"/>).
/// Principal construction is delegated to <see cref="IdentityClaimsBuilder"/> so that
/// Roles and Permissions claims match the cookie-based path 1:1.
/// </summary>
internal sealed class SessionAuthenticationService(IAuthenticatedProvider authenticated)
    : AuthenticationStateProvider
{
    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var principal = IdentityClaimsBuilder.BuildPrincipal(authenticated.Current);
        return Task.FromResult(new AuthenticationState(principal));
    }
}