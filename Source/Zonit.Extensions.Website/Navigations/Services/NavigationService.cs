using Zonit.Extensions.Cultures;
using Microsoft.Extensions.DependencyInjection;

namespace Zonit.Extensions.Website.Navigations.Services;

/// <summary>
/// Default <see cref="INavigationProvider"/> implementation.
/// </summary>
/// <remarks>
/// <para>Combines per-area static <see cref="IWebsiteArea.Navigation"/> contributions (provided
/// at registration time via <see cref="WebsiteOptions.AddArea"/>) with runtime-added groups
/// (<see cref="Add"/>). Filtering is by area key and optional position, then by the Site the
/// current scope is rendering.</para>
///
/// <para><b>Transient, and both halves of that matter.</b> The shared, process-wide data lives
/// in the singleton <see cref="NavigationRegistry"/>; this type is only the per-caller view of
/// it. A <em>transient</em> facade is the one lifetime that satisfies the two things this
/// service has to do at once:</para>
/// <list type="number">
///   <item>A singleton may take it. Seeding menus from an <c>IHostedService</c>
///     (<c>internal class NavData(INavigationProvider nav) : IHostedService</c> registered with
///     <c>AddHostedService</c>) is a first-class pattern in this framework's own hosts. Scoped
///     would reject that registration outright — <c>Cannot consume scoped service
///     'INavigationProvider' from singleton 'IHostedService'</c> — and everything such a service
///     does is <see cref="Add"/>, which lands in the singleton store and is visible everywhere.</item>
///   <item>It still sees the caller's scope. <see cref="Get"/> filters by the active Site, which
///     means reading <see cref="ICurrentSite"/> — a scoped service. A transient resolved from a
///     request or circuit scope is handed <em>that</em> scope's <see cref="IServiceProvider"/>,
///     so the lookup reaches the same self-hydrating <see cref="ICurrentSite"/> the rest of the
///     framework uses. A singleton facade cannot: it would be one object shared by every
///     concurrent circuit, with no way to know which Site any given caller is rendering. That
///     was the pre-10.0.0-preview.10 bug — the old singleton reached for
///     <c>IHttpContextAccessor.HttpContext?.RequestServices</c>, a SignalR circuit has no
///     <c>HttpContext</c>, so the filter was skipped and an interactive render happily showed
///     navigation for areas that are not mounted on the active Site.</item>
/// </list>
///
/// <para><b>Resolved from the root there is no Site, and no filtering.</b> That is not a
/// silent failure, it is the same answer <see cref="ICurrentSite"/> itself gives outside any
/// registered mount (<c>IsSet == false</c>): nothing is known about the active Site, so nothing
/// is hidden. A hosted service that seeds menus never calls <see cref="Get"/>; one that does
/// gets the unfiltered store, which is the only defensible answer when there is no scope to
/// ask.</para>
///
/// <para>Permission-based filtering is intentionally NOT done here – pass a
/// <c>predicate</c>-style filter in the UI layer if needed (we don't have a global
/// <c>IPermissionChecker</c> abstraction yet). <see cref="NavGroup.Permission"/>
/// remains as data the UI can consult.</para>
/// </remarks>
internal sealed class NavigationService : INavigationProvider
{
    private readonly NavigationRegistry _registry;
    private readonly NavigationRootScope _root;
    private readonly IServiceProvider _services;

    /// <inheritdoc />
    /// <remarks>
    /// Forwarded verbatim to the singleton store: a group added on one scope has to repaint
    /// menus on every other one, so per-scope subscriber lists would be wrong. Forwarding also
    /// makes the transient lifetime invisible here — subscribing through one instance and
    /// unsubscribing through another still works, because both talk to the same store.
    /// </remarks>
    public event Action<string?>? OnChanged
    {
        add => _registry.OnChanged += value;
        remove => _registry.OnChanged -= value;
    }

    /// <param name="registry">Process-wide navigation store.</param>
    /// <param name="root">
    /// Marker holding the container's root provider, used to detect the no-scope case before
    /// asking for the scoped <see cref="ICurrentSite"/>. See <see cref="NavigationRootScope"/>.
    /// </param>
    /// <param name="services">
    /// The provider this instance was resolved from — a request scope, a circuit scope, or the
    /// root. Used to look up <see cref="ICurrentSite"/> lazily. The lookup stays optional
    /// (<c>GetService</c>, not <c>GetRequiredService</c>) because a container that registered
    /// <c>AddNavigationsExtension()</c> without the rest of <c>AddWebsite</c> has no
    /// <see cref="ICurrentSite"/> at all; there is then no Site concept and no filtering to do.
    /// </param>
    public NavigationService(NavigationRegistry registry, NavigationRootScope root, IServiceProvider services)
    {
        _registry = registry;
        _root = root;
        _services = services;
    }

    public void Add(NavGroup model, string? areaKey = null) => _registry.Add(model, areaKey);

    public void Clear(string? areaKey = null) => _registry.Clear(areaKey);

