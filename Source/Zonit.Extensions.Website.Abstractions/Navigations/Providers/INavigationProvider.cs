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
    /// <param name="area"></param>
    /// <returns></returns>
    public IReadOnlyList<NavGroupModel>? Get(AreaType area, string? position);
}