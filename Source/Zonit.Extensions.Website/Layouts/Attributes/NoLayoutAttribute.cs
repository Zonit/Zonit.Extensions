namespace Zonit.Extensions.Website;

/// <summary>
/// Marks a Razor page that opts out of <em>every</em> layout — including the Site default
/// and Blazor's <c>[Layout]</c> attribute on any base class. <c>ZonitRouteView</c>
/// renders the page directly with no wrapping <c>LayoutView</c>, giving the page total
/// control over its DOM (useful for embeddable widgets, OAuth popups, error pages that
/// must work even when the main layout is broken).
/// </summary>
/// <remarks>
/// <para><b>Precedence.</b> Wins over <see cref="LayoutKeyAttribute"/>, <c>[Layout]</c> and
/// the Site default. The only thing that can still override it is the runtime
/// <c>PageBase.LayoutKey</c> dynamic context (which can be set to a real key to
/// re-enable a layout for a particular session).</para>
///
/// <para><b>Auth still applies.</b> <c>[Authorize]</c> on the page is honoured by
/// <c>ZonitRouteView</c>'s wrapped <c>AuthorizeRouteView</c> regardless of
/// layout state.</para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class NoLayoutAttribute : Attribute;
