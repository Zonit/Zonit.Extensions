using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Zonit.Extensions.Auth;
using Zonit.Extensions.Auth.Repositories;
using Zonit.Extensions.Auth.Services;

namespace Zonit.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the framework-agnostic Auth core: the scoped
    /// <see cref="IAuthenticatedRepository"/> / <see cref="IAuthenticatedProvider"/> pair
    /// that carries the current <see cref="Identity"/> through a unit of work
    /// (request / circuit / job).
    /// </summary>
    /// <remarks>
    /// <para>This package has no opinion about how the identity is initialised. ASP.NET Core
    /// hosts get cookie-based hydration, scheme registration, authorization handlers,
    /// <c>[RequirePermission]</c> / <c>[RequireRole]</c>, and the Blazor
    /// <c>AuthenticationStateProvider</c> wiring from <c>Zonit.Extensions.Website</c> via
    /// <c>AddWebsite()</c>. Non-web hosts (console / mobile / WASM client) call this method
    /// and populate the repository directly through <see cref="IAuthenticatedRepository.Initialize"/>.</para>
    ///
    /// <para>Idempotent — every registration uses <c>TryAdd</c> so callers can layer their
    /// own implementations on top without worrying about ordering.</para>
    /// </remarks>
    public static IServiceCollection AddAuthExtension(this IServiceCollection services)
    {
        services.TryAddScoped<IAuthenticatedRepository, AuthenticatedRepository>();
        services.TryAddScoped<IAuthenticatedProvider, AuthenticatedService>();
        return services;
    }
}