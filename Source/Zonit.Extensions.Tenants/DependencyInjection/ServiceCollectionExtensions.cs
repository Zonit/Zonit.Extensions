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
    public static IServiceCollection AddTenantsExtension(this IServiceCollection services)
    {
        services.TryAddScoped<ITenantRepository, TenantRepository>();
        services.TryAddScoped<ITenantProvider, TenantService>();

        // Safety net: no consumer source → middleware falls back to solo-mode.
        services.TryAddScoped<ITenantSource, NullTenantSource>();
        return services;
    }
}
