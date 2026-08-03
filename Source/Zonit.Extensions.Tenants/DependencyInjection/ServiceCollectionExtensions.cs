using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Zonit.Extensions.Tenants;
using Zonit.Extensions.Tenants.Repositories;
using Zonit.Extensions.Tenants.Services;

namespace Zonit.Extensions;

/// <summary>
/// DI surface for <see cref="Zonit.Extensions.Tenants"/>. Registers the scoped
/// <see cref="ITenantRepository"/> + <see cref="ITenantProvider"/> pair. The consumer
/// is expected to register their own <see cref="ITenantSource"/> implementation
/// (data source) — without it the providers stay empty.
/// </summary>
public static class TenantsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the per-scope tenant state machine. Idempotent (TryAdd-based) so
    /// consumers calling it directly + via <c>AddWebsite()</c> incur no penalty.
    /// </summary>
    /// <remarks>
    /// <para><b>No <see cref="ITenantSource"/> is registered here on purpose.</b> Up to
    /// 10.0.0-preview.9 this method <c>TryAdd</c>ed a <c>NullTenantSource</c> that answered
    /// <see langword="null"/> for every host. That made <c>ITenantSource</c> permanently
    /// resolvable, so <c>TenantMiddleware</c>'s "solo site — nothing registered" branch could
    /// never run: every single-site request instead paid an async round trip to the null
    /// source, got <see langword="null"/> back (raising <c>OnChange</c>), and then fell back to
    /// <see cref="Tenant.Solo"/> (raising <c>OnChange</c> a second time) — which re-ran every
    /// component's data load twice per render pass. Leaving the seam unregistered lets the
    /// middleware's own null check do the job it documents.</para>
    /// </remarks>
    public static IServiceCollection AddTenantsExtension(this IServiceCollection services)
    {
        services.TryAddScoped<ITenantRepository, TenantRepository>();
        services.TryAddScoped<ITenantProvider, TenantService>();
        return services;
    }
}
