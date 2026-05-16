using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Zonit.Extensions.Website.Layouts.Components;
using Zonit.Extensions.Website.Layouts.Repositories;
using Zonit.Extensions.Website.Layouts.Services;

namespace Zonit.Extensions;

/// <summary>
/// DI surface for the string-keyed layout system
/// (<c>LayoutKeyAttribute</c>, <c>NoLayoutAttribute</c>, <c>ILayoutContext</c>, <c>ILayoutRegistry</c>).
/// </summary>
public static class ServiceCollectionLayoutsExtensions
{
    /// <summary>
    /// Wires the layout subsystem. Idempotent — safe to call multiple times. Invoked
    /// automatically by <c>services.AddWebsite(...)</c>; standalone consumers (e.g.
    /// a non-web host that still wants <see cref="ILayoutRegistry"/>) can call this
    /// directly.
    /// </summary>
    /// <remarks>
    /// <para>Also seeds the canonical built-in layout key <c>"Zonit.Minimal"</c> with
    /// <see cref="ZonitMinimalLayout"/> so plug-ins always have a safe fallback. Hosts
    /// can overwrite the binding later with their own minimal layout — last writer wins
    /// (see <see cref="ILayoutRegistry.Register"/>).</para>
    /// </remarks>
    public static IServiceCollection AddLayoutsExtension(this IServiceCollection services)
    {
        // Factory consumes all LayoutSeed singletons collected via AddWebsiteLayout<T>(...)
        // and materialises them into the runtime map exactly once on first resolve.
        // No BuildServiceProvider() during configuration → no duplicate containers.
        services.TryAddSingleton<ILayoutRegistry>(sp =>
        {
            var registry = new LayoutRegistry();
            foreach (var seed in sp.GetServices<LayoutSeed>())
                registry.Register(seed.Key, seed.LayoutType);
            return registry;
        });

        services.TryAddScoped<Zonit.Extensions.Website.ILayoutContext, LayoutContext>();

        services.AddWebsiteLayout<ZonitMinimalLayout>("Zonit.Minimal");

        return services;
    }

    /// <summary>
    /// Registers a layout type under the given string key. The key is what plug-in
    /// pages reference via <c>[LayoutKey("...")]</c>; the layout type is whatever
    /// <see cref="LayoutComponentBase"/> derivative the host provides.
    /// </summary>
    /// <typeparam name="TLayout">
    /// The concrete layout component. Generic + <c>new()</c> constraint keeps the
    /// trimmer aware of it; no reflection needed at runtime.
    /// </typeparam>
    /// <param name="services">The DI service collection.</param>
    /// <param name="key">Case-insensitive layout key. Convention: <c>"Area.Purpose"</c>
    /// (e.g. <c>"Auth.LoginBox"</c>, <c>"Dashboard.Minimal"</c>).</param>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection AddWebsiteLayout<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors
                                  | DynamicallyAccessedMemberTypes.PublicProperties)]
        TLayout>(this IServiceCollection services, string key)
        where TLayout : LayoutComponentBase
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        // Seed pattern: record the registration as a singleton, materialised on first
        // ILayoutRegistry resolve by the factory installed in AddLayoutsExtension.
        // Multiple seeds with the same key are allowed; last writer wins (see Register).
        services.AddSingleton(new LayoutSeed(key, typeof(TLayout)));

        return services;
    }
}
