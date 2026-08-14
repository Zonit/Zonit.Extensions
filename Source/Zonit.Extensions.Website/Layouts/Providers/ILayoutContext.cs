namespace Zonit.Extensions.Website;

/// <summary>
/// Per-circuit / per-request runtime override channel for layout selection. Page
/// components (typically derived from <see cref="PageBase"/>) push a layout decision
/// here at runtime — e.g. inside <c>OnInitialized</c> after consulting auth state —
/// and <c>ZonitRouteView</c> picks it up via <see cref="OnChange"/>
/// and re-renders with the new layout.
/// </summary>
/// <remarks>
/// <para><b>Why not just a property on the page?</b> Blazor's <c>Router</c> reads the
/// layout from the page's <see cref="Type"/> attributes <em>before</em> the page is
/// instantiated, so a property cannot influence the very first render. The static
/// path is <see cref="LayoutKeyAttribute"/> / <see cref="NoLayoutAttribute"/>; this
/// context is the <em>dynamic</em> path for the (rarer) case where the layout must
/// change after the page is alive.</para>
///
/// <para><b>Lifetime.</b> Scoped — one instance per Blazor Server circuit. Pages
/// share it across the circuit; navigating to a new page automatically clears any
/// prior override (the route view resets the context when <c>RouteData.PageType</c>
/// changes), so plug-ins do not need to remember to call <see cref="ClearOverride"/>
/// themselves.</para>
///
/// <para><b>Three-state encoding</b>:</para>
/// <list type="bullet">
///   <item><see cref="HasOverride"/> == <see langword="false"/> — no dynamic override;
///         the route view falls back to the static attribute path.</item>
///   <item><see cref="HasOverride"/> == <see langword="true"/> &amp;&amp; <see cref="IsNoLayout"/>
///         == <see langword="true"/> — render raw, no chrome.</item>
///   <item><see cref="HasOverride"/> == <see langword="true"/> &amp;&amp; <see cref="Key"/>
///         is a string — resolve via <c>ILayoutRegistry</c>. Empty key means "Site default".</item>
/// </list>
/// </remarks>
public interface ILayoutContext
{
    /// <summary><see langword="true"/> when a dynamic override is currently active.</summary>
    bool HasOverride { get; }

    /// <summary>The active layout key when <see cref="HasOverride"/> is set; may be empty (= Site default).</summary>
    string? Key { get; }

    /// <summary><see langword="true"/> when the active override is <c>NoLayout</c>.</summary>
    bool IsNoLayout { get; }

    /// <summary>
    /// Sets the dynamic layout key. Pass <c>null</c> to switch into "no layout" mode
    /// (equivalent to <see cref="NoLayoutAttribute"/>); pass <c>""</c> to fall back
    /// to the Site default; pass any other string to resolve via <c>ILayoutRegistry</c>.
    /// Fires <see cref="OnChange"/> only when the effective value actually changes.
    /// </summary>
    void SetKey(string? key);

    /// <summary>
    /// Drops any active override and reverts to the static attribute path. Called by
    /// the route view on navigation; consumers rarely need to call this directly.
    /// </summary>
    void ClearOverride();

    /// <summary>
    /// Width the routed page asked for. <c>ZonitRouteView</c> sets it from
    /// <c>[WebsiteWidth]</c> before the first render and from <c>PageBase.Width</c> after; the
    /// layout reads it and maps it to whatever its design system uses.
    /// </summary>
    /// <remarks>
    /// On the same context as the layout key because it is the same kind of fact — something the
    /// page says and the chrome around it obeys. A second mechanism would be a second thing to
    /// learn for no gain.
    /// </remarks>
    PageWidth Width { get; }

    /// <summary>
    /// Sets the page's content width. Fires <see cref="OnChange"/> only on an actual change.
    /// </summary>
    void SetWidth(PageWidth width);

    /// <summary>Raised when <see cref="HasOverride"/> / <see cref="Key"/> / <see cref="IsNoLayout"/> / <see cref="Width"/> change.</summary>
    event Action? OnChange;
}
