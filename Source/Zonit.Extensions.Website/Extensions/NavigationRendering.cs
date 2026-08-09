namespace Zonit.Extensions.Website;

/// <summary>
/// Turns a navigation model into the exact <c>href</c> an anchor needs, whichever kind of
/// destination it carries.
/// </summary>
/// <remarks>
/// <para><b>The bug this exists to prevent.</b> Renderers used to call
/// <c>item.Url.ToHref()</c> directly. <see cref="UrlPath"/> rejects absolute addresses by
/// design — it is the in-site path type — so an author putting <c>"https://twitter.com/acme"</c>
/// into a navigation item got an exception from the value object's implicit conversion, at
/// startup, with nothing pointing at the menu entry that caused it. External links now live in
/// <c>External</c> and every renderer funnels through here instead of reaching for one
/// property.</para>
///
/// <para>An external destination is emitted verbatim. An in-site one is emitted <em>relative</em>
/// so it resolves against the active <c>&lt;base href&gt;</c> — which is what carries both the
/// mount and the culture prefix. See <see cref="UrlPathRendering.ToHref"/>.</para>
/// </remarks>
public static class NavigationRendering
{
    /// <summary>Href for a navigation item — external address if it has one, else the in-site path.</summary>
    public static string ToHref(this NavItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.External.HasValue ? item.External.Value : item.Url.ToHref();
    }

    /// <summary>Href for a link model — external address if it has one, else the in-site path.</summary>
    public static string ToHref(this LinkModel link)
    {
        ArgumentNullException.ThrowIfNull(link);
        return link.External.HasValue ? link.External.Value : link.Url.ToHref();
    }

    /// <summary>
    /// Whether this item can be marked "active" by comparing against the current route.
    /// </summary>
    /// <remarks>
    /// External links never can: the current path is a path within this site, and an absolute URL
    /// on another host has nothing to compare against. A renderer that skips this check will
    /// happily highlight a social link whose path happens to collide.
    /// </remarks>
    public static bool IsMatchable(this NavItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return !item.External.HasValue && item.Url.HasValue;
    }
}
