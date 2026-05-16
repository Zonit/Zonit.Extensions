namespace Zonit.Extensions.Website;

/// <summary>
/// Base class for Razor pages in a Zonit-hosted Site. Inherits the full DI surface of
/// <see cref="ExtensionsBase"/> (Culture / Workspace / Catalog / Authenticated / Toast
/// / Cookie / Tenant / Breadcrumbs) and adds the dynamic <see cref="LayoutKey"/> hook
/// so a page can switch its rendering layout at runtime.
/// </summary>
/// <remarks>
/// <para><b>Prefer static attributes for layout selection.</b> Most pages should
/// declare their layout via class-level attributes — read by <c>ZonitRouteView</c>
/// <em>before</em> the page is instantiated and applied on the very first render:</para>
/// <list type="bullet">
///   <item><c>[LayoutKey("Auth.LoginBox")]</c> — pick from <c>ILayoutRegistry</c> by string.</item>
///   <item><c>[NoLayout]</c> — render raw, no chrome.</item>
///   <item>Standard <c>[Layout(typeof(X))]</c> — Blazor's built-in path.</item>
/// </list>
///
/// <para><b>Use <see cref="LayoutKey"/> only for runtime-dependent decisions</b> —
/// e.g. "use 'Login' layout for unauthenticated users, 'Dashboard' for authenticated"
/// on a single endpoint. Setting <see cref="LayoutKey"/> after the first render causes
/// exactly one re-render with the new layout; the user sees a brief flicker. The static
/// attribute path has no flicker.</para>
/// </remarks>
public abstract class PageBase : ExtensionsBase
{
    /// <summary>
    /// Dynamic layout key for this page. Mirrors <c>ILayoutContext.Key</c>.
    /// </summary>
    /// <remarks>
    /// <para>Setter semantics:</para>
    /// <list type="bullet">
    ///   <item><c>null</c> — render with <em>no</em> layout (equivalent to a runtime
    ///         <see cref="NoLayoutAttribute"/>).</item>
    ///   <item><c>""</c> (empty) — fall back to the Site / router default layout
    ///         (overrides any static <see cref="LayoutKeyAttribute"/> on the page).</item>
    ///   <item>Any other string — resolved via <c>ILayoutRegistry</c>. Missing keys
    ///         log a warning and fall back to the Site default.</item>
    /// </list>
    /// <para>The override is cleared automatically when the user navigates to a
    /// different page (<c>ZonitRouteView</c> handles the reset); pages do not need to
    /// undo it manually.</para>
    /// </remarks>
    protected string? LayoutKey
    {
        get => LayoutContext.HasOverride ? LayoutContext.Key : null;
        set => LayoutContext.SetKey(value);
    }
}
