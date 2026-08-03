using System.Collections.Concurrent;

namespace Zonit.Extensions.Website.Navigations.Services;

/// <summary>
/// Process-wide navigation store: the per-area static contributions collected from
/// <see cref="IWebsiteArea.Navigation"/> at startup, plus whatever was added at runtime
/// through <see cref="INavigationProvider.Add"/>.
/// </summary>
/// <remarks>
/// <para><b>Why the store is separate from <see cref="NavigationService"/>.</b> Navigation
/// visibility depends on the active Site (<see cref="ICurrentSite"/>), which is a
/// <em>scoped</em> concept — an HTTP request scope or a SignalR circuit scope. The data
/// being filtered is not: a group added by a hosted service at startup must be visible to
/// every request and every circuit. Those two lifetimes cannot live in one object, so the
/// data lives here as a singleton and the per-scope view lives in
/// <see cref="NavigationService"/>.</para>
///
/// <para><b>The event stays here on purpose.</b> <see cref="OnChanged"/> must reach
/// subscribers in <em>all</em> scopes — an <see cref="INavigationProvider.Add"/> executed
/// on one circuit has to repaint the menu of every other one. <see cref="NavigationService"/>
/// therefore forwards its own event accessors straight to this instance rather than keeping
/// a per-scope subscriber list.</para>
/// </remarks>
internal sealed class NavigationRegistry
{
    private readonly IReadOnlyDictionary<string, IReadOnlyList<NavGroup>> _staticByArea;
    private readonly ConcurrentDictionary<string, List<NavGroup>> _runtime = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Raised whenever the stored navigation changes. Argument: area key (<c>null</c> = all).</summary>
    public event Action<string?>? OnChanged;

    /// <param name="registry">
    /// Area registry populated by <c>AddWebsite(o =&gt; o.AddArea&lt;TArea&gt;())</c>, source of the
    /// static per-area <see cref="IWebsiteArea.Navigation"/> contributions.
    /// <para><b>Optional by design</b> — the default makes the parameter satisfiable by a container
    /// that has no <see cref="WebsiteAreaRegistry"/> registered, which is what
    /// <c>AddNavigationsExtension()</c> called on its own produces. Without the default the
    /// standalone registration builds a container that cannot be validated
    /// (<c>Unable to resolve service for type 'WebsiteAreaRegistry'</c>) and throws on first use;
    /// with it, a navigation-only container works and simply has no static contributions —
    /// runtime <see cref="Add"/> is unaffected. This is not the same as registering a
    /// <see cref="WebsiteAreaRegistry"/> ourselves: doing that would let a
    /// <c>AddNavigationsExtension()</c>-before-<c>AddWebsite()</c> ordering win the
    /// <c>TryAddSingleton</c> race and hand out an <em>empty</em> registry while
    /// <c>o.AddArea&lt;T&gt;()</c> filled a different instance.</para>
    /// </param>
    public NavigationRegistry(WebsiteAreaRegistry? registry = null)
    {
        // Materialised once for the process: the area list is fixed after startup and
        // this projection would otherwise re-run for every circuit that renders a menu.
        _staticByArea = registry is null
            ? new Dictionary<string, IReadOnlyList<NavGroup>>(StringComparer.OrdinalIgnoreCase)
            : registry.AsAreas()
                .GroupBy(a => a.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => (IReadOnlyList<NavGroup>)g.SelectMany(a => a.Navigation ?? Array.Empty<NavGroup>()).ToList(),
                    StringComparer.OrdinalIgnoreCase);
    }

    public void Add(NavGroup model, string? areaKey)
    {
        ArgumentNullException.ThrowIfNull(model);
        var key = areaKey ?? string.Empty;
        var list = _runtime.GetOrAdd(key, _ => new List<NavGroup>());
        lock (list) { list.Add(model); }
        OnChanged?.Invoke(areaKey);
    }

    public void Clear(string? areaKey)
    {
        if (areaKey is null) _runtime.Clear();
        else _runtime.TryRemove(areaKey, out _);
        OnChanged?.Invoke(areaKey);
    }

    public void Refresh(string? areaKey) => OnChanged?.Invoke(areaKey);

    /// <summary>
    /// Static + runtime groups for one area, ordered, optionally narrowed to a position.
    /// Site filtering deliberately does <em>not</em> happen here — the store has no scope
    /// and therefore no way to know which Site is being rendered.
    /// </summary>
    public IReadOnlyList<NavGroup> Get(string areaKey, string? position)
    {
        IEnumerable<NavGroup> source = Array.Empty<NavGroup>();

        if (_staticByArea.TryGetValue(areaKey, out var staticGroups))
            source = source.Concat(staticGroups);

        if (_runtime.TryGetValue(areaKey, out var dynamicGroups))
        {
            lock (dynamicGroups) { source = source.Concat(dynamicGroups.ToArray()); }
        }

        if (position is not null)
            source = source.Where(g => string.Equals(g.Position, position, StringComparison.OrdinalIgnoreCase));

        return source.OrderBy(g => g.Order).ToArray();
    }
}
