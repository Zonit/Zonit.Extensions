using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.Logging;
using Zonit.Extensions.Website.Layouts.Repositories;

namespace Zonit.Extensions.Website.Layouts.Components;

/// <summary>
/// Drop-in replacement for <see cref="Microsoft.AspNetCore.Components.Authorization.AuthorizeRouteView"/>
/// that understands Zonit's string-keyed layout system. Place inside the
/// <c>&lt;Found&gt;</c> block of a <see cref="Microsoft.AspNetCore.Components.Routing.Router"/>:
/// </summary>
/// <remarks>
/// <code>
/// &lt;Router AppAssembly="@typeof(Program).Assembly" AdditionalAssemblies="@_asms"&gt;
///   &lt;Found Context="routeData"&gt;
///     &lt;ZonitRouteView RouteData="@routeData" DefaultLayout="@typeof(MainLayout)" /&gt;
///   &lt;/Found&gt;
/// &lt;/Router&gt;
/// </code>
///
/// <para><b>Layout resolution order</b> (highest priority wins):</para>
/// <list type="number">
///   <item>Dynamic <see cref="ILayoutContext"/> override (set via <c>PageBase.LayoutKey</c>
///         at runtime). <c>HasOverride == true &amp;&amp; IsNoLayout</c> ⇒ render with
///         a <c>null</c> layout; <c>HasOverride == true &amp;&amp; Key</c> ⇒
///         <c>ILayoutRegistry.TryResolve</c>.</item>
///   <item>Static <see cref="NoLayoutAttribute"/> on the page type ⇒ render with a
///         <c>null</c> layout.</item>
///   <item>Static <see cref="LayoutKeyAttribute"/> ⇒ <c>ILayoutRegistry.TryResolve</c>.</item>
///   <item>Standard Blazor <c>[Layout(typeof(X))]</c> ⇒ honoured by the wrapped
///         <see cref="AuthorizeRouteView"/> (page-level attribute always wins over our
///         resolved value when present).</item>
///   <item><see cref="DefaultLayout"/> parameter (the Site / router fallback).</item>
/// </list>
///
/// <para><b>Caveat — <c>[NoLayout]</c> + page-level <c>[Layout]</c>.</b> If a page
/// declares both, Blazor's <see cref="AuthorizeRouteView"/> will still honour the
/// page-level <c>[Layout]</c> attribute (we have no hook to override that). Treat
/// <c>[NoLayout]</c> and <c>[Layout]</c> as mutually exclusive at the page level —
/// the static analyser cannot enforce it, but mixing them is undefined behaviour.</para>
///
/// <para><b>Navigation reset.</b> When <see cref="RouteData.PageType"/> changes (i.e.
/// the user navigated to a different page), any dynamic override is cleared automatically
/// so plug-ins do not have to remember to undo it. Authorization is delegated to the
/// wrapped <see cref="AuthorizeRouteView"/> verbatim, so <c>[Authorize]</c> /
/// <c>[RequirePermission]</c> / <c>[RequireRole]</c> attributes keep working unchanged.</para>
///
/// <para><b>Missing keys.</b> A <see cref="LayoutKeyAttribute"/> pointing at an
/// unregistered key logs <see cref="LogLevel.Warning"/> exactly <em>once</em> per
/// (key, page-type) pair and falls back to <see cref="DefaultLayout"/>. The host
/// keeps working; the missing layout shows up in logs.</para>
/// </remarks>
public sealed class ZonitRouteView : ComponentBase, IDisposable
{
    [Inject] private ILayoutRegistry Registry { get; set; } = default!;
    [Inject] private ILayoutContext Context { get; set; } = default!;
    [Inject] private ILogger<ZonitRouteView> Logger { get; set; } = default!;

    /// <summary>Route data describing the matched route + page type.</summary>
    [Parameter, EditorRequired]
    public RouteData RouteData { get; set; } = default!;

