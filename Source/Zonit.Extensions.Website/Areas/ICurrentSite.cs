using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;

namespace Zonit.Extensions.Website;

/// <summary>
/// Per-request marker identifying which Site the current HTTP request is being
/// served from. Set by the per-Site branch middleware in
/// <c>app.UseWebsite&lt;TApp&gt;(o => …)</c>, read by services that need to scope
/// their output to the active mount-point (navigation, breadcrumbs, layout chrome).
/// </summary>
/// <remarks>
/// <para>Scoped service. The default implementation self-hydrates from
/// <see cref="WebsiteMountRegistry"/> (a singleton populated by every
/// <c>UseWebsite</c> call at startup) whenever the per-Site branch middleware has
/// not had a chance to call <see cref="Set"/> on the current scope — most notably
/// in the SignalR <em>circuit</em> scope that owns interactive Blazor components.
/// The circuit lifecycle never passes through <c>branch.Use(...)</c>, so without
/// the singleton fallback an <c>@rendermode="InteractiveServer"</c> page would see
/// <see cref="Areas"/>=<see cref="Array.Empty{T}"/> the moment it hydrates — the
/// classic "SSR renders correctly, then flashes blank" bug.</para>
///
/// <para>The fallback infers the active mount from
/// <see cref="HttpContext.Request"/> when an HTTP context is available
/// (<c>Request.PathBase</c> first, falling back to <c>Request.Path</c>) and from
/// <see cref="NavigationManager.BaseUri"/> when it is not (interactive circuit
/// scope, hosted services, background tasks rendering off-thread). Both signals
/// resolve to a path that <see cref="WebsiteMountRegistry.ForMount"/> matches
/// against the registered mount keys using the same longest-prefix logic as
/// <c>DashboardMountRegistry.ForMount</c>.</para>
///
/// <para><see cref="IsSet"/> stays <see langword="true"/> whenever either source
/// produced a Site — explicit middleware call OR singleton fallback — so
/// consumers branching on it (e.g. the dashboard's <c>NavGroups</c> projection,
/// the <c>NavigationService.Get</c> filter, the dashboard Index page's
/// <c>Mounted areas</c> counter) get the same answer in HTTP and circuit scopes.</para>
/// </remarks>
public interface ICurrentSite
{
    /// <summary>
    /// <see langword="true"/> when either the per-Site branch middleware has
    /// called <see cref="Set"/> or <see cref="WebsiteMountRegistry"/> resolved a
    /// mount for the current request / circuit. <see langword="false"/> only when
    /// the consumer is reading outside any registered mount.
    /// </summary>
    bool IsSet { get; }

    /// <summary>
    /// URL prefix of the active Site as a <see cref="UrlPath"/> VO.
    /// <see cref="UrlPath.Empty"/> for the root Site, otherwise a rooted segment
    /// such as <c>"/admin"</c>. Mirrors <see cref="SiteOptions.Directory"/>.
    /// </summary>
    UrlPath Directory { get; }

    /// <summary>Optional permission gating the Site (mirrors <see cref="SiteOptions.Permission"/>).</summary>
    string? Permission { get; }

    /// <summary>Areas mounted under the active Site, in registration order.</summary>
    IReadOnlyList<IWebsiteArea> Areas { get; }

    /// <summary>Stable area keys mounted under the active Site (case-insensitive set).</summary>
    IReadOnlySet<string> AreaKeys { get; }

    /// <summary>
    /// Called once by the per-Site branch middleware. Subsequent calls overwrite
    /// and pin the explicit Site, so the singleton fallback is no longer consulted
    /// on this scope.
    /// </summary>
    void Set(SiteOptions site);
}

internal sealed class CurrentSite : ICurrentSite
{
    private readonly WebsiteMountRegistry _mounts;
    private readonly IHttpContextAccessor _httpContext;
    private readonly IServiceProvider _services;

