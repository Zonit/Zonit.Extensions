using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.Extensions.WebEncoders;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Zonit.Extensions.Auth;
using Zonit.Extensions.Website;
using Zonit.Extensions.Website.Authentication;
using Zonit.Extensions.Website.Cookies.Middlewares;
using Zonit.Extensions.Website.Middlewares;

namespace Zonit.Extensions;

/// <summary>
/// Top-level entry points for the Website host: <c>AddWebsite</c> (services-time) and
/// <c>UseWebsite&lt;TApp&gt;</c> (middleware-time, multi-Site).
/// </summary>
public static class WebsiteServiceCollectionExtensions
{
    private const string GlobalPipelineFlag = "Zonit.Extensions.Website.GlobalPipelineWired";

    /// <summary>
    /// Registers the Website framework (Razor Components hosting, auth scheme,
    /// compression, antiforgery, navigation/breadcrumbs/toast/cookie providers,
    /// permission policies, every domain core's <c>Add*Extension()</c>) and runs
    /// each declared area's <see cref="IWebsiteServices.ConfigureServices"/> hook.
    /// </summary>
    /// <remarks>
    /// <para>Areas are <b>not</b> mounted by this call — mounting is per-Site at
    /// middleware time:</para>
    /// <code>
    /// app.UseWebsite&lt;App&gt;(o =>
    /// {
    ///     o.Directory = "";          // root site
    ///     o.AddArea&lt;HomeArea&gt;();
    ///     o.AddArea&lt;AuthArea&gt;();
    /// });
    ///
    /// app.UseWebsite&lt;App&gt;(o =>
    /// {
    ///     o.Directory = "/admin";    // second mount sharing AuthArea
    ///     o.Permission = "admin";
    ///     o.AddArea&lt;AuthArea&gt;();
    ///     o.AddArea&lt;AdminArea&gt;();
    /// });
    /// </code>
    /// </remarks>
    [RequiresUnreferencedCode("Razor Components and Antiforgery use reflection. Components from area assemblies are discovered dynamically.")]
    [RequiresDynamicCode("Razor Components and Antiforgery may emit dynamic code at runtime.")]
    public static IServiceCollection AddWebsite(
        this IServiceCollection services,
        Action<WebsiteOptions>? configure = null)
    {
        var registry = new WebsiteAreaRegistry();
        services.TryAddSingleton(registry);

        var opts = new WebsiteOptions(services, registry);
        configure?.Invoke(opts);

        services.TryAddSingleton(opts);
        services.AddHttpContextAccessor();

        // Per-request marker for the active Site mount. Set by the branch middleware
        // installed inside UseWebsite<TApp>(o => …) — read by INavigationProvider /
        // any consumer that needs to scope output to the current mount-point.
        services.TryAddScoped<ICurrentSite, CurrentSite>();

        // Built-in providers
        services.AddNavigationsExtension();
        services.AddBreadcrumbsExtension();
        services.AddToastsExtension();
        services.AddCookiesExtension();

        // Domain cores (idempotent — TryAdd-based + Null*Source safety net inside).
        services.AddCulturesExtension();
        services.AddAuthExtension();
        services.AddOrganizationsExtension();
        services.AddProjectsExtension();
        services.AddTenantsExtension();

        // ASP.NET auth scheme (only if the consumer hasn't already brought their own).
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

        // Permission-aware policy provider (Permission VO → dynamic policy).
        services.Replace(ServiceDescriptor.Singleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAuthorizationHandler, PermissionAuthorizationHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAuthorizationHandler, RoleAuthorizationHandler>());

        // Cascading auth state for Blazor.
        services.AddCascadingAuthenticationState();
        services.TryAddScoped<AuthenticationStateProvider, SessionAuthenticationService>();

        // ─── Middleware-paired services. Always wired (cheap, idempotent). Each Site
        // picks per-branch whether to actually USE the matching middleware via SiteOptions.

        services.AddAntiforgery();
        services.AddProblemDetails(); // ASP.NET 7+ standardised error responses (RFC 7807).

        // Allow the full Unicode range through the default HTML encoder. ASP.NET's default
        // escapes non-ASCII into &#xNNNN; entities — catastrophic for non-English content
        // (Polish, German, Cyrillic, CJK, emoji). This single Configure fixes every Razor
        // / minimal-API string output for the rest of the host's lifetime.
        services.Configure<WebEncoderOptions>(o =>
        {
            o.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All);
        });

        services.AddHsts(o =>
        {
            // Conservative production defaults — consumers can re-Configure<HstsOptions> to override.
            o.Preload = false;
            o.IncludeSubDomains = false;
            o.MaxAge = TimeSpan.FromDays(30);
        });

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

