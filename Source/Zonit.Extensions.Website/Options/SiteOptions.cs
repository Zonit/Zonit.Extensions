using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Zonit.Extensions.Website;

/// <summary>
/// Per-Site configuration consumed by <c>app.UseWebsite&lt;TApp&gt;(o => …)</c>. A Site
/// is a mount-point: a URL prefix (<see cref="Directory"/>) under which a curated set
/// of <see cref="IWebsiteArea"/>s is exposed, with optional permission gating.
/// </summary>
/// <remarks>
/// <para>One host may declare any number of Sites — each call to
/// <c>app.UseWebsite&lt;TApp&gt;(o => …)</c> creates an isolated branch
/// (<c>MapWhen</c> + <c>UsePathBase</c>) with its own pipeline and its own
/// <c>MapRazorComponents&lt;TApp&gt;</c>. Areas may be re-used across Sites — the
/// same <c>AuthArea</c> instance can be mounted at <c>/</c> and <c>/admin</c> to
/// surface login/register pages under both paths.</para>
/// </remarks>
public class SiteOptions
{
    private WebsiteAreaRegistry? _registry;
    private readonly List<IWebsiteArea> _areas = [];
    private readonly List<Action<IApplicationBuilder>> _appHooks = [];
    private readonly List<Action<IApplicationBuilder>> _useHooks = [];
    private readonly List<Action<IEndpointRouteBuilder>> _endpointHooks = [];

    internal SiteOptions(WebsiteAreaRegistry registry)
    {
        _registry = registry;
    }

    /// <summary>
    /// Derivation ctor. Subclasses (e.g. <c>DashboardSiteOptions</c>) call this base
    /// without arguments — <see cref="Directory"/> is assigned by the framework when
    /// <c>UseWebsite&lt;TApp, TSiteOptions&gt;(directory, ...)</c> mounts the instance,
    /// so callers cannot accidentally fork the mount path away from the actual branch
    /// path-base. The registry is attached automatically by the same generic overload
    /// before <see cref="OnConfiguring"/> runs, so <see cref="AddArea{TArea}"/> is
    /// usable from the very first override hook.
    /// </summary>
    protected SiteOptions()
    {
    }

    /// <summary>
    /// Attaches the area registry to a <see cref="SiteOptions"/> instance built outside
    /// the framework (e.g. <c>DashboardSiteOptions</c> instantiated by <c>UseDashboard</c>).
    /// Idempotent — only the first registry sticks, so a derived host wiring the registry
    /// upfront can't be silently overwritten later. <see cref="AddArea{TArea}"/> needs
    /// the registry, so call this BEFORE running any configuration that touches areas.
    /// </summary>
    public void AttachRegistry(WebsiteAreaRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _registry ??= registry;
    }

    /// <summary>
    /// Template-method hook — runs once, AFTER <see cref="Directory"/> + registry are
    /// attached but BEFORE the consumer's <c>configure</c> lambda. Override in derived
    /// classes to seed implicit areas, install always-on hooks, or any other setup
    /// that must precede consumer customisation. The default is a no-op.
    /// </summary>
    /// <param name="services">
    /// Application-level <see cref="IServiceProvider"/> — typically used to resolve
    /// singletons that the override needs to populate (e.g. mount registries).
    /// </param>
    /// <remarks>
    /// <para>Only invoked through <see cref="WebsiteServiceCollectionExtensions.UseWebsite{TApp, TSiteOptions}"/>.
    /// Manual <c>new</c> + low-level <c>UseWebsite&lt;TApp&gt;(directory, site)</c>
    /// callers bypass this entry point and are expected to perform their own setup.</para>
    /// </remarks>
    protected virtual void OnConfiguring(IServiceProvider services)
    {
    }

    /// <summary>
    /// Template-method hook — runs once, AFTER the consumer's <c>configure</c> lambda.
    /// Override in derived classes to snapshot consumer-supplied state (e.g. final
    /// <see cref="Areas"/> list) into singletons or to add late hooks that must
    /// observe the fully-built configuration. The default is a no-op.
    /// </summary>
    /// <param name="services">
    /// Application-level <see cref="IServiceProvider"/> — same instance as
    /// <see cref="OnConfiguring"/>; typically used to resolve singletons that need a
    /// post-configuration write (e.g. <c>DashboardMountRegistry</c>).
    /// </param>
    protected virtual void OnConfigured(IServiceProvider services)
    {
    }

