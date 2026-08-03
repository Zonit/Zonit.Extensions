using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Zonit.Extensions.Website;
using Zonit.Extensions.Website.Navigations.Services;

namespace Zonit.Extensions;

public static class ServiceCollectionNavigationsExtensions
{
    /// <summary>
    /// Registers the navigation store (singleton) and the Site-aware
    /// <see cref="INavigationProvider"/> facade (transient).
    /// </summary>
    /// <remarks>
    /// <para><see cref="INavigationProvider"/> is <b>transient</b>, which is what lets the two
    /// things consumers do with it both keep working:</para>
    /// <list type="bullet">
    ///   <item><b>Seeding from a singleton.</b> <c>AddHostedService&lt;NavData&gt;()</c> with
    ///     <c>NavData(INavigationProvider nav)</c> resolves the provider from the root — allowed
    ///     for a transient, rejected for a scoped one. Everything it <c>Add</c>s lands in the
    ///     process-wide store and stays visible afterwards.</item>
    ///   <item><b>Per-Site filtering while rendering.</b> A component that injects the provider
    ///     gets an instance bound to its own request or circuit scope, so
    ///     <see cref="INavigationProvider.Get"/> can consult that scope's <c>ICurrentSite</c> and
    ///     hide areas that are not mounted on the Site being rendered.</item>
    /// </list>
    ///
    /// <para><b>Standalone use is supported.</b> Calling this method without
    /// <c>AddWebsite()</c> produces a container that builds and validates: the area registry is
    /// an optional dependency of the store and <c>ICurrentSite</c> is looked up with
    /// <c>GetService</c>. What you get is the runtime half of the feature — <c>Add</c>,
    /// <c>Clear</c>, <c>Refresh</c>, <c>OnChanged</c> and an unfiltered <c>Get</c>. Static
    /// per-area contributions (<c>IWebsiteArea.Navigation</c>) and per-Site filtering both need
    /// <c>AddWebsite()</c>, because that is what registers the areas and the Site marker.</para>
    /// </remarks>
    public static IServiceCollection AddNavigationsExtension(this IServiceCollection services)
    {
        services.TryAddSingleton<NavigationRegistry>();

        // Singleton on purpose — that is the mechanism, not a caching decision: the provider
        // handed to a singleton's constructor is always the root one. See NavigationRootScope.
        services.TryAddSingleton<NavigationRootScope>();

        services.TryAddTransient<INavigationProvider, NavigationService>();
        return services;
    }
}