        services.Configure<ForwardedHeadersOptions>(o =>
        {
            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            o.KnownIPNetworks.Clear();
            o.KnownProxies.Clear();
        });

        // ─── Pure-service toggles (no middleware counterpart). ───

        if (opts.MemoryCache)
            services.AddMemoryCache();

        if (opts.Controllers)
            services.AddControllers();

        if (opts.RazorPages)
            services.AddRazorPages();

        // Razor Components host (top-level — branches re-use the same service registrations).
        // WebAssembly bits require an extra package and are wired by the consumer manually.
        if (opts.RazorComponents)
        {
            services.AddRazorComponents().AddInteractiveServerComponents();
        }

        return services;
    }

    /// <summary>
    /// Mounts a Site at <see cref="SiteOptions.Directory"/> with the supplied set of
    /// Areas. Each call to <c>UseWebsite&lt;TApp&gt;</c> creates an isolated
    /// <c>MapWhen</c> branch with its own <c>UsePathBase</c>, full middleware pipeline,
    /// and a dedicated <c>MapRazorComponents&lt;TApp&gt;</c>. May be called any number
    /// of times — each Site can mount any subset of registered Areas, including the
    /// same Area at multiple paths (e.g. Auth at <c>/</c> and at <c>/admin</c>).
    /// </summary>
    /// <typeparam name="TApp">
    /// Root Razor component (typically <c>App.razor</c>). Different Sites may use
    /// different root components — useful when a sub-site needs a distinct layout
    /// or <c>&lt;base href&gt;</c>.
    /// </typeparam>
    public static WebApplication UseWebsite<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TApp>(
        this WebApplication app,
        UrlPath directory,
        Action<SiteOptions> configure)
        where TApp : IComponent
    {
        ArgumentNullException.ThrowIfNull(configure);

        var opts = app.Services.GetRequiredService<WebsiteOptions>();
        var registry = app.Services.GetRequiredService<WebsiteAreaRegistry>();

        var site = new SiteOptions(registry, directory);
        configure(site);

        EnsureGlobalPipeline(app);

        var pathBase = site.NormalizedPathBase;
        var matchPath = string.IsNullOrEmpty(pathBase) ? null : pathBase;

        // Build the per-Site branch. When Directory == "" the branch is the catch-all
        // (executed only when no other Site matched first — order of UseWebsite calls
        // is therefore meaningful: declare the root Site last to avoid swallowing
        // sub-mounts).
        if (matchPath is null)
        {
            BuildBranch<TApp>(app, site, opts);
        }
        else
        {
            app.MapWhen(
                ctx => ctx.Request.Path.StartsWithSegments(matchPath, StringComparison.OrdinalIgnoreCase),
                branch => BuildBranch<TApp>(branch, site, opts));
        }

        return app;
    }

    private static void EnsureGlobalPipeline(WebApplication app)
    {
        // Idempotent — first UseWebsite<> call wires the only truly global piece:
        // MapStaticAssets (endpoint data source, must register pre-branch). Every other
        // middleware (compression / HSTS / proxy / antiforgery / exception handler /
        // HTTPS redirection) is per-Site and lives in BuildBranch.
        IApplicationBuilder ab = app;
        if (ab.Properties.ContainsKey(GlobalPipelineFlag)) return;
        ab.Properties[GlobalPipelineFlag] = true;

        // Static assets (blazor.web.js, _content/, *.css, *.js) — host-wide.
        app.MapStaticAssets();
    }

    private static void BuildBranch<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TApp>(
        IApplicationBuilder branch, SiteOptions site, WebsiteOptions opts)
        where TApp : IComponent
    {
        var env = branch.ApplicationServices.GetRequiredService<IWebHostEnvironment>();
        var isDev = env.IsDevelopment();

        var pathBase = site.NormalizedPathBase;
        if (!string.IsNullOrEmpty(pathBase))
            branch.UsePathBase(pathBase);

        // Stamp the active Site onto the request scope BEFORE anything reads it.
        // INavigationProvider, breadcrumbs and consumer code rely on ICurrentSite to
        // filter their output to the current mount-point.
        branch.Use(async (ctx, next) =>
        {
            var current = ctx.RequestServices.GetRequiredService<ICurrentSite>();
            current.Set(site);
            await next();
        });

        // Exception handling — must come BEFORE every middleware that could throw.
        // Dev: DeveloperExceptionPage (always, regardless of site.ExceptionHandlerPath).
        // Prod: site.ExceptionHandlerPath (null disables).
        if (isDev)
        {
            branch.UseDeveloperExceptionPage();
        }
        else if (!string.IsNullOrEmpty(site.ExceptionHandlerPath))
        {
            branch.UseExceptionHandler(site.ExceptionHandlerPath);
            branch.UseStatusCodePagesWithReExecute(site.ExceptionHandlerPath + "/{0}");
        }

        // Production-only edge middleware (matches ASP.NET template order).
        if (!isDev)
        {
            if (site.Proxy) branch.UseForwardedHeaders();
            if (site.Hsts) branch.UseHsts();
        }

        if (site.HttpsRedirection) branch.UseHttpsRedirection();
        if (site.Compression) branch.UseResponseCompression();

        // Security response headers — cheap, idempotent, recommended by OWASP.
        if (site.SecurityHeaders)
        {
            branch.Use(async (ctx, next) =>
            {
                var h = ctx.Response.Headers;
                h["X-Content-Type-Options"] = "nosniff";
                h["X-Frame-Options"] = "SAMEORIGIN";
                h["X-XSS-Protection"] = "1; mode=block";
                h["Referrer-Policy"] = "strict-origin-when-cross-origin";
                h["Server"] = "web"; // mask Kestrel/IIS fingerprint
                await next();
            });
        }

        // Early-pipeline hooks — ImageSharp, custom static-files, request rewriters.
        // These run BEFORE routing so they can short-circuit / wrap every request.
        foreach (var area in site.Areas) area.App(branch);
        foreach (var hook in site.AppHooks) hook(branch);

        // Culture detection runs BEFORE UseRouting on purpose: the URL prefix path
        // (/pl/home → /home) must be rewritten before EndpointRoutingMiddleware
        // selects an endpoint, otherwise routes never match the prefixed form.
        // CultureMiddleware does not read RouteValues / HttpContext.GetEndpoint(),
        // so it is safe to place here — see audit §1.4 for the rationale.
        branch.UseMiddleware<CultureMiddleware>();

        branch.UseRouting();

        // Auth must come AFTER UseRouting in endpoint-routing model.
        branch.UseAuthentication();
        branch.UseAuthorization();
        if (site.AntiForgery) branch.UseAntiforgery();

        // Zonit hydrators — order matters:
        //   Cookies   → snapshot of Request.Cookies into the scoped repository so
        //               ICookieProvider.Get(...) works without an extra round-trip
        //               (without this, the scoped CookiesRepository stays empty —
        //               audit AUDIT_2026_05 §8.5).
        //   Session   → identity into the request scope
        //   Workspace/Project → consume identity to populate org/project state
        //   Tenant    → independent of auth, last so downstream sees fully populated scope.
        branch.UseMiddleware<CookieMiddleware>();
        branch.UseMiddleware<SessionMiddleware>();
        branch.UseMiddleware<WorkspaceMiddleware>();
        branch.UseMiddleware<ProjectMiddleware>();
        branch.UseMiddleware<TenantMiddleware>();

        // Late-pipeline hooks — per-area then per-Site (signed-URL guards consuming
        // identity, audit logging tied to authenticated principal).
        foreach (var area in site.Areas) area.Use(branch);
        foreach (var hook in site.UseHooks) hook(branch);

        // Endpoints — Razor Components host + per-area / per-Site minimal-API endpoints.
        // Filter out TApp's own assembly: MapRazorComponents<TApp>() already treats it as
        // the default, and AddAdditionalAssemblies rejects duplicates with
        // "Assembly already defined". This naturally handles the case where the host's
        // own area (e.g. HomeArea) lives in the same assembly as App.razor.
        var hostAssembly = typeof(TApp).Assembly;
        var assemblies = site.Areas
            .Select(a => a.ComponentsAssembly)
            .Where(a => a is not null && a != hostAssembly)
            .Distinct()
            .ToArray();

        branch.UseEndpoints(ep =>
        {
            if (opts.RazorComponents)
            {
                var razor = ep.MapRazorComponents<TApp>();

                // Per-Site render mode. Server is always wired (cheap, idempotent at the
                // services layer). WebAssembly bits require the consumer to reference
                // "Microsoft.AspNetCore.Components.WebAssembly.Server" and call
                // AddInteractiveWebAssemblyRenderMode() themselves via SiteOptions.MapEndpoints.
                if (site.Mode is WebsiteMode.Server or WebsiteMode.Auto)
                    razor.AddInteractiveServerRenderMode();

                if (assemblies.Length > 0)
                    razor.AddAdditionalAssemblies(assemblies);

                if (!string.IsNullOrEmpty(site.Permission))
                    razor.RequireAuthorization(site.Permission);
            }

            if (opts.Controllers)
                ep.MapControllers();

            if (opts.RazorPages)
                ep.MapRazorPages();

            foreach (var area in site.Areas) area.MapEndpoints(ep);
            foreach (var hook in site.EndpointHooks) hook(ep);
        });
    }
}