    /// <summary>Friend-only entry point used by <c>WebsiteServiceCollectionExtensions</c>.</summary>
    internal void InvokeOnConfiguring(IServiceProvider services) => OnConfiguring(services);

    /// <summary>Friend-only entry point used by <c>WebsiteServiceCollectionExtensions</c>.</summary>
    internal void InvokeOnConfigured(IServiceProvider services) => OnConfigured(services);

    /// <summary>
    /// URL prefix at which this Site is mounted as a strongly-typed
    /// <see cref="UrlPath"/> value object. <c>"/"</c> or <see cref="UrlPath.Empty"/>
    /// = root; otherwise a rooted segment such as <c>"/admin"</c>. Set via the first
    /// argument to <c>app.UseWebsite&lt;TApp&gt;("/", o => …)</c> — the string is
    /// implicitly converted to <see cref="UrlPath"/>. The setter is <c>internal</c>
    /// on purpose: only <c>UseWebsite&lt;TApp&gt;</c> assigns the mount path, so a
    /// consumer who hands a pre-built <see cref="SiteOptions"/> to
    /// <see cref="WebsiteServiceCollectionExtensions.UseWebsite{TApp}(WebApplication, UrlPath, SiteOptions)"/>
    /// can never desync <see cref="Directory"/> from the branch path-base.
    /// </summary>
    public UrlPath Directory { get; internal set; }

    /// <summary>
    /// Optional authorization policy required for any page rendered under
    /// <see cref="Directory"/>. <see langword="null"/> = anonymous access allowed
    /// (individual pages can still opt in via <c>[Authorize]</c> / <c>[RequirePermission]</c>).
    /// </summary>
    public string? Permission { get; set; }

    /// <summary>
    /// Blazor hosting mode for this Site. Different Sites may use different modes
    /// (e.g. public WebAssembly, admin Server) under the same host.
    /// </summary>
    public WebsiteMode Mode { get; set; } = WebsiteMode.Server;

    // ─── Per-Site middleware toggles (defaults match the ASP.NET Core template). ───

    /// <summary>
    /// HSTS header in non-Development environments. Idiomatic ASP.NET default.
    /// Disable for sites behind a TLS-terminating proxy that already sets the header.
    /// </summary>
    public bool Hsts { get; set; } = true;

    /// <summary>
    /// HTTP→HTTPS redirection (<c>UseHttpsRedirection</c>). Default <see langword="true"/> —
    /// matches the ASP.NET Core template. Set <see langword="false"/> for HTTP-only
    /// hosts (internal services, dev-only branches).
    /// </summary>
    public bool HttpsRedirection { get; set; } = true;

    /// <summary>Response compression middleware (gzip + brotli).</summary>
    public bool Compression { get; set; } = true;

    /// <summary>
    /// Forwarded-headers middleware (<c>X-Forwarded-For</c> / <c>X-Forwarded-Proto</c>).
    /// Enable when running behind nginx / traefik / Aspire ingress.
    /// </summary>
    public bool Proxy { get; set; } = false;

    /// <summary>Antiforgery middleware for Razor / form POSTs.</summary>
    public bool AntiForgery { get; set; } = true;

    /// <summary>
    /// Adds baseline security response headers to every response from this Site:
    /// <c>X-Content-Type-Options: nosniff</c>, <c>X-Frame-Options: SAMEORIGIN</c>,
    /// <c>X-XSS-Protection: 1; mode=block</c>, <c>Referrer-Policy: strict-origin-when-cross-origin</c>,
    /// and masks the <c>Server</c> header. Cheap, idempotent, on by default —
    /// disable only when an upstream proxy already injects the equivalent set.
    /// </summary>
    public bool SecurityHeaders { get; set; } = true;

