namespace Zonit.Extensions.Website;

/// <summary>
/// Singleton snapshot of every Site mount registered through
/// <see cref="WebsiteServiceCollectionExtensions.UseWebsite{TApp}(Microsoft.AspNetCore.Builder.WebApplication, UrlPath, Action{SiteOptions})"/>
/// (and its generic / pre-built siblings). Each entry pins the per-mount
/// configuration that <see cref="ICurrentSite"/> consumers need (Directory,
/// Permission, Areas list) keyed by the mount's
/// <see cref="SiteOptions.NormalizedPathBase"/>.
/// </summary>
/// <remarks>
/// <para><b>Why a singleton?</b> The scoped <see cref="ICurrentSite"/> is populated
/// from the per-Site branch middleware
/// (<c>branch.Use(ctx =&gt; current.Set(site))</c> inside <c>BuildBranch</c>). That
/// middleware fires only for the HTTP request scope; the SignalR <em>circuit</em>
/// scope that owns interactive Blazor components is freshly built and never passes
/// through it. Reading <see cref="ICurrentSite.Areas"/> from inside an
/// <c>@rendermode="InteractiveServer"</c> component therefore returns the unset
/// default (<c>Areas=Array.Empty</c>) right after the SSR pass hydrates — every
/// consumer that branches on <c>AreaKeys.Count</c> (the dashboard's
/// <c>NavGroups</c> projection, the <c>Mounted areas</c> counter on the dashboard
/// Index page, the <c>NavigationService</c> filter) silently collapses to an empty
/// state and the chrome "flashes" from a correctly-populated SSR render to a blank
/// interactive render.</para>
///
/// <para>This registry sidesteps the gap: it is populated once per
/// <c>UseWebsite</c> call at startup and looked up by mount path from the same
/// <see cref="Microsoft.AspNetCore.Components.NavigationManager.BaseUri"/> that is
/// available in BOTH scopes. The scoped <see cref="ICurrentSite"/> falls back to
/// this snapshot whenever the middleware-driven <see cref="ICurrentSite.Set"/> has
/// not been called on the current scope.</para>
///
/// <para><b>Companion of <see cref="WebsiteAreaRegistry"/>:</b> the area registry
/// stores <em>every</em> area registered process-wide so <c>UseWebsite</c> can
/// resolve <c>o.AddArea&lt;TArea&gt;()</c> back to the singleton instance; this
/// registry stores which subset of those areas is mounted at which path so a
/// scoped consumer can recover its per-mount view of the world without a request
/// scope.</para>
///
/// <para><b>Lookup key.</b> Mount paths are stored exactly as
/// <see cref="SiteOptions.NormalizedPathBase"/> emits them — empty string for the
/// root mount, otherwise a rooted segment with no trailing slash (e.g.
/// <c>"/dashboard"</c>). Lookups tolerate trailing slashes and deeper sub-paths
/// (longest-prefix match), so callers can hand the registry whatever shape they
/// have at hand (<c>Request.Path</c>, <c>NavigationManager.BaseUri</c>'s
/// <see cref="System.Uri.AbsolutePath"/>, etc.).</para>
/// </remarks>
public sealed class WebsiteMountRegistry
{
    private readonly Dictionary<string, MountSnapshot> _byMount =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Captures the per-mount state from <paramref name="site"/> into a snapshot
    /// keyed by the mount's <see cref="SiteOptions.NormalizedPathBase"/>. Idempotent
    /// in the same sense as <see cref="DashboardMountRegistry.Register"/> — calling
    /// twice with the same key overwrites (last-call-wins matches the way
    /// <c>UseWebsite</c> is expected to be invoked exactly once per mount).
    /// </summary>
    public void Register(SiteOptions site)
    {
        ArgumentNullException.ThrowIfNull(site);
        var key = site.NormalizedPathBase;
        _byMount[key] = new MountSnapshot(
            site.Directory,
            site.Permission,
            site.Areas);
    }

    /// <summary>
    /// Resolves the mount snapshot that owns <paramref name="absolutePath"/>.
    /// Empty / <see langword="null"/> path resolves the root mount when it exists.
    /// </summary>
    /// <param name="absolutePath">
    /// Absolute URL path — typically <c>HttpContext.Request.PathBase</c> (set by
    /// <c>UsePathBase</c> inside the branch) or
    /// <c>new Uri(NavigationManager.BaseUri).AbsolutePath</c>. Both are valid
    /// because the registry tolerates trailing slashes and longer paths via
    /// longest-prefix-match.
    /// </param>
    /// <returns>
    /// The owning <see cref="MountSnapshot"/>, or <see langword="null"/> when no
    /// registered mount covers the supplied path.
    /// </returns>
    public MountSnapshot? ForMount(string? absolutePath)
    {
        var path = absolutePath?.TrimEnd('/') ?? string.Empty;

        // Fast path — exact normalised-key hit.
        if (_byMount.TryGetValue(path, out var direct))
            return direct;

        // Longest-prefix-match fallback. Necessary when the caller hands us a
        // deep sub-path like "/dashboard/users" but only the mount root
        // "/dashboard" is registered. Identical contract to
        // DashboardMountRegistry.ForMount — see remarks there for the rationale.
        MountSnapshot? best = null;
        var bestKeyLength = -1;
        foreach (var (key, snapshot) in _byMount)
        {
            if (key.Length <= bestKeyLength) continue;
            if (key.Length == 0 ||
                path.Equals(key, StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith(key + "/", StringComparison.OrdinalIgnoreCase))
            {
                best = snapshot;
                bestKeyLength = key.Length;
            }
        }

        // No prefix hit — fall back to the registered root mount (if any) so a
        // caller asking for "/" or "" while only "/dashboard" is registered does
        // not get a null. This mirrors what the branch middleware would set on
        // the request scope: the root mount Site, with its zero-area default.
        if (best is null && _byMount.TryGetValue(string.Empty, out var root))
            return root;

        return best;
    }

    /// <summary>
    /// Per-mount immutable snapshot consumed by <see cref="ICurrentSite"/> when the
    /// middleware-populated request scope is unavailable (interactive circuit, host
    /// background tasks, anywhere outside a branch).
    /// </summary>
    /// <param name="Directory">URL prefix of the mount — empty for the root mount.</param>
    /// <param name="Permission">Authorization policy guarding the mount, if any.</param>
    /// <param name="Areas">
    /// Areas mounted at this path. Holds the same <see cref="IWebsiteArea"/>
    /// singleton references that the per-Site branch middleware uses, so consumers
    /// reading from a circuit scope see the identical set without serialization.
    /// </param>
    public sealed record MountSnapshot(
        UrlPath Directory,
        string? Permission,
        IReadOnlyList<IWebsiteArea> Areas);
}
