using Microsoft.AspNetCore.Components.Authorization;
using Zonit.Extensions.Auth;

namespace Zonit.Extensions.Website.Authentication;

/// <summary>
/// Blazor <see cref="AuthenticationStateProvider"/> backed by the scoped
/// <see cref="IAuthenticatedProvider"/> (filled by <see cref="Middlewares.SessionMiddleware"/>).
/// Principal construction is delegated to <see cref="IdentityClaimsBuilder"/> so that
/// Roles and Permissions claims match the cookie-based path 1:1.
/// </summary>
/// <remarks>
/// Subscribes to <see cref="IAuthenticatedProvider.OnChange"/> so that a sign-in
/// or sign-out within a long-lived Blazor Server circuit immediately refreshes
/// <c>&lt;AuthorizeView&gt;</c> and any cascading <c>Task&lt;AuthenticationState&gt;</c>
/// — previously these were stale until a full page reload.
/// </remarks>
internal sealed class SessionAuthenticationService : AuthenticationStateProvider, IDisposable
{
    private readonly IAuthenticatedProvider _authenticated;

    public SessionAuthenticationService(IAuthenticatedProvider authenticated)
    {
        _authenticated = authenticated;
        _authenticated.OnChange += HandleIdentityChanged;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var principal = IdentityClaimsBuilder.BuildPrincipal(_authenticated.Current);
        return Task.FromResult(new AuthenticationState(principal));
    }

    private void HandleIdentityChanged()
        => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());

    public void Dispose()
    {
        _authenticated.OnChange -= HandleIdentityChanged;
    }
}