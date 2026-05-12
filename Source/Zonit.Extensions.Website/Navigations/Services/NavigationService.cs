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
/// <c>IPermissionChecker</c> abstraction yet). <see cref="NavGroupModel.Permission"/>
/// remains as data the UI can consult.</para>
/// </remarks>
internal sealed class NavigationService : INavigationProvider
{
    private readonly IReadOnlyDictionary<string, IReadOnlyList<NavGroupModel>> _staticByArea;
    private readonly ConcurrentDictionary<string, List<NavGroupModel>> _runtime = new(StringComparer.OrdinalIgnoreCase);

    public event Action<string?>? OnChanged;

    public NavigationService(IEnumerable<IWebsiteArea> areas)
    {
        _staticByArea = areas
            .GroupBy(a => a.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<NavGroupModel>)g.SelectMany(a => a.Navigation ?? Array.Empty<NavGroupModel>()).ToList(),
                StringComparer.OrdinalIgnoreCase);
    }

    public void Add(NavGroupModel model, string? areaKey = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        var key = areaKey ?? string.Empty;
        var list = _runtime.GetOrAdd(key, _ => new List<NavGroupModel>());
        lock (list) { list.Add(model); }
        OnChanged?.Invoke(areaKey);
    }

    public void Clear(string? areaKey = null)
    {
        if (areaKey is null) _runtime.Clear();
        else _runtime.TryRemove(areaKey, out _);
        OnChanged?.Invoke(areaKey);
    }

    public IReadOnlyList<NavGroupModel> Get(string areaKey, string? position = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(areaKey);

        IEnumerable<NavGroupModel> source = Array.Empty<NavGroupModel>();

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
