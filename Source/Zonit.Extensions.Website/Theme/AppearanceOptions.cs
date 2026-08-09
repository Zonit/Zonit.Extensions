namespace Zonit.Extensions.Website;

/// <summary>
/// How the document exposes the active colour scheme to CSS. Structural, so it is declared in
/// code and fixed for the lifetime of the process.
/// </summary>
/// <remarks>
/// <para><b>The theme never reaches the server.</b> A blocking inline script stamps the scheme
/// onto the root element before the first paint, reading the cookie and falling back to
/// <c>prefers-color-scheme</c>. Nothing about the rendered HTML depends on it.</para>
///
/// <para>That is a deliberate architectural choice, not a shortcut. Rendering the theme
/// server-side would make every HTML response vary by cookie, which costs the entire site its
/// shared-cache hit rate — a far larger loss than the feature is worth. It would also mean the
/// theme could not change without a round trip, which on a statically rendered page means a full
/// reload. Toggling an attribute is a CSS recalculation: instant, identical in static and
/// interactive rendering, and free of a flash of the wrong scheme.</para>
///
/// <para>Any styling system consumes it — this is a plain attribute selector, not a Tailwind
/// feature:</para>
/// <code>
/// /* Tailwind v4 */
/// @custom-variant dark (&amp;:where([data-theme="dark"], [data-theme="dark"] *));
///
/// /* Tailwind v3 */
/// darkMode: ['selector', '[data-theme="dark"]']
///
/// /* plain CSS */
/// :root { --bg: #fff } [data-theme="dark"] { --bg: #111 }
/// </code>
///
/// <para><b>Which scheme is the default is not here</b> — that is a branding decision and lives
/// with the rest of them, in the tenant's Theme setting. Everything on this type is the plumbing:
/// names and identifiers that a deployment cannot sensibly retune and that a page's markup and
/// CSS are written against.</para>
///
/// <para>The defaults carry no product or framework name. Cookie names and global identifiers are
/// visible to anyone who opens developer tools, and a recognisable one advertises exactly which
/// open-source stack and version to go looking for advisories against.</para>
/// </remarks>
public sealed class AppearanceOptions
{
    /// <summary>
    /// Whether the document shell emits the theme bootstrap script at all. Disable for a Site
    /// that ships a single fixed scheme.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Root-element attribute carrying the scheme — <c>&lt;html data-theme="dark"&gt;</c>.
    /// Change it to <c>"class"</c> to drive Tailwind's stock <c>darkMode: 'class'</c> instead.
    /// </summary>
    public string Attribute { get; set; } = "data-theme";

    /// <summary>Cookie persisting an explicit choice. Absent means follow the tenant's default.</summary>
    public string CookieName { get; set; } = "theme";

    /// <summary>
    /// Name of the global object the bootstrap script installs, exposing <c>toggle()</c>,
    /// <c>set(mode)</c> and <c>get()</c>. This is what a switcher's <c>onclick</c> calls.
    /// </summary>
    public string GlobalName { get; set; } = "__theme";
}
