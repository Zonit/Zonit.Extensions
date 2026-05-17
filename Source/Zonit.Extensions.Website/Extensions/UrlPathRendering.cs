namespace Zonit.Extensions.Website;

/// <summary>
/// Rendering helpers that turn a <see cref="UrlPath"/> value object into the exact
/// shape an HTML attribute consumer needs. Kept here (rather than on the VO itself)
/// because <see cref="UrlPath"/> lives in the framework-agnostic
/// <c>Zonit.Extensions</c> assembly and intentionally has no notion of Blazor's
/// <c>&lt;base href&gt;</c> mechanics — that concern is a Website-host detail.
/// </summary>
/// <remarks>
/// <para><b>The bug this exists to prevent.</b> A typical area declares its
/// navigation as <c>new NavItem { Url = "/components" }</c>. <see cref="UrlPath"/>
/// preserves the leading slash because the VO's contract is "give back what the
/// author wrote". The renderer then emits <c>&lt;a href="/components"&gt;</c> —
/// which is an <em>absolute</em> path in HTML and therefore <em>bypasses</em> any
/// active <c>&lt;base href&gt;</c>. On a dashboard mounted at <c>/dashboard</c>
/// (with <c>&lt;base href="/dashboard/"&gt;</c> emitted by <c>DashboardApp.razor</c>)
/// the user clicks the dashboard's Components link and ends up on the root site's
/// <c>/components</c> page — same route, wrong chrome, no breadcrumbs.</para>
///
/// <para><b>The fix.</b> Emitting the same path WITHOUT the leading slash makes
/// the URL <em>relative</em>, so the browser resolves it against the active
/// <c>&lt;base href&gt;</c>: <c>href="components"</c> + <c>base="/dashboard/"</c>
/// → <c>/dashboard/components</c>. The framework's NavMenu / breadcrumb renderers
/// funnel every <see cref="UrlPath"/> through <see cref="ToHref"/> so consumers
/// never have to remember this gotcha per-area.</para>
/// </remarks>
public static class UrlPathRendering
{
    /// <summary>
    /// Returns the path in a form safe to drop into an HTML <c>href</c> attribute
    /// without breaking <c>&lt;base href&gt;</c> resolution — the leading <c>/</c>
    /// (if any) is stripped so the URL is treated as <em>relative</em> to the
    /// active base. Empty input round-trips as the empty string so an empty
    /// breadcrumb / nav item renders <c>href=""</c> (i.e. "stay on the current
    /// page") rather than <c>href="/"</c> (i.e. "navigate to the host root").
    /// </summary>
    /// <remarks>
    /// <para>This is the renderer's counterpart to <see cref="UrlPath.ToAbsolutePath"/>:
    /// where <see cref="UrlPath.ToAbsolutePath"/> guarantees a rooted form for
    /// scenarios that need an absolute path (cross-mount redirects, server-side
    /// <c>Location:</c> headers), <see cref="ToHref"/> guarantees a relative form
    /// for scenarios that want path-base resolution to kick in.</para>
    /// </remarks>
    public static string ToHref(this UrlPath path)
    {
        var raw = path.Value;
        if (string.IsNullOrEmpty(raw)) return string.Empty;
        return raw[0] == '/' ? raw[1..] : raw;
    }
}
