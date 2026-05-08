using Zonit.Extensions.Website.Abstractions.Navigations.Models;
using Zonit.Extensions.Website.Abstractions.Navigations.Types;

namespace Zonit.Extensions.Website;

public interface INavigationProvider
{
    /// <summary>
    /// Adding a new navigation
    /// </summary>
    /// <param name="model"></param>
    public void Add(NavGroupModel model);

    /// <summary>
    /// Downloading navigation by area
    /// </summary>
    /// <param name="area">The area type to filter navigation groups by.</param>
    /// <param name="position">Optional position filter (e.g. "header", "sidebar"); <c>null</c> returns all positions.</param>
    /// <returns>Matching navigation groups, or <c>null</c> if none defined.</returns>
    public IReadOnlyList<NavGroupModel>? Get(AreaType area, string? position);
}