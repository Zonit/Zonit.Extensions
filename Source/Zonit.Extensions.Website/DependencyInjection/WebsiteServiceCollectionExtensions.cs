using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using Zonit.Extensions.Website.Sitemaps;
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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Zonit.Extensions.Auth;
using Zonit.Extensions.Cultures.Options;
using Zonit.Extensions.Website;
using Zonit.Extensions.Website.Cultures;
using Zonit.Extensions.Website.Authentication;
using Zonit.Extensions.Website.Cookies.Middlewares;
using Zonit.Extensions.Website.Hydration;
using Zonit.Extensions.Website.Middlewares;

namespace Zonit.Extensions;

/// <summary>
/// Top-level entry points for the Website host: <c>AddWebsite</c> (services-time) and
/// <c>UseWebsite&lt;TApp&gt;</c> / <c>UseWebsite&lt;TApp, TSiteOptions&gt;</c>
/// (middleware-time, multi-Site).
/// </summary>
public static class WebsiteServiceCollectionExtensions
{
    /// <summary>
    /// Per-app marker placed onto <see cref="IApplicationBuilder.Properties"/> the first
    /// time the root mount (<see cref="SiteOptions.Directory"/> == <see cref="UrlPath.Empty"/>)
    /// is registered. Any subsequent non-root <c>UseWebsite</c> call after this flag is set
    /// fails fast with a developer-actionable exception — because <c>app.UseEndpoints</c>
    /// inside <c>BuildBranch(app, ...)</c> is a <em>terminal</em> middleware, every
    /// <c>app.MapWhen(...)</c> branch registered AFTER it is unreachable and silently
    /// swallowed by the root site's endpoint matcher. The user-visible symptom is
    /// "HTTP 405 / 404 on /sub/_blazor/negotiate" because the root site's catch-all
    /// razor endpoint claims the path before the sub-mount branch ever runs.
    /// </summary>
    private const string RootMountRegisteredFlag = "Zonit.Extensions.Website.RootMountRegistered";

    /// <summary>
    /// Normalises the user-supplied mount path so <c>"/"</c> and the empty
    /// <see cref="UrlPath"/> both map to the root site
    /// (<see cref="UrlPath.Empty"/>). Used by every <c>UseWebsite</c> overload so
    /// <see cref="SiteOptions.Directory"/> stays in sync with the actual branch
    /// path-base regardless of the entry point.
    /// </summary>
    private static UrlPath NormalizeDirectory(UrlPath directory)
        => directory.Value == "/" ? UrlPath.Empty : directory;

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

