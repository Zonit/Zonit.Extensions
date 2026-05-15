using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;

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
    private readonly IHttpContextAccessor _httpContext;

    public event Action<string?>? OnChanged;

    public NavigationService(WebsiteAreaRegistry registry, IHttpContextAccessor httpContext)
    {
        _httpContext = httpContext;
        _staticByArea = registry.AsAreas()
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

        // Per-Site filtering: if a request is being served from a Site branch (the
        // branch middleware called ICurrentSite.Set), hide nav for areas that aren't
        // mounted on the active Site. Outside a Site (e.g. a global endpoint) every
        // area's nav is visible — same behaviour as before the per-Site split.
        var currentSite = _httpContext.HttpContext?.RequestServices.GetService(typeof(ICurrentSite)) as ICurrentSite;
        if (currentSite is { IsSet: true } && !currentSite.AreaKeys.Contains(areaKey))
            return Array.Empty<NavGroup>();

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