    // Explicit middleware-driven state. When _explicit is true these fields win;
    // otherwise every accessor falls through to the resolved registry snapshot.
    private bool _explicit;
    private UrlPath _directory = UrlPath.Empty;
    private string? _permission;
    private IReadOnlyList<IWebsiteArea> _areas = Array.Empty<IWebsiteArea>();
    private IReadOnlySet<string> _areaKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    // Cached singleton lookup. _resolveAttempted suppresses repeat NavigationManager
    // probes inside the same scope (BaseUri throws if accessed before the circuit
    // initialised it; we want to fail open exactly once per scope).
    private WebsiteMountRegistry.MountSnapshot? _resolved;
    private bool _resolveAttempted;

    public CurrentSite(
        WebsiteMountRegistry mounts,
        IHttpContextAccessor httpContext,
        IServiceProvider services)
    {
        _mounts = mounts;
        _httpContext = httpContext;
        _services = services;
    }

    public void Set(SiteOptions site)
    {
        ArgumentNullException.ThrowIfNull(site);
        _directory = site.Directory;
        _permission = site.Permission;
        _areas = site.Areas;
        _areaKeys = site.Areas
            .Select(a => a.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        _explicit = true;
    }

    public bool IsSet => _explicit || ResolveSnapshot() is not null;

    public UrlPath Directory => _explicit
        ? _directory
        : ResolveSnapshot()?.Directory ?? UrlPath.Empty;

    public string? Permission => _explicit
        ? _permission
        : ResolveSnapshot()?.Permission;

    public IReadOnlyList<IWebsiteArea> Areas => _explicit
        ? _areas
        : ResolveSnapshot()?.Areas ?? Array.Empty<IWebsiteArea>();

    public IReadOnlySet<string> AreaKeys
    {
        get
        {
            if (_explicit) return _areaKeys;
            var snap = ResolveSnapshot();
            if (snap is null) return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            return snap.Areas
                .Select(a => a.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Resolve the owning mount snapshot lazily. The lookup runs at most once per
    /// scope (subsequent calls return the cached value) so repeated accessor reads
    /// on hot render paths cost a single field read.
    /// </summary>
    private WebsiteMountRegistry.MountSnapshot? ResolveSnapshot()
    {
        if (_resolveAttempted) return _resolved;
        _resolveAttempted = true;

        var path = ResolveActivePath();
        if (path is null) return null;

        _resolved = _mounts.ForMount(path);
        return _resolved;
    }

    /// <summary>
    /// Best-effort resolution of the active mount path:
    /// <list type="number">
    ///   <item><c>HttpContext.Request.PathBase</c> when the branch's
    ///         <c>UsePathBase</c> already ran on this request (typical for static
    ///         file / non-Razor endpoints reading <see cref="ICurrentSite"/> after
    ///         the branch's middleware).</item>
    ///   <item><c>HttpContext.Request.Path</c> when an HTTP request is present but
    ///         <c>UsePathBase</c> has not stripped the mount yet (global middleware
    ///         running before the <c>MapWhen</c> branch).</item>
    ///   <item><c>NavigationManager.BaseUri</c> when no HTTP context is available
    ///         (interactive Blazor circuit scope, hosted services). The circuit
    ///         exposes <see cref="NavigationManager"/> as a scoped service so this
    ///         resolves cleanly without a request-time dependency.</item>
    /// </list>
    /// Returns <see langword="null"/> when none of the signals are usable —
    /// callers fall back to <see cref="Array.Empty{T}"/> / unset defaults.
    /// </summary>
    private string? ResolveActivePath()
    {
        var http = _httpContext.HttpContext;
        if (http is not null)
        {
            if (http.Request.PathBase.HasValue)
                return http.Request.PathBase.Value;
            if (http.Request.Path.HasValue)
                return http.Request.Path.Value;
        }

        // NavigationManager is registered by AddRazorComponents (Blazor Web App
        // shared framework). When the host opts out of Razor Components the
        // service is absent — falling through to null is the documented
        // unhydrated-state contract.
        var nav = _services.GetService(typeof(NavigationManager)) as NavigationManager;
        if (nav is null) return null;

        // Accessing BaseUri before the circuit initialised throws; the very few
        // scenarios that hit this (e.g. a background renderer started before the
        // first user navigation) are correctly reported as "unhydrated" by
        // catching here.
        try
        {
            return new Uri(nav.BaseUri).AbsolutePath;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