    public IReadOnlyList<NavGroup> Get(string areaKey, string? position = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(areaKey);

        // Per-Site filtering: hide nav for areas that aren't mounted on the active Site.
        // Outside any registered mount ICurrentSite reports IsSet=false and every area's
        // nav is visible — same behaviour as before the per-Site split.
        var currentSite = ResolveCurrentSite();
        if (currentSite is { IsSet: true } && !currentSite.AreaKeys.Contains(areaKey))
            return Array.Empty<NavGroup>();

        return Localize(_registry.Get(areaKey, position));
    }

    /// <summary>
    /// Runs every title and tooltip through the translation registry, honouring each node's
    /// <c>Translate</c> flag.
    /// </summary>
    /// <remarks>
    /// <para>Done here rather than in the renderers so that a menu is translated once, for every
    /// UI layer, without the author writing <c>T(…)</c> at each declaration site. The key in this
    /// framework <b>is</b> the English source string, so <c>Title = "Settings"</c> is already a
    /// valid key and text with no rendition falls through to itself — a brand name survives
    /// untouched whether or not the author remembered to set <c>Translate = false</c>.</para>
    ///
    /// <para>The tree is rebuilt with <c>with</c> expressions rather than mutated in place: the
    /// registry's copy is shared by every scope and every language, so translating in place would
    /// pin the first request's language onto the whole process. Using <c>with</c> rather than a
    /// hand-written copy constructor also means a property added later is carried across
    /// automatically — the alternative silently drops it.</para>
    ///
    /// <para>Skipped entirely when there is no culture provider (navigation wired without the rest
    /// of the framework, or resolved from the root where scoped services do not exist), so the
    /// registry's objects are returned untouched and nothing allocates.</para>
    /// </remarks>
    private IReadOnlyList<NavGroup> Localize(IReadOnlyList<NavGroup> groups)
    {
        if (groups.Count == 0)
            return groups;

        var culture = ResolveCulture();
        if (culture is null)
            return groups;

        var result = new NavGroup[groups.Count];
        for (var i = 0; i < groups.Count; i++)
            result[i] = LocalizeGroup(groups[i], culture);

        return result;
    }

    private static NavGroup LocalizeGroup(NavGroup group, ICultureProvider culture)
        => group with
        {
            Title = group.Translate ? Translate(group.Title, culture) : group.Title,
            Tooltip = group.Translate ? Translate(group.Tooltip, culture) : group.Tooltip,
            Link = group.Link is null ? null : LocalizeLink(group.Link, culture),
            Children = group.Children is null ? null : LocalizeItems(group.Children, culture),
            Groups = group.Groups is null ? null : [.. group.Groups.Select(g => LocalizeGroup(g, culture))],
        };

    private static IReadOnlyList<NavItem> LocalizeItems(IReadOnlyList<NavItem> items, ICultureProvider culture)
    {
        var result = new NavItem[items.Count];
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            result[i] = item with
            {
                Title = item.Translate ? Translate(item.Title, culture) : item.Title,
                Tooltip = item.Translate ? Translate(item.Tooltip, culture) : item.Tooltip,
                Children = item.Children is null ? null : LocalizeItems(item.Children, culture),
            };
        }

        return result;
    }

    private static LinkModel LocalizeLink(LinkModel link, ICultureProvider culture)
        => link with { Title = Translate(link.Title, culture) };

    private static Title Translate(Title title, ICultureProvider culture)
    {
        var value = title.Value;
        if (string.IsNullOrWhiteSpace(value))
            return title;

        // TryCreate, not the throwing ctor: Title has a MaxLength and a translation can be longer
        // than the source string. A menu label that grew past the limit in one language must not
        // take the whole navigation down — keeping the untranslated label is the graceful answer.
        return Title.TryCreate(culture.Translate(value).Value, out var translated) ? translated : title;
    }

    private static string? Translate(string? text, ICultureProvider culture)
        => string.IsNullOrWhiteSpace(text) ? text : culture.Translate(text).Value;

    /// <summary>
    /// The culture provider of the scope this instance belongs to, or <see langword="null"/> when
    /// there is no scope or the host wired navigation without the culture extension.
    /// </summary>
    private ICultureProvider? ResolveCulture()
    {
        if (ReferenceEquals(_services, _root.Provider))
            return null;

        return _services.GetService<ICultureProvider>();
    }

    public void Refresh(string? areaKey = null) => _registry.Refresh(areaKey);

    /// <summary>
    /// The active Site of the scope this instance belongs to, or <see langword="null"/> when
    /// there is no such scope (resolved from the root) or the host wired navigation without
    /// <c>AddWebsite</c> (no <see cref="ICurrentSite"/> registered). Both cases mean "no Site
    /// is known", which <see cref="Get"/> treats as "do not filter".
    /// </summary>
    private ICurrentSite? ResolveCurrentSite()
    {
        // ICurrentSite is scoped. Asking the ROOT provider for a scoped service throws
        // ("Cannot resolve scoped service … from root provider") whenever the host enables
        // ServiceProviderOptions.ValidateScopes — the generic host does so in Development.
        // Checking first is deterministic; catching the exception afterwards would hide real
        // container-wiring mistakes behind the same code path.
        if (ReferenceEquals(_services, _root.Provider))
            return null;

        return _services.GetService<ICurrentSite>();
    }
}