    /// <summary>
    /// Exception-handler path applied in non-Development environments.
    /// <see langword="null"/> disables (Developer Exception Page handles dev requests).
    /// </summary>
    public string? ExceptionHandlerPath { get; set; } = "/error";

    /// <summary>Areas mounted under this Site, in registration order.</summary>
    public IReadOnlyList<IWebsiteArea> Areas => _areas;

    /// <summary>
    /// Early-pipeline site hooks (run at the START of this branch, after PathBase /
    /// ICurrentSite stamping, BEFORE routing/auth). Used for middleware that must wrap
    /// every request from byte zero: ImageSharp, custom static-file pipelines,
    /// per-Site request rewriting.
    /// </summary>
    internal IReadOnlyList<Action<IApplicationBuilder>> AppHooks => _appHooks;

    /// <summary>
    /// Late-pipeline site hooks (run AFTER auth + Zonit hydrators, BEFORE endpoints).
    /// Used for middleware that needs an authenticated principal / hydrated workspace
    /// (custom permission guards, audit logging tied to identity).
    /// </summary>
    internal IReadOnlyList<Action<IApplicationBuilder>> UseHooks => _useHooks;

    /// <summary>Site-level endpoint hooks (run inside this branch's <c>UseEndpoints</c>).</summary>
    internal IReadOnlyList<Action<IEndpointRouteBuilder>> EndpointHooks => _endpointHooks;

    /// <summary>
    /// Mounts an Area in this Site. <typeparamref name="TArea"/> must have already been
    /// registered at services-time via
    /// <c>builder.Services.AddWebsite(o => o.AddArea&lt;TArea&gt;())</c> — otherwise the
    /// resolver throws with a clear diagnostic.
    /// </summary>
    public SiteOptions AddArea<TArea>() where TArea : class, IWebsiteArea
    {
        if (_registry is null)
            throw new InvalidOperationException(
                "SiteOptions has no attached WebsiteAreaRegistry. Construct it via the framework " +
                "(UseWebsite<TApp> / UseWebsite<TApp, TOptions>) rather than manually.");
        var area = _registry.Resolve<TArea>();
        if (_areas.Any(a => a.Key.Equals(area.Key, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException(
                $"Area with key '{area.Key}' is already mounted on Site '{Directory}'.");
        _areas.Add(area);
        return this;
    }

    /// <summary>
    /// Early-pipeline hook — runs at the START of this Site's branch (after PathBase,
    /// before routing/auth). Use this to plug libraries that must wrap every request:
    /// <c>UseImageSharp()</c>, custom static-file pipelines, per-Site request rewriting.
    /// Multiple calls chain in registration order.
    /// </summary>
    public SiteOptions App(Action<IApplicationBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        _appHooks.Add(configure);
        return this;
    }

    /// <summary>
    /// Late-pipeline hook — runs AFTER auth + Zonit hydrators, BEFORE endpoints.
    /// Use this for middleware that needs an authenticated identity / hydrated
    /// workspace (custom permission gates, identity-tied audit logging).
    /// </summary>
    public SiteOptions Use(Action<IApplicationBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        _useHooks.Add(configure);
        return this;
    }

    /// <summary>
    /// Adds a Site-level endpoint hook (runs inside this branch's <c>UseEndpoints</c>,
    /// alongside <c>MapRazorComponents</c>). Useful for minimal-API endpoints scoped
    /// to this Site (per-Site health checks, Site-wide webhooks).
    /// </summary>
    public SiteOptions MapEndpoints(Action<IEndpointRouteBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        _endpointHooks.Add(configure);
        return this;
    }

    /// <summary>
    /// Normalised path-base form: empty string for root, otherwise <c>/segment</c>
    /// (no trailing slash). Used internally to drive <c>app.UsePathBase(…)</c> and
    /// the <c>MapWhen</c> matcher.
    /// </summary>
    internal string NormalizedPathBase
    {
        get
        {
            if (!Directory.HasValue) return string.Empty;
            var v = Directory.Value.TrimEnd('/');
            if (v.Length == 0) return string.Empty;
            return v.StartsWith('/') ? v : "/" + v;
        }
    }
}
