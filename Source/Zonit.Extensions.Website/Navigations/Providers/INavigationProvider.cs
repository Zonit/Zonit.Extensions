namespace Zonit.Extensions.Website;

/// <summary>
/// Aggregates navigation contributions from registered <see cref="IWebsiteArea"/>s
/// and ad-hoc additions, with permission-aware filtering and change notifications.
/// </summary>
public interface INavigationProvider
{
    /// <summary>
    /// Registers an additional navigation group at runtime (in-memory; lost on app restart).
    /// Useful for dynamic features that are revealed conditionally.
    /// </summary>
    void Add(NavGroupModel model, string? areaKey = null);

    /// <summary>
    /// Removes runtime-added groups by area key.
    /// </summary>
    /// <param name="areaKey">If <c>null</c>, clears all runtime additions.</param>
    void Clear(string? areaKey = null);

    /// <summary>
    /// Gets the visible navigation tree for a given area / position.
    /// </summary>
    /// <param name="areaKey">Area to filter by (matches <see cref="IWebsiteArea.Key"/>).</param>
    /// <param name="position">Optional layout position filter (e.g. <c>"sidebar"</c>); <c>null</c> = all.</param>
    /// <returns>Ordered, permission-filtered navigation groups; empty list when nothing matches.</returns>
    IReadOnlyList<NavGroupModel> Get(string areaKey, string? position = null);

    /// <summary>
    /// Forces re-evaluation of navigation (e.g. after a permission change like
    /// "user joined affiliate") and raises <see cref="OnChanged"/>.
    /// </summary>
    /// <param name="areaKey">Refresh a specific area, or <c>null</c> for all.</param>
    void Refresh(string? areaKey = null);

    /// <summary>Raised when navigation visibility changes. Argument: area key (<c>null</c> = all).</summary>
    event Action<string?>? OnChanged;
}
