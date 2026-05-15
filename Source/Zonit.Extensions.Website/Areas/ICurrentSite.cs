namespace Zonit.Extensions.Website;

/// <summary>
/// Per-request marker identifying which Site the current HTTP request is being
/// served from. Set by the per-Site branch middleware in
/// <c>app.UseWebsite&lt;TApp&gt;(o => …)</c>, read by services that need to scope
/// their output to the active mount-point (navigation, breadcrumbs, layout chrome).
/// </summary>
/// <remarks>
/// <para>Scoped service. Outside an active Site branch (e.g. a request that hit a
/// global endpoint declared after every <c>UseWebsite&lt;TApp&gt;()</c> call) the
/// instance is in its initial unset state — <see cref="IsSet"/> returns
/// <see langword="false"/> and consumers should fall back to area-agnostic defaults.</para>
/// </remarks>
public interface ICurrentSite
{
    /// <summary>True after the branch middleware has called <see cref="Set"/>.</summary>
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

    /// <summary>Called once by the per-Site branch middleware. Subsequent calls overwrite.</summary>
    void Set(SiteOptions site);
}

internal sealed class CurrentSite : ICurrentSite
{
    public bool IsSet { get; private set; }
    public UrlPath Directory { get; private set; } = UrlPath.Empty;
    public string? Permission { get; private set; }
    public IReadOnlyList<IWebsiteArea> Areas { get; private set; } = Array.Empty<IWebsiteArea>();
    public IReadOnlySet<string> AreaKeys { get; private set; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public void Set(SiteOptions site)
    {
        ArgumentNullException.ThrowIfNull(site);
        Directory = site.Directory;
        Permission = site.Permission;
        Areas = site.Areas;
        AreaKeys = site.Areas
            .Select(a => a.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        IsSet = true;
    }
}
