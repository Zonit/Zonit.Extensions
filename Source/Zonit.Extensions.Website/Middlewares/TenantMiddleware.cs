using Microsoft.AspNetCore.Http;
using Zonit.Extensions.Tenants;

namespace Zonit.Extensions.Website.Middlewares;

/// <summary>
/// Resolves the per-host <see cref="Tenant"/> on the first non-static request of each
/// scope and stores it in the scoped <see cref="ITenantRepository"/>. Downstream
/// consumers (<see cref="ITenantProvider"/>, <c>TenantSettings</c> facade) observe the
/// resolved snapshot via <see cref="ITenantRepository.OnChange"/>.
/// </summary>
/// <remarks>
/// <para><b>Static asset bypass.</b> Same logic as <see cref="WorkspaceMiddleware"/> /
/// <see cref="ProjectMiddleware"/> — see <see cref="WebsiteRequestFilter"/> for the
/// motivation. Tenant resolution incurs a consumer-side <c>ITenantSource</c> call which
/// usually means a database round trip. We have to keep that off the hot path of static
/// asset serving.</para>
///
/// <para><b>Anonymous traffic.</b> Unlike <see cref="WorkspaceMiddleware"/>, tenants are
/// orthogonal to authentication — the home page of a white-label SaaS still needs to
/// know which brand / theme to render before any login happens. So this middleware does
/// <i>not</i> short-circuit on anonymous requests.</para>
///
/// <para><b>Solo / multi-site auto-detection.</b> The middleware is happy in either
/// shape:</para>
/// <list type="bullet">
///   <item><b>Solo site</b> (most apps): no <see cref="ITenantSource"/> registered.
///         The middleware seeds the scoped repository with <see cref="Tenant.Solo"/>
///         (id = <see cref="Guid.Empty"/>, domain = <c>"*"</c>) so settings always
///         surface their defaults via the standard <see cref="ITenantProvider"/> API
///         — zero ceremony for the host.</item>
///   <item><b>Multi-site</b> (white-label SaaS): host registers an
///         <see cref="ITenantSource"/>, the middleware resolves by host name. When
///         the manager doesn't recognise the host, it falls back to <see cref="Tenant.Solo"/>
///         rather than <see langword="null"/> — pages still render with defaults
///         instead of crashing on null-deref.</item>
/// </list>
/// </remarks>
internal sealed class TenantMiddleware(RequestDelegate next, WebsiteOptions options)
{
    public async Task InvokeAsync(HttpContext httpContext, ITenantRepository repository)
    {
        if (WebsiteRequestFilter.ShouldSkip(httpContext))
        {
            await next(httpContext);
            return;
        }

        // The repository starts every scope on Tenant.Default and is idempotent per host, so
        // there is nothing to branch on here any more: no "is it seeded yet" check, no
        // solo-versus-multi split, no fallback assignment. A single-site host has no
        // ITenantSource registered and InitializeAsync returns immediately; a multi-site host
        // that does not recognise the host leaves the scope on Tenant.Default. Both used to be
        // spelled out as explicit Initialize(Tenant.Solo) calls, and the second one raised a
        // *second* OnChange for one logical resolution — every subscriber (the hydrated-settings
        // cache, every component's OnRefreshChangeAsync) ran twice per request because of it.
        await repository.InitializeAsync(httpContext.Request.Host.Host, httpContext.RequestAborted);

        // An ITenantSource was asked about this host and did not recognise it. Carrying on would
        // serve default branding on a domain the application knows nothing about — the failure
        // mode a multi-domain host is least likely to notice, because it looks like a working
        // site. The repository has already logged it; this is the part that stops it.
        if (repository.Resolution is TenantResolution.Unknown
            && options.UnknownHost is UnknownHostBehavior.NotFound)
        {
            httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await next(httpContext);
    }
}
