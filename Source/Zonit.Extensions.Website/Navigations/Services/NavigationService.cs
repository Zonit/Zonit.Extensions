using System.Collections.Concurrent;

namespace Zonit.Extensions.Website.Navigations.Services;

/// <summary>
/// Default <see cref="INavigationProvider"/> implementation.
/// </summary>
/// <remarks>
/// Combines per-area static <see cref="IWebsiteArea.Navigation"/> contributions (provided
/// at registration time via <see cref="WebsiteOptions.AddArea"/>) with runtime-added groups
/// (<see cref="Add"/>). Filtering is by area key and optional position.
///
/// <para>Permission-based filtering is intentionally NOT done here – pass a
/// <c>predicate</c>-style filter in the UI layer if needed (we don't have a global
/// <c>IPermissionChecker</c> abstraction yet). <see cref="NavGroup.Permission"/>
/// remains as data the UI can consult.</para>
/// </remarks>
internal sealed class NavigationService : INavigationProvider
{
    private readonly IReadOnlyDictionary<string, IReadOnlyList<NavGroup>> _staticByArea;
    private readonly ConcurrentDictionary<string, List<NavGroup>> _runtime = new(StringComparer.OrdinalIgnoreCase);

    public event Action<string?>? OnChanged;

    public NavigationService(IEnumerable<IWebsiteArea> areas)
    {
        _staticByArea = areas
            .GroupBy(a => a.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<NavGroup>)g.SelectMany(a => a.Navigation ?? Array.Empty<NavGroup>()).ToList(),
                StringComparer.OrdinalIgnoreCase);
    }

    public void Add(NavGroup model, string? areaKey = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        var key = areaKey ?? string.Empty;
        var list = _runtime.GetOrAdd(key, _ => new List<NavGroup>());
        lock (list) { list.Add(model); }
        OnChanged?.Invoke(areaKey);
    }

    public void Clear(string? areaKey = null)
    {
        if (areaKey is null) _runtime.Clear();
        else _runtime.TryRemove(areaKey, out _);
        OnChanged?.Invoke(areaKey);
    }

    public IReadOnlyList<NavGroup> Get(string areaKey, string? position = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(areaKey);

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

    public void Refresh(string? areaKey = null) => OnChanged?.Invoke(areaKey);
}
