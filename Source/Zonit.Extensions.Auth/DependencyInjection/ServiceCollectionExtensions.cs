using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Zonit.Extensions.Auth;
using Zonit.Extensions.Auth.Authorization;
using Zonit.Extensions.Auth.Repositories;
using Zonit.Extensions.Auth.Services;

namespace Zonit.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers Zonit authentication / authorization on top of the standard ASP.NET Core
    /// stack. After calling this method the consumer can use the full Microsoft authorization
    /// surface (<c>[Authorize]</c>, <c>AuthorizeView</c>, <c>RequireAuthorization(...)</c>,
    /// policies) plus Zonit's <see cref="RequirePermissionAttribute"/> and
    /// <see cref="RequireRoleAttribute"/> backed by VO <c>Permission</c> / <c>Role</c>.
    /// </summary>
    /// <remarks>
    /// <para>What is registered:</para>
    /// <list type="bullet">
    ///   <item>Authentication scheme <see cref="AuthExtensions.SchemeName"/> (<c>"Zonit"</c>)
    ///         backed by <see cref="AuthenticationSchemeService"/> — cookie based.</item>
    ///   <item><see cref="AuthorizationOptions"/> via <c>AddAuthorization()</c> — required for
    ///         <c>[Authorize]</c> to work outside of MVC controllers (Blazor, minimal APIs).</item>
    ///   <item><see cref="PermissionAuthorizationHandler"/> + <see cref="RoleAuthorizationHandler"/>
    ///         — handle the VO-based requirements.</item>
    ///   <item>Cascading authentication state for Blazor.</item>
    ///   <item><see cref="IAuthenticatedProvider"/> / <see cref="IAuthenticatedRepository"/>
    ///         scoped to the request / circuit.</item>
    /// </list>
    /// </remarks>
    public static IServiceCollection AddAuthExtension(this IServiceCollection services)
    {
        // Authentication scheme registration — guarded so consumers can call this multiple
        // times or have already configured authentication elsewhere.
        if (!services.Any(x => x.ServiceType == typeof(IAuthenticationSchemeProvider)))
        {
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = AuthExtensions.SchemeName;
                options.DefaultChallengeScheme = AuthExtensions.SchemeName;
            })
            .AddScheme<AuthenticationSchemeOptions, AuthenticationSchemeService>(
                AuthExtensions.SchemeName, _ => { });
        }

        // Authorization core — previously bypassed (commented out). Now mandatory so that
        // standard [Authorize] / [Authorize<RequirePermission>(...)] works end-to-end.
        services.AddAuthorization();

        // Zonit requirement handlers (VO Permission / Role).
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IAuthorizationHandler, PermissionAuthorizationHandler>());
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IAuthorizationHandler, RoleAuthorizationHandler>());

        services.AddCascadingAuthenticationState();
        services.TryAddScoped<AuthenticationStateProvider, SessionAuthenticationService>();

        services.TryAddScoped<IAuthenticatedRepository, AuthenticatedRepository>();
        services.TryAddScoped<IAuthenticatedProvider, AuthenticatedService>();

        return services;
    }
}