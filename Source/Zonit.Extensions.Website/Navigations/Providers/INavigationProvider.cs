namespace Zonit.Extensions.Website;

/// <summary>
/// Aggregates navigation contributions from registered <see cref="IWebsiteArea"/>s
/// and ad-hoc additions, with permission-aware filtering and change notifications.
/// </summary>
/// <remarks>
/// <para><b>Lifetime: transient.</b> The data behind it is process-wide, so it is safe to take
/// this dependency from a singleton — seeding menus from an <c>IHostedService</c> at startup is
/// a supported pattern and needs no scope of its own.</para>
///
/// <para><b>Where you resolve it decides what <see cref="Get"/> hides.</b> An instance resolved
/// from a request or circuit scope filters out areas that are not mounted on the Site that scope
/// is rendering; one resolved from the root has no Site to filter by and returns everything.
/// Inject it into components and services and let the container hand you the right one — do not
/// cache an instance obtained at startup and read <see cref="Get"/> from it later, or you will
/// read the unfiltered view from inside a Site.</para>
/// </remarks>
public interface INavigationProvider
{
    /// <summary>
    /// Registers an additional navigation group at runtime (in-memory; lost on app restart).
    /// Useful for dynamic features that are revealed conditionally.
    /// </summary>
    void Add(NavGroup model, string? areaKey = null);

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
    IReadOnlyList<NavGroup> Get(string areaKey, string? position = null);

    /// <summary>
    /// Forces re-evaluation of navigation (e.g. after a permission change like
    /// "user joined affiliate") and raises <see cref="OnChanged"/>.
    /// </summary>
    /// <param name="areaKey">Refresh a specific area, or <c>null</c> for all.</param>
    void Refresh(string? areaKey = null);

    /// <summary>Raised when navigation visibility changes. Argument: area key (<c>null</c> = all).</summary>
    event Action<string?>? OnChanged;
}