        // ─── Prerender → interactive state bridges ─────────────────────────────
        // Each scoped repository the middleware pipeline populates on the HTTP
        // request scope (Identity / Workspace / Catalog / Culture / Cookies /
        // Tenant) would otherwise re-read empty in the SignalR circuit scope,
        // because the middleware never runs against that scope. The single
        // <WebsiteHydrator/> component (placed in App.razor / DashboardApp.razor)
        // resolves IEnumerable<IPersistentStateProvider> and uses
        // PersistentComponentState to round-trip every bridge's snapshot.
        // TryAddEnumerable so consumers can stack their own bridges via the same
        // contract without displacing ours; scoped lifetime so each bridge can
        // reference the same scoped repository the rest of the framework writes.
        // Registration order is the Restore order; Tenant is last, mirroring
        // TenantMiddleware's position at the end of the request pipeline.
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPersistentStateProvider, AuthStateBridge>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPersistentStateProvider, CultureStateBridge>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPersistentStateProvider, WorkspaceStateBridge>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPersistentStateProvider, CatalogStateBridge>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPersistentStateProvider, CookieStateBridge>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPersistentStateProvider, TenantStateBridge>());

        // Singleton snapshot of every registered mount. The scoped ICurrentSite
        // below falls back to this map when the per-Site branch middleware has not
        // run on the current scope (most notably: SignalR circuit scope owning
        // interactive Blazor components). See WebsiteMountRegistry remarks for the
        // full rationale and the mirror in DashboardMountRegistry.
        services.TryAddSingleton<WebsiteMountRegistry>();

        // Per-request marker for the active Site mount. Set by the branch middleware
        // installed inside UseWebsite<TApp>(o => …) — read by INavigationProvider /
        // any consumer that needs to scope output to the current mount-point. The
        // implementation self-hydrates from WebsiteMountRegistry when the scope's
        // middleware never ran (Blazor circuit scope), so consumers see a populated
        // Site in both SSR and interactive render passes without per-host bridges.
        services.TryAddScoped<ICurrentSite, CurrentSite>();

        // Channel carrying the routed page's PageMeta up to the document head. Scoped, because
        // the head and the page must be looking at the same instance within one request or one
        // circuit, and must NOT be within another.
        services.TryAddScoped<IPageMetaState, PageMetaState>();

        // Built-in providers
        services.AddNavigationsExtension();
        services.AddBreadcrumbsExtension();
        services.AddToastsExtension();
        services.AddCookiesExtension();
        services.AddLayoutsExtension();

        // robots.txt / sitemap.xml / llms.txt. Registered unconditionally, because after the
        // merge there is no package to forget: a Site opts in per mount with o.Indexing(...), and
        // a host that never calls it pays a generator and a cache object that are never resolved.
        // Call AddSitemap(...) explicitly only to change the generation limits or cache duration.
        services.AddSitemap();

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
    /// Mounts a Site at <paramref name="directory"/> with the supplied set of Areas.
    /// Each call to <c>UseWebsite&lt;TApp&gt;</c> creates an isolated <c>MapWhen</c>
    /// branch with its own <c>UsePathBase</c>, full middleware pipeline, and a
    /// dedicated <c>MapRazorComponents&lt;TApp&gt;</c>. May be called any number of
    /// times — each Site can mount any subset of registered Areas, including the
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

        // Manually run the same OnConfiguring → configure → OnConfigured lifecycle as
        // the generic overload below. Cannot delegate to `UseWebsite<TApp, SiteOptions>`
        // because the `new()` constraint requires a public parameterless ctor, and
        // SiteOptions intentionally hides its parameterless ctor behind `protected` —
        // consumers must always reach a configured instance through the framework's
        // entry points (or via a derived class with its own public ctor).
        var registry = app.Services.GetRequiredService<WebsiteAreaRegistry>();
        var site = new SiteOptions(registry);
        site.Directory = NormalizeDirectory(directory);

        site.InvokeOnConfiguring(app.Services);
        configure(site);
        site.InvokeOnConfigured(app.Services);

        return app.UseWebsite<TApp>(directory, site);
    }

    /// <summary>
    /// Mounts a Site using the framework's own document shell — no root component, no
    /// <c>App.razor</c>, no subclass.
    /// </summary>
    /// <remarks>
    /// <para>The whole document comes from <c>SiteOptions.Document</c> and the Site's settings,
    /// which covers a Site that has nothing structurally unusual to say about its HTML. Reach for
    /// <c>UseWebsite&lt;TApp&gt;</c> when a virtual on <see cref="AppBase"/> needs overriding, or
    /// when the document itself must be hand-written.</para>
    ///
    /// <code>
    /// app.UseWebsite("/", o =>
    /// {
    ///     o.Mode = WebsiteMode.Static;
    ///     o.Cultures.Strategy = CultureUrlStrategy.Prefix;
    ///     o.Document.AddStylesheet("app.css").SetFavicon("favicon.svg", "image/svg+xml");
    /// });
    /// </code>
    /// </remarks>
    public static WebApplication UseWebsite(
        this WebApplication app,
        UrlPath directory,
        Action<SiteOptions> configure)
        => app.UseWebsite<WebsiteApp>(directory, configure);

    /// <summary>
    /// Inheritance-friendly entry point — mounts a Site at <paramref name="directory"/>
    /// using <typeparamref name="TSiteOptions"/> as the per-Site configuration type.
    /// Specialised hosts (e.g. <c>Zonit.Dashboard</c>) ship a <see cref="SiteOptions"/>
    /// subclass with extra knobs + <see cref="SiteOptions.OnConfiguring"/> /
    /// <see cref="SiteOptions.OnConfigured"/> overrides; consumers then mount with a
    /// one-liner like <c>app.UseDashboard("/admin", o =&gt; …)</c> that forwards to this
    /// overload.
    /// </summary>
    /// <typeparam name="TApp">Root Razor component (typically <c>App.razor</c>).</typeparam>
    /// <typeparam name="TSiteOptions">
    /// <see cref="SiteOptions"/> subclass exposing the per-Site configuration surface.
    /// Must have a public parameterless constructor.
    /// </typeparam>
    /// <remarks>
    /// <para><b>Lifecycle</b> (in order):</para>
    /// <list type="number">
    ///   <item><c>new TSiteOptions()</c> — derived ctor sets defaults.</item>
    ///   <item><c>AttachRegistry(registry)</c> — so <c>AddArea&lt;TArea&gt;()</c> works inside hooks.</item>
    ///   <item><c>Directory = </c><paramref name="directory"/> (normalised) — so hooks can read it.</item>
    ///   <item><c>OnConfiguring(services)</c> — derived seeds implicit areas / always-on hooks.</item>
    ///   <item><paramref name="configure"/>(site) — consumer customisation.</item>
    ///   <item><c>OnConfigured(services)</c> — derived snapshots final state into singletons / late hooks.</item>
    ///   <item>Mount via the low-level <c>UseWebsite&lt;TApp&gt;(directory, site)</c>.</item>
    /// </list>
    /// </remarks>
    public static WebApplication UseWebsite<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TApp,
        TSiteOptions>(
        this WebApplication app,
        UrlPath directory,
        Action<TSiteOptions>? configure = null)
        where TApp : IComponent
        where TSiteOptions : SiteOptions, new()
    {
        ArgumentNullException.ThrowIfNull(app);

        var registry = app.Services.GetRequiredService<WebsiteAreaRegistry>();
        var site = new TSiteOptions();
        site.AttachRegistry(registry);

        // Directory is assigned BEFORE OnConfiguring so derived overrides may read it
        // (e.g. DashboardSiteOptions.OnConfigured uses Directory to key the mount
        // registry). The low-level UseWebsite<TApp>(directory, site) below assigns
        // it again with the same value — idempotent.
        site.Directory = NormalizeDirectory(directory);

        site.InvokeOnConfiguring(app.Services);
        configure?.Invoke(site);
        site.InvokeOnConfigured(app.Services);

        return app.UseWebsite<TApp>(directory, site);
    }

    /// <summary>
    /// Low-level "pre-built" overload — mounts an already-configured
    /// <see cref="SiteOptions"/> at <paramref name="directory"/>. Use the higher-level
    /// <see cref="UseWebsite{TApp, TSiteOptions}"/> overload unless you need to bypass
    /// the <see cref="SiteOptions.OnConfiguring"/> / <see cref="SiteOptions.OnConfigured"/>
    /// template-method lifecycle (e.g. when wiring a fully-instanced
    /// <see cref="SiteOptions"/> from a non-standard composition root).
    /// </summary>
    public static WebApplication UseWebsite<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TApp>(
        this WebApplication app,
        UrlPath directory,
        SiteOptions site)
        where TApp : IComponent
    {
        ArgumentNullException.ThrowIfNull(site);

        // "/" and "" both mean root — normalise to UrlPath.Empty so Directory.HasValue
        // unambiguously indicates a non-root mount.
        site.Directory = NormalizeDirectory(directory);

        var opts = app.Services.GetRequiredService<WebsiteOptions>();

        var pathBase = site.NormalizedPathBase;

        // Layer the "Website" configuration section over the code defaults, and keep watching it.
        // Attached before the registry snapshot because everything downstream — the mount
        // registry, the URL policy, the middleware — reads settings through this resolver rather
        // than through the SiteOptions instance, so that an appsettings edit reaches a running
        // process instead of waiting for a deployment.
        site.Settings = new SiteSettingsProvider(
            app.Services.GetService<IConfiguration>(),
            site.Defaults,
            pathBase);

        // Let every mounted area append its own assets to this Site's document. After the Site's
        // declarations (so an area sheet cascades over the base one) and before the mount snapshot
        // (which is what ICurrentSite reads back outside the branch, so it has to see the final
        // document). Per Site, not per area instance: the same area mounted twice contributes to
        // each Site's own DocumentOptions exactly once.
        foreach (var area in site.Areas)
            area.ConfigureDocument(site.Document);

        // Snapshot this mount into the singleton registry. The scoped ICurrentSite
        // reads back from it whenever its per-Site branch middleware has not run
        // (Blazor circuit scope, hosted services). Mirror of how UseDashboard
        // populates DashboardMountRegistry — same survival contract, applies to
        // every UseWebsite mount including the root.
        var mounts = app.Services.GetRequiredService<WebsiteMountRegistry>();
        mounts.Register(site);

        var matchPath = string.IsNullOrEmpty(pathBase) ? null : pathBase;

        // Order guard. `BuildBranch(app, ...)` for the root mount finishes with
        // `app.UseEndpoints(...)`, which is a TERMINAL middleware — every middleware
        // (and every `app.MapWhen(...)` branch) registered AFTER it is unreachable.
        // Declaring a non-root mount after the root mount therefore SILENTLY breaks
        // sub-site routing: sub-mount requests fall through to the root site's
        // catch-all razor endpoint, which then returns 405 for hub negotiate POSTs
        // and 404 for sub-site pages. The failure mode is opaque, so we fail fast
        // here with a developer-actionable message.
        IApplicationBuilder ab = app;
        if (matchPath is null)
        {
            // Registering the root mount — flag it so future calls can detect the order.
            ab.Properties[RootMountRegisteredFlag] = true;
        }
        else if (ab.Properties.ContainsKey(RootMountRegisteredFlag))
        {
            throw new InvalidOperationException(
                $"app.UseWebsite<{typeof(TApp).Name}>(\"{directory.Value}\", ...) cannot be called " +
                "after the root mount (Directory == \"/\") has been registered. The root mount " +
                "finishes with a terminal UseEndpoints, so any later MapWhen branch is unreachable " +
                "and the sub-mount silently fails (typical symptom: HTTP 405 on /<sub>/_blazor/negotiate). " +
                "Declare every non-root mount BEFORE the root mount, e.g.:\n" +
                "    app.UseDashboard(\"/dashboard\", ...);   // sub-mounts first\n" +
                "    app.UseWebsite<App>(\"/\", ...);          // root mount last");
        }

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

        // Exception handling — must come BEFORE every middleware that could throw.
        //
        // These are two different concerns and they get different treatment:
        //
        //   Unhandled exceptions (500) are environment-dependent. In Development the
        //   DeveloperExceptionPage with its stack trace is worth far more than a styled page;
        //   in Production it would leak internals, so the consumer's error page takes over.
        //
        //   Status codes (404, 401, 403) are NOT exceptions and are environment-independent.
        //   Re-executing them only in Production used to mean a developer never saw their own
        //   404 page while building the site — a request for a missing route returned a bare,
        //   empty 404 in dev and a fully styled page in prod, so the first time anyone met the
        //   error pages was in production. Status-code re-execution now runs in both.
        //
        // BOTH re-executions ask for a FRESH DI SCOPE, and that is not a tuning knob.
        // Re-execution replays the request through the same HttpContext; without a new scope it
        // also reuses the same scoped services, and `NavigationManager` is one of them. It
        // refuses a second Initialize:
        //
        //   InvalidOperationException: '…NavigationManager' already initialized.
        //     at EndpointHtmlRenderer.InitializeStandardComponentServicesAsync
        //
        // The renderer only initializes it when a page actually renders, so the damage is
        // invisible in the easy case and total in the interesting one. Measured on a Blazor Web
        // App with a page calling NavigationManager.NotFound() and a page throwing:
        //
        //   route unknown to the router  →  404 + error page      (renderer never ran once)
        //   NotFound() from a page       →  500, shared scope     →  404 + error page, new scope
        //   exception from a page (prod) →  500 and NO page at all →  500 + error page, new scope
        //
        // The names differ per host (RemoteNavigationManager wins the registration as soon as
        // AddInteractiveServerComponents is present, even on a Static mount), but the failure does
        // not: HttpNavigationManager throws on the second Initialize in exactly the same way.
        // Nothing about this is fixable from the consumer side — the two-argument
        // UseStatusCodePagesWithReExecute overload builds its own options and ignores
        // Configure<StatusCodePagesOptions>, so the flag has to be passed here.
        if (isDev)
        {
            branch.UseDeveloperExceptionPage();
        }
        else if (!string.IsNullOrEmpty(site.ExceptionHandlerPath))
        {
            branch.UseExceptionHandler(new ExceptionHandlerOptions
            {
                ExceptionHandlingPath = site.ExceptionHandlerPath,
                CreateScopeForErrors = true,
            });
        }

        if (!string.IsNullOrEmpty(site.ExceptionHandlerPath))
        {
            branch.UseStatusCodePagesWithReExecute(
                site.ExceptionHandlerPath + "/{0}", null, createScopeForStatusCodePages: true);
        }

        // Stamp the active Site onto the request scope BEFORE anything reads it.
        // INavigationProvider, breadcrumbs and consumer code rely on ICurrentSite to
        // filter their output to the current mount-point.
        //
        // This MUST stay BELOW the two registrations above, and the reason is the fresh scope they
        // now create. Re-execution restarts the pipeline from the error middleware inwards, so a
        // stamp placed above it never runs again, and in a brand-new scope this resolves a
        // brand-new CurrentSite on which Set() was never called.
        //
        // That does not crash: CurrentSite keeps _explicit false and every accessor falls through
        // to the WebsiteMountRegistry snapshot, which is resolved best-effort from the request.
        // It is still the wrong source. The snapshot is a per-mount copy, so the error page would
        // read Appearance, Document and LocalizedRoutes from it instead of from the live
        // SiteOptions this branch was actually built with — a quiet divergence on exactly the page
        // nobody looks at twice. Keeping the stamp below means re-execution re-stamps the new
        // scope and the error page sees the same Site as every other page on the mount.
        //
        // Nothing above this point reads ICurrentSite: only UsePathBase and framework error
        // middleware sit there, so moving it down costs nothing.
        branch.Use(async (ctx, next) =>
        {
            var current = ctx.RequestServices.GetRequiredService<ICurrentSite>();
            current.Set(site);
            await next();
        });

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

        // Culture detection runs BEFORE UseRouting on purpose: the URL prefix
        // (/pl/home → PathBase "/pl" + Path "/home") must be split before
        // EndpointRoutingMiddleware selects an endpoint, otherwise routes never match the
        // prefixed form. CultureMiddleware does not read RouteValues / HttpContext.GetEndpoint(),
        // so it is safe to place here — see audit §1.4 for the rationale.
        //
        // The URL policy and the localized-route table are per-Site, so they are built here and
        // handed to the middleware rather than resolved from the container: two mounts under one
        // host legitimately disagree about whether the language belongs in the path at all. Both
        // follow configuration reloads, so adding a language or retuning the policy stays a
        // restart-free change on every mount.
        var urlPolicy = new CultureUrlPolicy(
            branch.ApplicationServices.GetRequiredService<IOptionsMonitor<CultureOption>>(),
            site.Cultures,
            site.Settings!);

        // Localized routes are contributed by the areas that own them, not by the Site — a host
        // should not have to restate the translations of every plug-in it mounts.
        var localizedRoutes = site.Areas.SelectMany(a => a.Routes).ToArray();
        var routeTable = localizedRoutes.Length == 0
            ? LocalizedRouteTable.Empty
            : new LocalizedRouteTable(localizedRoutes);

        site.UrlPolicy = urlPolicy;
        site.LocalizedRoutes = routeTable;

        // Tenant BEFORE culture, and both before routing. The tenant owns the site's default
        // language — in a multi-site host each brand legitimately has its own — so the culture
        // resolution has to be able to ask. It is safe this early: tenant resolution keys off
        // Request.Host alone, touching neither routing nor the authenticated principal, which is
        // exactly why it used to sit at the end without depending on anything there.
        branch.UseMiddleware<TenantMiddleware>();
        branch.UseMiddleware<CultureMiddleware>(urlPolicy, routeTable, site);

        branch.UseRouting();

        // The culture prefix is valid for PAGES and for nothing else, and "is this a page" is the
        // router's knowledge — so the gate sits immediately after UseRouting, where the matched
        // endpoint is known, and before authentication, so a 404 costs no auth work. Prefixed
        // Sites only: on every other mount the gate would be a per-request feature read that can
        // never fire.
        if (urlPolicy.IsPrefixed)
            branch.UseMiddleware<CultureRouteGate>();

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
        //
        // Tenant is NOT here any more — it moved ahead of routing, above, because culture
        // resolution needs the tenant's default language. Its own initialisation is idempotent
        // per host, so nothing downstream notices the move.
        branch.UseMiddleware<CookieMiddleware>();
        branch.UseMiddleware<SessionMiddleware>();
        branch.UseMiddleware<WorkspaceMiddleware>();
        branch.UseMiddleware<ProjectMiddleware>();

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

        // The entry assembly joins the scan explicitly. MapRazorComponents<TApp>() roots page
        // discovery at TApp's assembly, which for the built-in shell is this framework — a Site
        // mounted with the non-generic UseWebsite and no areas would then have no page endpoints
        // at all and answer 404 for everything. It reads as a routing bug and is nothing of the
        // sort. Harmless when TApp already lives in the entry assembly: the Distinct and the
        // hostAssembly filter below remove it.
        var candidates = site.Areas
            .Select(a => a.ComponentsAssembly)
            .Append(System.Reflection.Assembly.GetEntryAssembly());

        var assemblies = candidates
            .Where(a => a is not null && a != hostAssembly)
            .Select(a => a!)
            .Distinct()
            .ToArray();

        branch.UseEndpoints(ep =>
        {
            // Static assets (blazor.web.js, _framework/, _content/, *.css, *.js) MUST
            // be mapped INSIDE the branch — never globally on the parent app. Reason:
            //
            // For a non-root mount such as "/dashboard" the request "/dashboard/_content/
            // MudBlazor/MudBlazor.min.css" is matched by app.MapWhen(StartsWith("/dashboard"))
            // and routed into this branch, where UsePathBase("/dashboard") strips the
            // prefix and Request.Path becomes "/_content/MudBlazor/MudBlazor.min.css".
            // The branch has its OWN endpoint route builder (independent of the parent
            // app's), so the only way the StaticAssetsEndpointDataSource can resolve
            // the stripped path is if MapStaticAssets() is registered HERE — registering
            // it on the parent app means the branch's UseEndpoints sees zero static-
            // asset endpoints and every "/<mount>/_content/..." request 404s. See
            // https://github.com/dotnet/aspnetcore/issues/62249 for the same scenario.
            //
            // For the root mount (Directory == "") `branch == app`, so MapStaticAssets
            // ends up on the parent endpoint route builder anyway — same behaviour as
            // the previous global registration, just driven through the per-Site path.
            // The fingerprinted @Assets[...] resolutions in App.razor / DashboardApp.razor
            // are then resolved against the same endpoint set, so URLs like
            // "/dashboard/_content/MudBlazor/MudBlazor.min.css#[<fingerprint>]" route
            // back to this branch and hit the static-assets endpoint after path-base
            // stripping.
            ep.MapStaticAssets();

            // Short social links and Apple's ownership files, both on by default. Neither needs a
            // decision from the host: the tenant is already resolved on every request, an entry it
            // left blank answers 404, and a page sharing a platform's name wins the route because
            // the redirects are ordered last. Opting in would be asking the host to repeat a
            // decision it already made by filling the setting in.
            if (site.Social.Enabled)
                Website.Social.SocialLinkEndpoints.Map(ep, site.Social);

            if (site.Verification.Enabled)
                Website.Verification.VerificationEndpoints.Map(ep);

            // robots.txt / llms.txt / sitemap.xml are mapped by the Site's own hooks below, via
            // SiteOptions.Indexing(...) in Zonit.Extensions.Website.Sitemaps. They sit outside the
            // kernel because the three files are one statement in three formats and only work if
            // they agree — robots.txt has to name the sitemap's real address, and neither may
            // contradict the culture policy about which languages are indexed. Splitting them
            // across a package boundary made that agreement something a host retyped by hand.
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
