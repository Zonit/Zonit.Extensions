using Microsoft.AspNetCore.Http;
using Zonit.Extensions.Auth.Repositories;
using Zonit.Extensions.Website.Authentication;

namespace Zonit.Extensions.Website.Middlewares;

/// <summary>
/// Hydrates the scoped <see cref="IAuthenticatedRepository"/> from the
/// <see cref="HttpContext.User"/> populated by <c>UseAuthentication</c>. <b>Does not
/// query <see cref="Auth.IAuthSource"/> on its own</b> — the cookie-to-identity
/// round trip already happened in <see cref="AuthenticationSchemeService"/> (cached
/// per-request by ASP.NET's <c>IAuthenticationService</c>).
/// </summary>
/// <remarks>
/// <para><b>Why this matters.</b> The previous implementation also pulled the
/// <c>Session</c> cookie and called <c>IAuthSource.GetByTokenAsync</c>, doubling
/// every database lookup that <see cref="AuthenticationSchemeService"/> already
/// performed. With this rewrite the auth round trip happens once per HTTP request,
/// here we just project the cached <see cref="System.Security.Claims.ClaimsPrincipal"/>
/// back into our <see cref="Identity"/> value object.</para>
///
/// <para><b>Static-asset bypass.</b> A page navigation triggers dozens of follow-up
/// asset requests; running auth glue on each of them gains nothing.
/// <see cref="WebsiteRequestFilter"/> short-circuits those.</para>
///
/// <para><b>Pipeline contract.</b> Must run after <c>UseAuthentication</c> so that
/// <see cref="HttpContext.User"/> is already authenticated. <see cref="WorkspaceMiddleware"/>
/// and <see cref="ProjectMiddleware"/> sit downstream and depend on the populated
/// <see cref="IAuthenticatedRepository"/>.</para>
/// </remarks>
internal sealed class SessionMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext httpContext, IAuthenticatedRepository repository)
    {
        // Static / framework / library files: nothing to hydrate.
        if (WebsiteRequestFilter.ShouldSkip(httpContext))
            return next(httpContext);

        // Idempotent: AuthenticationStateProvider on a Blazor circuit may trigger this
        // path more than once for re-renders; we only project claims into the repository
        // when it is still empty for the current scope.
        if (!repository.Current.HasValue)
        {
            var identity = IdentityClaimsBuilder.Read(httpContext.User);
            if (identity.HasValue)
                repository.Initialize(identity);
        }

        return next(httpContext);
    }
}