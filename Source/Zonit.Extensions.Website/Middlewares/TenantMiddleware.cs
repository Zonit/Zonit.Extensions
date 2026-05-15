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
internal sealed class TenantMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext httpContext, ITenantRepository repository)
    {
        if (WebsiteRequestFilter.ShouldSkip(httpContext))
        {
            await next(httpContext);
            return;
        }

        if (repository.Current is null)
        {
            // ITenantSource is optional. Resolved via RequestServices (not constructor
            // injection) so that solo-site hosts don't need to register one.
            var manager = httpContext.RequestServices.GetService(typeof(ITenantSource)) as ITenantSource;

            if (manager is null)
            {
                // Solo site shape — no per-domain resolution needed.
                repository.Initialize(Tenant.Solo);
            }
            else
            {
                var domain = httpContext.Request.Host.Host;
                Tenant? resolved = null;
                if (!string.IsNullOrEmpty(domain))
                    resolved = await repository.InitializeAsync(domain, httpContext.RequestAborted);

                // Manager said "no match" — fall back to Solo so downstream pages still
                // get defaults instead of a null Tenant. The host can still tell solo
                // from real-tenant via Tenant.IsSolo if it cares.
                if (resolved is null)
                    repository.Initialize(Tenant.Solo);
            }
        }

        await next(httpContext);
    }
}
