using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Zonit.Extensions.Tenants;
using Zonit.Extensions.Tenants.Repositories;
using Zonit.Extensions.Tenants.Services;
using Zonit.Extensions.Tenants.Settings;

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
        => services.AddTenantsExtension(static _ => { });

    /// <summary>
    /// Registers the per-scope tenant state machine and configures the JSON contract every
    /// <see cref="Zonit.Extensions.Tenants.Settings.Setting{T}"/> is hydrated through.
    /// </summary>
    /// <remarks>
    /// <para>The usual call registers the application's existing source-generated context so
    /// hydration needs no reflection:</para>
    /// <code>
    /// builder.Services.AddTenantsExtension(o => o.AddJsonContext(AppJsonContext.Default));
    /// </code>
    /// <para>Registering nothing is supported and is the right choice for an app that does not
    /// publish with Native AOT or trimming — settings then hydrate reflectively, which needs no
    /// ceremony per setting at all. See
    /// <see cref="Zonit.Extensions.Tenants.Settings.Setting{T}.Hydrate"/> for the trade.</para>
    ///
    /// <para><b>Configuration composes, registrations do not.</b> Every call's
    /// <paramref name="configure"/> runs against the same
    /// <see cref="TenantSettingsOptions"/> instance, so a host and an area can each contribute a
    /// context; the service registrations themselves stay <c>TryAdd</c>-based and idempotent, so
    /// calling this directly <i>and</i> through <c>AddWebsite()</c> costs nothing.</para>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Adds JSON metadata sources for setting models.</param>
    public static IServiceCollection AddTenantsExtension(
        this IServiceCollection services,
        Action<TenantSettingsOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        // The options object is registered as a singleton *instance* and mutated in place rather
        // than composed through IConfigureOptions<>, because it has to be reachable from this
        // method on every call — an area that adds its context after the host already called
        // AddTenantsExtension must contribute to the same instance, not to a second one that
        // TryAddSingleton would silently discard.
        var options = (TenantSettingsOptions?)services
            .FirstOrDefault(d => d.ServiceType == typeof(TenantSettingsOptions))?.ImplementationInstance;

        if (options is null)
        {
            options = new TenantSettingsOptions();
            services.AddSingleton(options);
        }

        configure(options);

        services.TryAddSingleton<ITenantSettingsSerializer, TenantSettingsSerializer>();
        services.TryAddSingleton<TenantConfigurationSource>();
        services.TryAddScoped<ITenantRepository, TenantRepository>();
        services.TryAddScoped<ITenantProvider, TenantService>();
        return services;
    }
}