    /// <summary>
    /// Layout used when no higher-priority signal resolves. Mirrors
    /// <see cref="RouteView.DefaultLayout"/>.
    /// </summary>
    [Parameter]
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors
                              | DynamicallyAccessedMemberTypes.PublicProperties)]
    public Type? DefaultLayout { get; set; }

    /// <summary>
    /// Layout key used when <see cref="DefaultLayout"/> is not supplied — resolved through
    /// <see cref="ILayoutRegistry"/> like every other key in this system.
    /// </summary>
    /// <remarks>
    /// The string form exists so a router can carry a default layout without carrying a
    /// <see cref="Type"/> through a component parameter, which drags trim annotations across the
    /// whole call chain and makes the router reflection-visible. A missing key logs once and
    /// falls back to <see cref="DefaultLayout"/>, exactly like <see cref="LayoutKeyAttribute"/>.
    /// </remarks>
    [Parameter] public string? DefaultLayoutKey { get; set; }

    /// <summary>Content shown when authorization fails for the routed page.</summary>
    [Parameter] public RenderFragment<AuthenticationState>? NotAuthorized { get; set; }

    /// <summary>Content shown while authorization is being evaluated.</summary>
    [Parameter] public RenderFragment? Authorizing { get; set; }

    // Cache to suppress repeated warnings for the same missing (key, page) pair —
    // otherwise every re-render of a 404-style page with [LayoutKey("X")] floods logs.
    private static readonly HashSet<(string Key, Type Page)> WarnedMissing = [];

    private Type? _previousPageType;
    private Type? _resolvedLayout;
    private bool _noLayout;
    private bool _subscribed;

    protected override void OnInitialized()
    {
        Context.OnChange += HandleContextChanged;
        _subscribed = true;
    }

    protected override void OnParametersSet()
    {
        // Page change => drop any prior dynamic override so navigations always start
        // from the static attribute path. Done before Resolve() so the dropped state
        // is reflected in the first render of the new page.
        if (RouteData?.PageType is { } page && page != _previousPageType)
        {
            _previousPageType = page;
            Context.ClearOverride();
        }

        Resolve();
    }

    private void HandleContextChanged()
    {
        Resolve();
        _ = InvokeAsync(StateHasChanged);
    }

    private void Resolve()
    {
        var pageType = RouteData?.PageType;
        if (pageType is null)
        {
            _resolvedLayout = DefaultLayout;
            _noLayout = false;
            return;
        }

        // 1. Dynamic override wins outright.
        if (Context.HasOverride)
        {
            if (Context.IsNoLayout)
            {
                _noLayout = true;
                _resolvedLayout = null;
                return;
            }

            _noLayout = false;
            _resolvedLayout = ResolveByKey(Context.Key, pageType);
            return;
        }

        // 2. Static [NoLayout].
        if (pageType.GetCustomAttribute<NoLayoutAttribute>() is not null)
        {
            _noLayout = true;
            _resolvedLayout = null;
            return;
        }

        // 3. Static [LayoutKey("...")].
        if (pageType.GetCustomAttribute<LayoutKeyAttribute>() is { } keyAttr)
        {
            _noLayout = false;
            _resolvedLayout = ResolveByKey(keyAttr.Key, pageType);
            return;
        }

        // 4. Standard [Layout(typeof(X))] is consumed by AuthorizeRouteView itself —
        //    we do nothing here. Setting our DefaultLayout below is harmless because
        //    AuthorizeRouteView preserves the page-level attribute's precedence.
        // 5. Site / router default — the keyed form first, so a Site configured through
        //    DocumentOptions.DefaultLayoutKey wins over a hard-coded type.
        _noLayout = false;
        _resolvedLayout = string.IsNullOrEmpty(DefaultLayoutKey)
            ? DefaultLayout
            : ResolveByKey(DefaultLayoutKey, pageType);
    }

    private Type? ResolveByKey(string? key, Type pageType)
    {
        if (string.IsNullOrEmpty(key))
            return DefaultLayout;

        if (Registry.TryResolve(key, out var resolved))
            return resolved;

        // Deduplicated warning — one per (key, page) pair per process lifetime.
        var tag = (key, pageType);
        bool emitWarning;
        lock (WarnedMissing) emitWarning = WarnedMissing.Add(tag);
        if (emitWarning)
        {
            Logger.LogWarning(
                "Layout key '{Key}' is not registered (page {Page}); falling back to default layout. " +
                "Register it via services.AddWebsiteLayout<TLayout>(\"{Key}\").",
                key, pageType.FullName, key);
        }

        return DefaultLayout;
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        if (_noLayout)
        {
            // Wrap AuthorizeRouteView in a LayoutView whose Layout is null — Blazor's
            // LayoutView renders ChildContent directly when Layout == null, which is
            // exactly what we want for [NoLayout] (no chrome at all). We can't simply
            // pass null as DefaultLayout to AuthorizeRouteView because Blazor will
            // still apply any page-level [Layout]; the wrapping LayoutView shortcut
            // is what gives [NoLayout] its win-over-everything semantics.
            BuildAuthorizeRouteView(builder, defaultLayout: null);
        }
        else
        {
            BuildAuthorizeRouteView(builder, defaultLayout: _resolvedLayout);
        }

        // The document head is emitted here rather than from a layout so that it is present for
        // EVERY routed page — including [NoLayout] ones, and including hosts that supply their
        // own layouts, each of which would otherwise have to remember it. It renders through
        // PageTitle / HeadContent, so its position in the tree does not constrain where the
        // markup lands in the document.
        //
        // AFTER the page, not before: the page publishes its metadata during its own
        // initialisation, so composing the head first would always start from the tenant
        // fallbacks and need a second pass to correct itself. The notification path exists
        // anyway — a title resolved after an await cannot be seen any other way — but ordering
        // it this way makes the common, synchronous case right on the first attempt.
        builder.OpenComponent<PageHead>(20);
        if (RouteData?.PageType is { } routed)
            builder.AddComponentParameter(21, nameof(PageHead.PageType), routed);
        builder.CloseComponent();
    }

    private void BuildAuthorizeRouteView(RenderTreeBuilder builder, Type? defaultLayout)
    {
        builder.OpenComponent<AuthorizeRouteView>(10);
        builder.AddAttribute(11, nameof(AuthorizeRouteView.RouteData), RouteData);

        // AuthorizeRouteView.DefaultLayout has a [DynamicallyAccessedMembers] annotation;
        // our parameter carries the same so trim analysis is happy end-to-end.
        if (defaultLayout is not null)
            builder.AddAttribute(12, nameof(AuthorizeRouteView.DefaultLayout), defaultLayout);

        if (NotAuthorized is not null)
            builder.AddAttribute(13, nameof(AuthorizeRouteView.NotAuthorized), NotAuthorized);

        if (Authorizing is not null)
            builder.AddAttribute(14, nameof(AuthorizeRouteView.Authorizing), Authorizing);

        builder.CloseComponent();
    }

    public void Dispose()
    {
        if (_subscribed)
        {
            Context.OnChange -= HandleContextChanged;
            _subscribed = false;
        }
    }
}
