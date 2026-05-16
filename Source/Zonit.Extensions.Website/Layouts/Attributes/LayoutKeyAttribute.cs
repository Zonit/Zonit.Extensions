namespace Zonit.Extensions.Website;

/// <summary>
/// Marks a Razor page as opting into Zonit's string-keyed layout registry. The page
/// will be rendered inside the <see cref="System.Type"/> that the host registered
/// under <see cref="Key"/> via <c>services.AddWebsiteLayout&lt;TLayout&gt;("...")</c>
/// — the page itself does <b>not</b> need a reference to the layout's assembly.
/// </summary>
/// <remarks>
/// <para><b>Why an attribute, not a property?</b> Blazor's <c>Router</c> picks the layout
/// from <c>Type.GetCustomAttribute&lt;LayoutAttribute&gt;()</c> on the page type — i.e.
/// at the type level, before the page instance exists. A <c>protected virtual</c> property
/// on <see cref="PageBase"/> would be read too late. <see cref="LayoutKeyAttribute"/>
/// fills that exact gap with a string indirection: the page declares <em>intent</em>
/// (<c>"Auth.Login"</c>), the host decides which concrete <see cref="System.Type"/>
/// implements that intent.</para>
///
/// <para><b>Resolution order</b> (highest to lowest priority) inside
/// <c>ZonitRouteView</c>:</para>
/// <list type="number">
///   <item>Dynamic override set at runtime via <c>PageBase.LayoutKey</c> (rare; for
///         pages that flip layout mid-render based on auth state etc.).</item>
///   <item><see cref="NoLayoutAttribute"/> — render the page raw, no chrome.</item>
///   <item><see cref="LayoutKeyAttribute"/> — resolve via <c>ILayoutRegistry</c>.</item>
///   <item>Standard <c>[Layout(typeof(X))]</c> — Blazor's built-in path, untouched.</item>
///   <item>Site / router default layout.</item>
/// </list>
///
/// <para><b>Fallback when the key is not registered</b>: the route view logs a warning
/// and falls back to the Site default. The app never crashes for a missing key — that
/// keeps plug-ins installed in any order from breaking the host. Strict-mode validation
/// at startup is an opt-in for a future <c>WebsiteOptions.ValidateLayoutKeysOnStartup</c>
/// switch.</para>
///
/// <para><b>Plug-in usage</b> (no reference to the layout's assembly required):</para>
/// <code>
/// @page "/login"
/// @attribute [LayoutKey("Minimal")]
/// @inherits PageBase
/// </code>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class LayoutKeyAttribute(string key) : Attribute
{
    /// <summary>The case-insensitive key looked up in <c>ILayoutRegistry</c>.</summary>
    public string Key { get; } = string.IsNullOrWhiteSpace(key)
        ? throw new ArgumentException("Layout key must be non-empty.", nameof(key))
        : key;
}
