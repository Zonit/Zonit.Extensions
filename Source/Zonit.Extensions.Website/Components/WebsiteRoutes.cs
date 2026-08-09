using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Routing;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Zonit.Extensions.Website.Layouts.Components;

namespace Zonit.Extensions.Website;

/// <summary>
/// Built-in router for a Site: discovers routable components from the host assembly and from
/// every mounted area, and renders them through <see cref="ZonitRouteView"/> so the string-keyed
/// layout system and the document head both apply.
/// </summary>
/// <remarks>
/// <para>Saves each project the <c>Routes.razor</c> whose only real content is a list of
/// assemblies that has to be kept in step with the areas the Site mounts — a list that fails
/// quietly when it drifts, because a page in a forgotten assembly does not error, it simply
/// renders "Not found". The list is derived here from <see cref="ICurrentSite.Areas"/>, which is
/// the same thing <c>UseWebsite</c> was told.</para>
///
/// <para><b>No 404 fragment.</b> An unmatched route is a status code, and this branch already
/// turns status codes into real pages through <c>UseStatusCodePagesWithReExecute</c> — so the
/// miss renders the Site's error page with the correct 404 on the wire, rather than a 200 with
/// apologetic markup. Router-level not-found content would quietly undo that.</para>
///
/// <para><b>The default layout is a key, not a type.</b> Passing a <c>Type</c> through a
/// component parameter drags trim annotations along with it and makes the whole router
/// reflection-visible; a string resolved against <c>ILayoutRegistry</c> does not, and it matches
/// how every other layout in this framework is selected.</para>
///
/// <para>Replaceable: <c>AppBase.RoutesComponent</c> points here by default, and a derived shell
/// can point it anywhere.</para>
/// </remarks>
public sealed class WebsiteRoutes : ComponentBase
{
    [Inject] private ICurrentSite Site { get; set; } = default!;

    /// <summary>
    /// Assembly scanned for routable components. Defaults to the entry assembly, which is where a
    /// conventional application keeps its pages.
    /// </summary>
    [Parameter] public Assembly? AppAssembly { get; set; }

    /// <summary>Extra assemblies to scan, on top of the mounted areas' own.</summary>
    [Parameter] public IEnumerable<Assembly>? AdditionalAssemblies { get; set; }

    /// <summary>
    /// Layout key applied to pages that declare none. Resolved through <c>ILayoutRegistry</c>;
    /// falls back to <c>DocumentOptions.DefaultLayoutKey</c>.
    /// </summary>
    [Parameter] public string? DefaultLayoutKey { get; set; }

    // Rendering a component makes every one of its parameters reflection-visible to the trimmer,
    // including the Type-valued ones this router never sets. Router.NotFoundPage is left at its
    // default (404s are handled by the branch's status-code re-execution, not here), and
    // ZonitRouteView receives only a layout KEY — the layout type itself is rooted where it is
    // registered, by the generic AddWebsiteLayout<T>() instantiation.
    [UnconditionalSuppressMessage("Trimming", "IL2110",
        Justification = "Type-valued component parameters are never assigned by this router.")]
    [UnconditionalSuppressMessage("Trimming", "IL2111",
        Justification = "Type-valued component parameters are never assigned by this router; layout types are rooted by AddWebsiteLayout<T>().")]
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenComponent<Router>(0);
        builder.AddComponentParameter(1, nameof(Router.AppAssembly), AppAssembly ?? Assembly.GetEntryAssembly());
        builder.AddComponentParameter(2, nameof(Router.AdditionalAssemblies), ResolveAssemblies());
        builder.AddComponentParameter(3, nameof(Router.Found), (RenderFragment<RouteData>)BuildFound);
        builder.CloseComponent();
    }

    [UnconditionalSuppressMessage("Trimming", "IL2110",
        Justification = "ZonitRouteView.DefaultLayout is never assigned here; only the string key is passed.")]
    [UnconditionalSuppressMessage("Trimming", "IL2111",
        Justification = "ZonitRouteView.DefaultLayout is never assigned here; only the string key is passed.")]
    private RenderFragment BuildFound(RouteData routeData) => builder =>
    {
        builder.OpenComponent<ZonitRouteView>(0);
        builder.AddComponentParameter(1, nameof(ZonitRouteView.RouteData), routeData);
        builder.AddComponentParameter(2, nameof(ZonitRouteView.DefaultLayoutKey),
            DefaultLayoutKey ?? Site.Document.DefaultLayoutKey);
        builder.CloseComponent();
    };

    /// <summary>
    /// Area assemblies, de-duplicated and with the entry assembly removed — <c>Router</c> scans
    /// <c>AppAssembly</c> separately and would otherwise report duplicate routes for any page that
    /// lives alongside an area.
    /// </summary>
    private IEnumerable<Assembly> ResolveAssemblies()
    {
        var app = AppAssembly ?? Assembly.GetEntryAssembly();
        var seen = new HashSet<Assembly>();

        if (app is not null)
            seen.Add(app);

        var result = new List<Assembly>();

        foreach (var area in Site.Areas)
        {
            var assembly = area.GetType().Assembly;
            if (seen.Add(assembly))
                result.Add(assembly);
        }

        if (AdditionalAssemblies is not null)
        {
            foreach (var assembly in AdditionalAssemblies)
                if (seen.Add(assembly))
                    result.Add(assembly);
        }

        return result;
    }
}
