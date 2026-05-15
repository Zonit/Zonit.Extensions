using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Zonit.Extensions.Auth;
using Zonit.Extensions.Auth.Repositories;
using Zonit.Extensions.Website.Authentication;
using Zonit.Extensions.Website.Middlewares;

namespace Zonit.Extensions;

/// <summary>
/// Top-level <c>AddWebsite</c> entry point: aggregates Razor Components hosting,
/// response compression, forwarded headers, antiforgery and registers a set of
/// <see cref="Zonit.Extensions.Website.IWebsiteArea"/>s.
/// </summary>
public static class WebsiteServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Website host with the supplied <paramref name="configure"/> options.
    /// </summary>
    /// <remarks>
    /// <para>What this does:</para>
    /// <list type="bullet">
    ///   <item>Registers <see cref="Zonit.Extensions.Website.WebsiteOptions"/> as a singleton.</item>
    ///   <item>Registers each declared <see cref="Zonit.Extensions.Website.IWebsiteArea"/>
    ///     as a singleton and runs <c>area.ConfigureServices(services)</c>.</item>
    ///   <item>Wires <see cref="Zonit.Extensions.Website.INavigationProvider"/>,
    ///     <see cref="Zonit.Extensions.Website.IBreadcrumbsProvider"/>,
    ///     <see cref="Zonit.Extensions.Website.IToastProvider"/>,
    ///     <see cref="Zonit.Extensions.Website.ICookieProvider"/>.</item>
    ///   <item>Adds Razor Components host with Server / WebAssembly / Auto interactive modes
    ///     according to <see cref="Zonit.Extensions.Website.WebsiteOptions.Mode"/>, and
    ///     <c>AddAdditionalAssemblies(...)</c> for every area's <c>ComponentsAssembly</c>.</item>
    ///   <item>Configures response compression (gzip + brotli) and forwarded-headers
    ///     (when <c>Proxy=true</c>).</item>
    /// </list>
    ///
    /// <para><b>AOT/Trimming:</b> the host is otherwise AOT-safe; areas that bring their
    /// own services may add reflection/dynamic dependencies — annotate them as needed.</para>
    /// </remarks>
    [RequiresUnreferencedCode("Razor Components and Antiforgery use reflection. Components from area assemblies are discovered dynamically.")]
    [RequiresDynamicCode("Razor Components and Antiforgery may emit dynamic code at runtime.")]
    public static IServiceCollection AddWebsite(
        this IServiceCollection services,
        Action<Zonit.Extensions.Website.WebsiteOptions>? configure = null)
    {
        var opts = new Zonit.Extensions.Website.WebsiteOptions();
        configure?.Invoke(opts);

        services.TryAddSingleton(opts);

        // Areas
        foreach (var area in opts.Areas)
        {
            services.AddSingleton(area);
            area.ConfigureServices(services);
        }

        services.AddHttpContextAccessor();

        // Built-in providers
        services.AddNavigationsExtension();
        services.AddBreadcrumbsExtension();
        services.AddToastsExtension();
        services.AddCookiesExtension();

        // Compose the framework-agnostic domain cores. Each AddXxxExtension is idempotent
        // (TryAdd-based) so consumers who already called them directly aren't penalised.
        // We always wire all four because every web host transitively expects them via
        // ExtensionsBase (Culture / Authenticated / Workspace / Catalog accessors).
        services.AddCulturesExtension();
        services.AddAuthExtension();
        services.AddOrganizationsExtension();
        services.AddProjectsExtension();
        services.AddTenantsExtension();

        // ASP.NET Core authentication / authorization wiring that previously lived in
        // Zonit.Extensions.Auth.AddAuthExtension. Lives here so Auth core stays free of
        // Microsoft.AspNetCore.App. Guarded against double-registration so consumers who
        // already brought their own scheme (e.g. external IdP) don't get clobbered.
        if (!services.Any(x => x.ServiceType == typeof(IAuthenticationSchemeProvider)))
        {
            services.AddAuthentication(o =>
            {
                o.DefaultAuthenticateScheme = AuthExtensions.SchemeName;
                o.DefaultChallengeScheme = AuthExtensions.SchemeName;
            })
            .AddScheme<AuthenticationSchemeOptions, AuthenticationSchemeService>(
                AuthExtensions.SchemeName, _ => { });
        }

        services.AddAuthorization();

        // Replace the default policy provider with one that synthesizes a policy for any
        // name that parses as a Permission token. Lets <AuthorizeView Policy="orders.read">
        // and <AuthorizeView Policy="orders.*"> work without manual AddPolicy(...) calls.
        // Falls through to DefaultAuthorizationPolicyProvider for everything else.
        services.Replace(ServiceDescriptor.Singleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>());

        // Zonit VO-backed authorization handlers ([RequirePermission], [RequireRole]).
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IAuthorizationHandler, PermissionAuthorizationHandler>());
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IAuthorizationHandler, RoleAuthorizationHandler>());

        // Cascading auth state for Blazor + the SessionAuthenticationStateProvider that
        // projects IAuthenticatedProvider through ClaimsPrincipal.
        services.AddCascadingAuthenticationState();
        services.TryAddScoped<AuthenticationStateProvider, SessionAuthenticationService>();

        if (opts.AntiForgery)
            services.AddAntiforgery();

        // Razor Components
        var razor = services.AddRazorComponents();
        if (opts.Mode is Zonit.Extensions.Website.WebsiteMode.Server or Zonit.Extensions.Website.WebsiteMode.Auto)
            razor.AddInteractiveServerComponents();
        // NOTE: Interactive WebAssembly components require
        // "Microsoft.AspNetCore.Components.WebAssembly.Server" package referenced by the host project.
        // Consumers using WebsiteMode.WebAssembly / Auto must call
        //   builder.Services.AddRazorComponents().AddInteractiveWebAssemblyComponents()
        // themselves, or reference the package and call our overload.
        // We don't reference it here to keep this package usable in pure-Server hosts.

        // Compression
        if (opts.Compression)
        {
            services.AddResponseCompression(o =>
            {
                o.EnableForHttps = true;
                o.Providers.Add<BrotliCompressionProvider>();
                o.Providers.Add<GzipCompressionProvider>();
                o.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
                {
                    "application/javascript",
                    "application/wasm",
                    "image/svg+xml",
                });
            });
            services.Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
            services.Configure<GzipCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
        }

        // Forwarded headers (reverse proxy)
        if (opts.Proxy)
        {
            services.Configure<ForwardedHeadersOptions>(o =>
            {
                o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                o.KnownIPNetworks.Clear();
                o.KnownProxies.Clear();
            });
        }

        return services;
    }

    /// <summary>
    /// Wires the runtime middleware pieces shipped by Zonit.Extensions.Website. Call
    /// <b>before</b> <c>app.UseAntiforgery()</c> / <c>app.MapRazorComponents&lt;App&gt;()</c>.
    /// </summary>
    /// <remarks>
    /// <para>Pipeline order (ASP.NET Core idioms first, Zonit hydrators last):</para>
    /// <list type="number">
    ///   <item><c>UseForwardedHeaders</c> when running behind a reverse proxy.</item>
    ///   <item><c>UseResponseCompression</c> when compression is enabled.</item>
    ///   <item><c>UseAuthentication</c> + <c>UseAuthorization</c> — required for the
    ///         Zonit auth scheme to participate in <c>[Authorize]</c>.</item>
    ///   <item><see cref="SessionMiddleware"/> — hydrates <see cref="IAuthenticatedRepository"/>
    ///         from the <c>Session</c> cookie on the first request of the scope.</item>
    ///   <item><see cref="CultureMiddleware"/> — resolves culture from URL / cookie /
    ///         Accept-Language and rewrites the path.</item>
    ///   <item><see cref="WorkspaceMiddleware"/> + <see cref="ProjectMiddleware"/> — lazily
    ///         initialise the per-scope workspace / catalog state if the consumer didn't
    ///         already touch them.</item>
    /// </list>
    ///
    /// <para>SessionMiddleware runs <em>after</em> UseAuthentication so the cookie path
    /// and the Zonit hydration share the same authenticated principal — duplication is
    /// avoided by guarding against <c>repository.Current.HasValue</c>.</para>
    /// </remarks>
    public static IApplicationBuilder UseWebsite(this IApplicationBuilder app)
    {
        var opts = app.ApplicationServices.GetService<Zonit.Extensions.Website.WebsiteOptions>();
        if (opts is null) return app;

        if (opts.Proxy) app.UseForwardedHeaders();
        if (opts.Compression) app.UseResponseCompression();

        app.UseAuthentication();
        app.UseAuthorization();

        app.UseMiddleware<SessionMiddleware>();
        app.UseMiddleware<CultureMiddleware>();
        app.UseMiddleware<WorkspaceMiddleware>();
        app.UseMiddleware<ProjectMiddleware>();
        // TenantMiddleware runs last; tenant resolution is independent of auth and
        // the per-user workspace, but downstream pages might want all of them already
        // populated by the time @inject ITenantProvider is consulted.
        app.UseMiddleware<TenantMiddleware>();

        return app;
    }
}
